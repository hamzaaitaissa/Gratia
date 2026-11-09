using System.Net;
using System.Net.Http.Json;
using Gratia.Application.DTOs.Invitation;
using Gratia.Domain.Enum;
using Microsoft.EntityFrameworkCore;

namespace GratiaTests.Integration;

public sealed class OnboardingTests(GratiaApiFactory factory) : IClassFixture<GratiaApiFactory>
{
    private const string SignupUrl = "/api/auth/register-company";

    [Fact]
    public async Task A_founder_signs_up_sets_a_password_and_becomes_the_first_admin()
    {
        var domain = NewDomain();
        var email = $"founder@{domain}";

        // Mixed case on purpose: the address and domain are stored lowercase
        var signup = await factory.CreateClient().PostAsJsonAsync(SignupUrl,
            new { companyName = "Initech", fullName = "Peter Gibbons", email = $"Founder@{domain.ToUpperInvariant()}" });
        Assert.Equal(HttpStatusCode.Accepted, signup.StatusCode);

        var setup = Assert.Single(factory.Emails.SentTo(email));
        Assert.Equal(EmailKind.CompanySetup, setup.Kind);

        var lookup = await factory.CreateClient().GetFromJsonAsync<InvitationDto>($"/api/invitation/token/{setup.Token}");
        Assert.Equal("Initech", lookup!.CompanyName);
        Assert.Equal(UserRole.Admin, lookup.InvitedRole);

        await AcceptAsync(setup.Token!, "Peter Gibbons");

        // The founder can log in and use the admin endpoints
        var founder = await factory.CreateClientForAsync(email);
        Assert.Equal(HttpStatusCode.OK, (await founder.GetAsync("/api/invitation/pending")).StatusCode);

        var (company, user) = await factory.WithDbAsync(async db => (
            await db.Companies.SingleAsync(c => c.Domain == domain),
            await db.Users.SingleAsync(u => u.Email == email)));
        Assert.Equal("Initech", company.Name);
        Assert.False(company.IsDomainVerified);
        Assert.Equal(UserRole.Admin, user.Role);
        Assert.Equal(company.Id, user.CompanyId);

        // #10: a new account has the monthly allowance to give, and nothing received yet
        Assert.Equal(25, user.NumberOfPointsAvailable);
        Assert.Equal(0, user.NumberOfPointsAcquired);
        Assert.Contains(factory.Emails.SentTo(email), e => e.Kind == EmailKind.Welcome);

        // The new admin can invite colleagues on the company's domain, but nobody else yet
        Assert.Equal(HttpStatusCode.Created, (await founder.PostAsJsonAsync("/api/invitation", new { email = $"colleague@{domain}" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await founder.PostAsJsonAsync("/api/invitation", new { email = "stranger@elsewhere.test" })).StatusCode);
    }

    [Fact]
    public async Task A_personal_email_address_is_rejected()
    {
        var response = await SignupAsync("Lumbergh Ltd", "Bill", "bill.lumbergh@gmail.com");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.Emails.SentTo("bill.lumbergh@gmail.com"));
    }

    [Fact]
    public async Task Signing_up_with_an_existing_account_only_emails_a_notice()
    {
        var response = await SignupAsync("Acme Two", "Acme Admin", factory.AcmeAdminEmail);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(EmailKind.AccountExists, Assert.Single(factory.Emails.SentTo(factory.AcmeAdminEmail)).Kind);
        Assert.False(await factory.WithDbAsync(db => db.Companies.AnyAsync(c => c.Name == "Acme Two")));
    }

    [Fact]
    public async Task A_second_signup_for_a_domain_with_a_workspace_only_emails_a_notice()
    {
        // acme.test already has a workspace with users
        var response = await SignupAsync("Acme Rival", "Rival", "rival@acme.test");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var notice = Assert.Single(factory.Emails.SentTo("rival@acme.test"));
        Assert.Equal(EmailKind.WorkspaceExists, notice.Kind);
        Assert.Equal("Acme", notice.CompanyName);
        Assert.Equal(1, await factory.WithDbAsync(db => db.Companies.CountAsync(c => c.Domain == "acme.test")));
    }

    [Fact]
    public async Task Signing_up_again_before_finishing_sends_a_new_link()
    {
        var domain = NewDomain();
        var email = $"founder@{domain}";

        await SignupAsync("Chotchkie's", "Stan", email);
        var firstToken = factory.Emails.LastTokenFor(email);
        await SignupAsync("Chotchkies", "Stan", email); // corrected the name
        var secondToken = factory.Emails.LastTokenFor(email);

        Assert.NotEqual(firstToken, secondToken);
        var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/invitation/token/{firstToken}")).StatusCode);
        var lookup = await anonymous.GetFromJsonAsync<InvitationDto>($"/api/invitation/token/{secondToken}");
        Assert.Equal("Chotchkies", lookup!.CompanyName);
        Assert.Equal(1, await factory.WithDbAsync(db => db.Companies.CountAsync(c => c.Domain == domain)));
    }

    [Fact]
    public async Task A_colleague_cannot_take_over_a_workspace_that_is_still_being_set_up()
    {
        var domain = NewDomain();
        await SignupAsync("Penetrode", "Samir", $"samir@{domain}");

        var response = await SignupAsync("Penetrode Inc", "Michael", $"michael@{domain}");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(EmailKind.WorkspaceExists, Assert.Single(factory.Emails.SentTo($"michael@{domain}")).Kind);
    }

    [Fact]
    public async Task Someone_else_can_set_up_a_workspace_whose_founder_never_finished()
    {
        var domain = NewDomain();
        var firstFounder = $"samir@{domain}";
        await SignupAsync("Penetrode", "Samir", firstFounder);

        // Samir's link expires unused
        await factory.WithDbAsync(async db =>
        {
            var invitation = await db.CompanyInvitations.SingleAsync(i => i.Email == firstFounder);
            invitation.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            return await db.SaveChangesAsync();
        });

        var secondFounder = $"michael@{domain}";
        await SignupAsync("Penetrode Inc", "Michael", secondFounder);

        var setup = Assert.Single(factory.Emails.SentTo(secondFounder));
        Assert.Equal(EmailKind.CompanySetup, setup.Kind);
        await AcceptAsync(setup.Token!, "Michael Bolton");

        var company = await factory.WithDbAsync(db => db.Companies.SingleAsync(c => c.Domain == domain));
        Assert.Equal("Penetrode Inc", company.Name);
    }

    [Fact]
    public async Task A_setup_link_cannot_be_resent_through_the_admin_endpoint()
    {
        // Only reachable by an admin of the same company, which a company being set up doesn't have yet,
        // so this checks the guard directly through Acme's admin and a founding invitation planted in Acme
        var invitationId = await factory.WithDbAsync(async db =>
        {
            var invitation = new Gratia.Domain.Entities.CompanyInvitation(
                $"{Guid.NewGuid():N}@acme.test", Guid.NewGuid().ToString("N"), DateTime.UtcNow.AddDays(7), UserRole.Admin, factory.Acme.Id, null);
            db.CompanyInvitations.Add(invitation);
            await db.SaveChangesAsync();
            return invitation.Id;
        });

        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        var response = await admin.PostAsync($"/api/invitation/{invitationId}/resend", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Signup_input_is_validated()
    {
        var response = await SignupAsync("", "", "not-an-email");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static string NewDomain() => $"{Guid.NewGuid():N}.test";

    private Task<HttpResponseMessage> SignupAsync(string companyName, string fullName, string email) =>
        factory.CreateClient().PostAsJsonAsync(SignupUrl, new { companyName, fullName, email });

    private async Task AcceptAsync(string token, string fullName)
    {
        var accept = await factory.CreateClient().PostAsJsonAsync("/api/invitation/accept",
            new { token, fullName, password = GratiaApiFactory.Password });
        Assert.Equal(HttpStatusCode.NoContent, accept.StatusCode);
    }
}
