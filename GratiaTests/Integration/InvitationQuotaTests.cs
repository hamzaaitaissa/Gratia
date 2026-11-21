using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Gratia.Application.DTOs.Invitation;
using Gratia.Application.DTOs.Token;
using Gratia.Domain.Entities;
using Gratia.Domain.Enum;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GratiaTests.Integration;

// #65: until its domain is verified, a workspace can send a few invitation emails a day, invites and resends together.
// Each test runs on a copy of the API with a low cap, and a company of its own.
public sealed class InvitationQuotaTests(GratiaApiFactory factory) : IClassFixture<GratiaApiFactory>
{
    [Fact]
    public async Task An_unverified_workspace_sends_its_daily_invitations_and_then_waits()
    {
        await using var api = WithCap(2);
        var (company, admin) = await CompanyWithAdminAsync(api);

        var first = await InviteAsync(admin, company);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await InviteAsync(admin, company)).StatusCode);

        var email = NewEmail(company);
        var refused = await admin.PostAsJsonAsync("/api/invitation", new { email });
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.InRange(int.Parse(refused.Headers.GetValues("Retry-After").Single()), 23 * 3600, 24 * 3600);
        var problem = (await refused.Content.ReadFromJsonAsync<ProblemDetails>())!;
        Assert.Equal("Until your company's domain is verified, it can send 2 invitation emails a day. Try again in 24 hours.", problem.Detail);
        Assert.False(await factory.WithDbAsync(db => db.CompanyInvitations.AnyAsync(i => i.Email == email)));
        Assert.Empty(factory.Emails.SentTo(email));

        // A resend is another email, so it waits too
        var id = (await first.Content.ReadFromJsonAsync<InvitationDto>(GratiaApiFactory.Json))!.Id;
        Assert.Equal(HttpStatusCode.TooManyRequests, (await admin.PostAsync($"/api/invitation/{id}/resend", null)).StatusCode);
    }

    [Fact]
    public async Task Cancelling_an_invitation_does_not_give_its_place_back()
    {
        await using var api = WithCap(1);
        var (company, admin) = await CompanyWithAdminAsync(api);
        var invitation = (await (await InviteAsync(admin, company)).Content.ReadFromJsonAsync<InvitationDto>(GratiaApiFactory.Json))!;

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/invitation/{invitation.Id}")).StatusCode);

        // Otherwise invite, cancel, invite again would get round the cap
        Assert.Equal(HttpStatusCode.TooManyRequests, (await InviteAsync(admin, company)).StatusCode);
    }

    [Fact]
    public async Task A_new_window_starts_a_day_after_the_last_one_began()
    {
        await using var api = WithCap(1);
        var (company, admin) = await CompanyWithAdminAsync(api);
        Assert.Equal(HttpStatusCode.Created, (await InviteAsync(admin, company)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await InviteAsync(admin, company)).StatusCode);

        await factory.WithDbAsync(db => db.Companies.Where(c => c.Id == company.Id).ExecuteUpdateAsync(setters =>
            setters.SetProperty(c => c.InvitationWindowStart, DateTime.UtcNow - Company.InvitationWindow - TimeSpan.FromMinutes(1))));

        Assert.Equal(HttpStatusCode.Created, (await InviteAsync(admin, company)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await InviteAsync(admin, company)).StatusCode);
    }

    [Fact]
    public async Task A_verified_workspace_has_no_cap()
    {
        await using var api = WithCap(1);
        var (company, admin) = await CompanyWithAdminAsync(api);
        await factory.WithDbAsync(db => db.Companies.Where(c => c.Id == company.Id).ExecuteUpdateAsync(setters =>
            setters.SetProperty(c => c.DomainVerifiedAt, DateTime.UtcNow)));

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.Created, (await InviteAsync(admin, company)).StatusCode);
    }

    private WebApplicationFactory<Program> WithCap(int cap) =>
        factory.WithWebHostBuilder(builder => builder.UseSetting("Onboarding:UnverifiedInvitationsPerDay", cap.ToString()));

    private async Task<(Company Company, HttpClient Admin)> CompanyWithAdminAsync(WebApplicationFactory<Program> api)
    {
        var company = await factory.AddCompanyAsync();
        var (_, email) = await factory.AddUserAsync(company, role: UserRole.Admin);

        var admin = api.CreateClient();
        var login = await admin.PostAsJsonAsync("/api/auth/login", new { email, hashedPassword = GratiaApiFactory.Password });
        login.EnsureSuccessStatusCode();
        admin.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<TokenResponseDto>())!.AccessToken);
        return (company, admin);
    }

    private static Task<HttpResponseMessage> InviteAsync(HttpClient admin, Company company) =>
        admin.PostAsJsonAsync("/api/invitation", new { email = NewEmail(company) });

    private static string NewEmail(Company company) => $"{Guid.NewGuid():N}@{company.Domain}";
}
