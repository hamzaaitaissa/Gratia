using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Gratia.Application.DTOs.Invitation;
using Gratia.Application.DTOs.Token;
using Gratia.Domain.Entities;
using Gratia.Domain.Enum;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace GratiaTests.Integration;

// Bulk invitations from a spreadsheet (#54): the template, the preview of each row, and the import, which checks every
// row again. The workbooks are built here with ClosedXML, as a spreadsheet app would save them.
public sealed class InvitationImportTests(GratiaApiFactory factory) : IClassFixture<GratiaApiFactory>
{
    private static readonly string[] Header = ["Email", "Full name", "Job title", "Role"];

    [Fact]
    public async Task The_template_has_the_columns_and_a_role_dropdown()
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);

        var response = await admin.GetAsync("/api/invitation/import/template");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType?.MediaType);
        using var workbook = new XLWorkbook(await response.Content.ReadAsStreamAsync());
        var sheet = workbook.Worksheets.First();
        Assert.Equal(Header, Enumerable.Range(1, 4).Select(c => sheet.Cell(1, c).GetString()));
        var dropdown = Assert.Single(sheet.DataValidations, v => v.Ranges.Any(r => r.Contains(sheet.Cell(2, 4))));
        Assert.Equal(XLAllowedValues.List, dropdown.AllowedValues);
        Assert.Equal("\"Employee,Admin\"", dropdown.Value);
    }

    [Fact]
    public async Task The_preview_says_what_would_happen_to_each_row_and_saves_nothing()
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        var ready = NewEmail("acme.test");
        var invited = await InvitedAsync(admin);
        var elsewhere = await AccountInGlobexOnAcmesDomainAsync();

        // Columns in another order and named loosely, as people rearrange sheets
        var preview = await PreviewAsync(admin, Workbook(["Role", "E-mail", "full name", "Job Title"],
            ["admin", ready, "Ada Lovelace", "Engineer"],      // row 2
            ["", "", "", ""],                                   // empty: left out
            ["", "", "No Email", ""],                           // row 4
            ["", "not-an-email", "", ""],                       // row 5
            ["", NewEmail("elsewhere.test"), "", ""],           // row 6
            ["Boss", NewEmail("acme.test"), "", ""],            // row 7
            ["", ready.ToUpperInvariant(), "", ""],             // row 8
            ["", factory.AcmeEmployeeEmail, "", ""],            // row 9
            ["", invited, "", ""],                              // row 10
            ["", elsewhere, "", ""]));                          // row 11: like any address (#61)

        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var result = (await preview.Content.ReadFromJsonAsync<ImportPreviewDto>(GratiaApiFactory.Json))!;
        Assert.Equal(2, result.Ready);
        Assert.Equal(
        [
            (2, ImportRowStatus.Ready, null),
            (4, ImportRowStatus.Error, "There's no email."),
            (5, ImportRowStatus.Error, "This isn't an email address."),
            (6, ImportRowStatus.Error, "Until your company's domain is verified, you can only invite @acme.test addresses."),
            (7, ImportRowStatus.Error, "The role must be Employee or Admin."),
            (8, ImportRowStatus.Duplicate, "The same email as row 2."),
            (9, ImportRowStatus.Member, "Already a member."),
            (10, ImportRowStatus.Invited, "Already invited."),
            // An account in another company isn't the admin's business: the invitee's email will say why they can't join
            ((int Row, ImportRowStatus Status, string? Message))(11, ImportRowStatus.Ready, null)
        ], result.Rows.Select(r => (r.Row, r.Status, r.Message)));
        var first = result.Rows[0];
        Assert.Equal((ready, "Ada Lovelace", "Engineer", "Admin"), (first.Email, first.FullName, first.JobTitle, first.Role));
        Assert.Equal("Boss", result.Rows.Single(r => r.Row == 7).Role);

        Assert.False(await factory.WithDbAsync(db => db.CompanyInvitations.AnyAsync(i => i.Email == ready)));
    }

    [Fact]
    public async Task The_import_invites_the_ready_rows_with_their_names_and_skips_the_rest()
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        var (lead, newcomer) = (NewEmail("acme.test"), NewEmail("acme.test"));

        var response = await admin.PostAsJsonAsync("/api/invitation/import", new
        {
            rows = new object[]
            {
                new { row = 2, email = lead, fullName = "Grace Hopper", jobTitle = "Lead", role = "Admin" },
                new { row = 3, email = newcomer },
                new { row = 4, email = factory.AcmeEmployeeEmail },
                new { row = 5, email = "nope" }
            }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<ImportResultDto>(GratiaApiFactory.Json))!;
        Assert.Equal(2, result.Invited);
        Assert.Equal([(4, ImportRowStatus.Member), (5, ImportRowStatus.Error)], result.Skipped.Select(s => (s.Row, s.Status)));

        // Their emails go out in the background (#55), and the accept form starts with what the admin knew
        await factory.SendInvitationEmailsAsync();
        var invitation = await factory.CreateClient().GetFromJsonAsync<InvitationDto>(
            $"/api/invitation/token/{factory.Emails.LastTokenFor(lead)}", GratiaApiFactory.Json);
        Assert.Equal(("Grace Hopper", "Lead", UserRole.Admin), (invitation!.FullName, invitation.JobTitle, invitation.InvitedRole));
        var plain = await factory.CreateClient().GetFromJsonAsync<InvitationDto>(
            $"/api/invitation/token/{factory.Emails.LastTokenFor(newcomer)}", GratiaApiFactory.Json);
        Assert.Equal((null, null, UserRole.Employee), (plain!.FullName, plain.JobTitle, plain.InvitedRole));
    }

    [Fact]
    public async Task The_import_checks_each_row_again()
    {
        // Ready in the preview, then invited by hand before the import was confirmed
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        var email = NewEmail("acme.test");
        var preview = (await (await PreviewAsync(admin, Workbook(Header, [email, "", "", ""]))).Content
            .ReadFromJsonAsync<ImportPreviewDto>(GratiaApiFactory.Json))!;
        Assert.Equal(ImportRowStatus.Ready, Assert.Single(preview.Rows).Status);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/invitation", new { email })).StatusCode);

        var result = (await (await admin.PostAsJsonAsync("/api/invitation/import", new { rows = new[] { new { row = 2, email } } })).Content
            .ReadFromJsonAsync<ImportResultDto>(GratiaApiFactory.Json))!;

        Assert.Equal(0, result.Invited);
        Assert.Equal(ImportRowStatus.Invited, Assert.Single(result.Skipped).Status);
        Assert.Equal(1, await factory.WithDbAsync(db => db.CompanyInvitations.CountAsync(i => i.Email == email)));
    }

    [Fact]
    public async Task The_daily_cap_applies_to_each_row()
    {
        // #65: an unverified workspace with a cap of 2 imports 3 people
        await using var api = factory.WithWebHostBuilder(builder => builder.UseSetting("Onboarding:UnverifiedInvitationsPerDay", "2"));
        var company = await factory.AddCompanyAsync();
        var (_, adminEmail) = await factory.AddUserAsync(company, role: UserRole.Admin);
        var admin = await ClientForAsync(api, adminEmail);
        var emails = Enumerable.Range(0, 3).Select(_ => NewEmail(company.Domain!)).ToArray();

        var result = (await (await admin.PostAsJsonAsync("/api/invitation/import",
                new { rows = emails.Select((email, i) => new { row = i + 2, email }) })).Content
            .ReadFromJsonAsync<ImportResultDto>(GratiaApiFactory.Json))!;

        Assert.Equal(2, result.Invited);
        var skipped = Assert.Single(result.Skipped);
        Assert.Equal((4, emails[2]), (skipped.Row, skipped.Email));
        Assert.StartsWith("Until your company's domain is verified, it can send 2 invitation emails a day.", skipped.Reason);
    }

    [Fact]
    public async Task Files_and_imports_over_the_limits_are_refused()
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);

        var tooMany = await PreviewAsync(admin, Workbook(Header, Enumerable.Range(0, 501).Select(_ => new[] { NewEmail("acme.test"), "", "", "" }).ToArray()));
        Assert.Equal("An import can have at most 500 people. Split the list and import each part.", await DetailAsync(tooMany));

        var notAWorkbook = await PreviewAsync(admin, "email\nada@acme.test"u8.ToArray(), "people.csv");
        Assert.StartsWith("This isn't an Excel workbook (.xlsx) that can be read.", await DetailAsync(notAWorkbook));

        var noEmailColumn = await PreviewAsync(admin, Workbook(["Name", "Role"], ["Ada", "Admin"]));
        Assert.StartsWith("The first row must name the columns", await DetailAsync(noEmailColumn));

        var tooBig = await PreviewAsync(admin, new byte[(int)(1.5 * 1024 * 1024)]);
        Assert.Equal("The file is larger than 1 MB. Split the list and import each part.", await DetailAsync(tooBig));

        var tooManyRows = await admin.PostAsJsonAsync("/api/invitation/import",
            new { rows = Enumerable.Range(0, 501).Select(i => new { row = i + 2, email = NewEmail("acme.test") }) });
        Assert.Equal(HttpStatusCode.BadRequest, tooManyRows.StatusCode);
    }

    [Fact]
    public async Task A_formula_is_read_as_its_saved_value_and_never_calculated()
    {
        // ClosedXML saves a formula without a calculated value, as a hand-made file might: there's no email to read
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Invitations");
        for (var c = 0; c < Header.Length; c++)
            sheet.Cell(1, c + 1).Value = Header[c];
        sheet.Cell(2, 1).FormulaA1 = "=\"formula\"&\"@acme.test\"";
        sheet.Cell(2, 2).Value = "Formula";

        var preview = (await (await PreviewAsync(admin, Save(workbook))).Content.ReadFromJsonAsync<ImportPreviewDto>(GratiaApiFactory.Json))!;

        Assert.Equal((ImportRowStatus.Error, "There's no email."), (Assert.Single(preview.Rows).Status, preview.Rows[0].Message));
    }

    [Fact]
    public async Task Only_admins_import_and_only_into_their_own_company()
    {
        var employee = await factory.CreateClientForAsync(factory.AcmeEmployeeEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync("/api/invitation/import/template")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PreviewAsync(employee, Workbook(Header, [NewEmail("acme.test"), "", "", ""]))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync("/api/invitation/import", new { rows = Array.Empty<object>() })).StatusCode);

        // Globex's admin can't invite into Acme: its rows go to Globex, and Acme's addresses are another domain
        var globexAdmin = await factory.CreateClientForAsync(factory.GlobexAdminEmail);
        var (ours, theirs) = (NewEmail("globex.test"), NewEmail("acme.test"));
        var result = (await (await globexAdmin.PostAsJsonAsync("/api/invitation/import", new
        {
            rows = new[] { new { row = 2, email = ours }, new { row = 3, email = theirs } }
        })).Content.ReadFromJsonAsync<ImportResultDto>(GratiaApiFactory.Json))!;

        Assert.Equal(1, result.Invited);
        Assert.Equal(ImportRowStatus.Error, Assert.Single(result.Skipped).Status);
        Assert.Equal(factory.Globex.Id, await factory.WithDbAsync(db => db.CompanyInvitations.Where(i => i.Email == ours).Select(i => i.CompanyId).SingleAsync()));
        var acmeAdmin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        var acmePending = (await acmeAdmin.GetFromJsonAsync<List<InvitationDto>>("/api/invitation/pending", GratiaApiFactory.Json))!;
        Assert.DoesNotContain(acmePending, i => i.Email == ours);
    }

    private static string NewEmail(string domain) => $"{Guid.NewGuid():N}@{domain}";

    private async Task<string> InvitedAsync(HttpClient admin)
    {
        var email = NewEmail("acme.test");
        (await admin.PostAsJsonAsync("/api/invitation", new { email })).EnsureSuccessStatusCode();
        return email;
    }

    // Someone at Globex with an acme.test address: only possible from before the domain rule, but the import mustn't
    // invite them into Acme
    private async Task<string> AccountInGlobexOnAcmesDomainAsync()
    {
        var (id, _) = await factory.AddUserAsync(factory.Globex);
        var email = NewEmail("acme.test");
        await factory.WithDbAsync(db => db.Users.Where(u => u.Id == id).ExecuteUpdateAsync(setters => setters.SetProperty(u => u.Email, email)));
        return email;
    }

    private static byte[] Workbook(string[] header, params string[][] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Invitations");
        for (var c = 0; c < header.Length; c++)
            sheet.Cell(1, c + 1).Value = header[c];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                if (rows[r][c].Length > 0)
                    sheet.Cell(r + 2, c + 1).Value = rows[r][c];
        return Save(workbook);
    }

    private static byte[] Save(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static Task<HttpResponseMessage> PreviewAsync(HttpClient client, byte[] file, string name = "people.xlsx")
    {
        var content = new MultipartFormDataContent { { new ByteArrayContent(file), "file", name } };
        return client.PostAsync("/api/invitation/import/preview", content);
    }

    private static async Task<string?> DetailAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail;
    }

    private static async Task<HttpClient> ClientForAsync(WebApplicationFactory<Program> api, string email)
    {
        var client = api.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, hashedPassword = GratiaApiFactory.Password });
        login.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<TokenResponseDto>())!.AccessToken);
        return client;
    }
}
