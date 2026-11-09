using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gratia.Application.DTOs.Invitation;
using Microsoft.EntityFrameworkCore;

namespace GratiaTests.Integration;

public sealed class InvitationEndpointsTests(GratiaApiFactory factory) : IClassFixture<GratiaApiFactory>
{
    [Fact]
    public async Task Invited_user_can_accept_and_then_log_in()
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        var email = NewEmail();

        var invite = await admin.PostAsJsonAsync("/api/invitation", new { email });
        Assert.Equal(HttpStatusCode.Created, invite.StatusCode);
        var created = await invite.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(created.TryGetProperty("invitationToken", out _), "The token must only go out by email (#28)");

        Assert.Contains(email, await PendingEmailsAsync(admin));

        // The invitee opens the link from the email
        var token = factory.Emails.LastTokenFor(email);
        var anonymous = factory.CreateClient();
        var lookup = await anonymous.GetFromJsonAsync<InvitationDto>($"/api/invitation/token/{token}", GratiaApiFactory.Json);
        Assert.Equal("Acme", lookup!.CompanyName);
        Assert.Equal("Acme Admin", lookup.InvitedByUserName);
        Assert.True(lookup.IsValid);

        var accept = await anonymous.PostAsJsonAsync("/api/invitation/accept",
            new { token, fullName = "New Colleague", password = GratiaApiFactory.Password });
        Assert.Equal(HttpStatusCode.NoContent, accept.StatusCode);

        await factory.CreateClientForAsync(email); // throws unless the new account can log in
        Assert.DoesNotContain(email, await PendingEmailsAsync(admin));

        var acceptAgain = await anonymous.PostAsJsonAsync("/api/invitation/accept",
            new { token, fullName = "New Colleague", password = GratiaApiFactory.Password });
        Assert.Equal(HttpStatusCode.BadRequest, acceptAgain.StatusCode);
    }

    [Fact]
    public async Task Inviting_the_same_email_twice_is_a_conflict()
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        var email = NewEmail();

        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/invitation", new { email })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/invitation", new { email })).StatusCode);
    }

    [Fact]
    public async Task An_unverified_company_can_only_invite_its_own_domain()
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);

        var outsider = await admin.PostAsJsonAsync("/api/invitation", new { email = NewEmail("elsewhere.test") });
        Assert.Equal(HttpStatusCode.BadRequest, outsider.StatusCode);
        Assert.Contains("@acme.test", await outsider.Content.ReadAsStringAsync());

        // Case doesn't matter, and the address is stored lowercase
        var colleague = await admin.PostAsJsonAsync("/api/invitation", new { email = $"{Guid.NewGuid():N}@ACME.test" });
        Assert.Equal(HttpStatusCode.Created, colleague.StatusCode);
        var invitation = await colleague.Content.ReadFromJsonAsync<InvitationDto>(GratiaApiFactory.Json);
        Assert.EndsWith("@acme.test", invitation!.Email);
        Assert.Equal("Acme", invitation.CompanyName);
        Assert.Equal("Acme Admin", invitation.InvitedByUserName);
    }

    [Fact]
    public async Task Inviting_an_existing_user_is_a_conflict()
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);

        var response = await admin.PostAsJsonAsync("/api/invitation", new { email = factory.AcmeEmployeeEmail });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Resend_sends_a_new_link_and_the_old_one_stops_working()
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        var (id, email) = await InviteAsync(admin);
        var oldToken = factory.Emails.LastTokenFor(email);

        var resend = await admin.PostAsync($"/api/invitation/{id}/resend", null);
        Assert.Equal(HttpStatusCode.OK, resend.StatusCode);

        var newToken = factory.Emails.LastTokenFor(email);
        Assert.NotEqual(oldToken, newToken);

        var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/invitation/token/{oldToken}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/invitation/token/{newToken}")).StatusCode);
    }

    [Fact]
    public async Task An_expired_invitation_stays_in_the_list_and_can_be_resent()
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        var (id, email) = await InviteAsync(admin);
        await ExpireAsync(id);

        // #75: it used to drop out of the list, though it could still be resent
        var listed = Assert.Single(await PendingAsync(admin), i => i.Email == email);
        Assert.True(listed.IsExpired);

        var resent = await admin.PostAsync($"/api/invitation/{id}/resend", null);
        Assert.Equal(HttpStatusCode.OK, resent.StatusCode);
        Assert.False(Assert.Single(await PendingAsync(admin), i => i.Email == email).IsExpired);
    }

    [Fact]
    public async Task An_expired_invitation_followed_by_a_new_one_is_not_listed()
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        var (oldId, email) = await InviteAsync(admin);
        await ExpireAsync(oldId);

        var again = await admin.PostAsJsonAsync("/api/invitation", new { email });
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);

        var listed = Assert.Single(await PendingAsync(admin), i => i.Email == email);
        Assert.NotEqual(oldId, listed.Id);
    }

    [Fact]
    public async Task Cancel_removes_a_pending_invitation()
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        var (id, email) = await InviteAsync(admin);

        var cancel = await admin.DeleteAsync($"/api/invitation/{id}");
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        Assert.DoesNotContain(email, await PendingEmailsAsync(admin));
        var lookup = await factory.CreateClient().GetAsync($"/api/invitation/token/{factory.Emails.LastTokenFor(email)}");
        Assert.Equal(HttpStatusCode.NotFound, lookup.StatusCode);
    }

    [Fact]
    public async Task An_admin_cannot_see_resend_or_cancel_another_companys_invitations()
    {
        var globexAdmin = await factory.CreateClientForAsync(factory.GlobexAdminEmail);
        var (id, email) = await InviteAsync(globexAdmin, "globex.test");

        var acmeAdmin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        Assert.DoesNotContain(email, await PendingEmailsAsync(acmeAdmin));
        Assert.Equal(HttpStatusCode.NotFound, (await acmeAdmin.PostAsync($"/api/invitation/{id}/resend", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await acmeAdmin.DeleteAsync($"/api/invitation/{id}")).StatusCode);

        Assert.Contains(email, await PendingEmailsAsync(globexAdmin));
    }

    [Fact]
    public async Task Employees_cannot_manage_invitations()
    {
        var employee = await factory.CreateClientForAsync(factory.AcmeEmployeeEmail);

        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync("/api/invitation", new { email = NewEmail() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync("/api/invitation/pending")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_callers_cannot_manage_invitations()
    {
        var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/invitation", new { email = NewEmail() })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/invitation/pending")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync($"/api/invitation/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Accepting_with_a_password_shorter_than_the_login_minimum_is_rejected()
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        var (_, email) = await InviteAsync(admin);

        var accept = await factory.CreateClient().PostAsJsonAsync("/api/invitation/accept",
            new { token = factory.Emails.LastTokenFor(email), fullName = "Short Password", password = "1234567" });

        Assert.Equal(HttpStatusCode.BadRequest, accept.StatusCode);
        Assert.Contains(email, await PendingEmailsAsync(admin)); // still unused
    }

    [Fact]
    public async Task An_unknown_token_is_not_found()
    {
        var response = await factory.CreateClient().GetAsync("/api/invitation/token/not-a-real-token");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_role_is_sent_and_returned_by_name()
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);

        var invite = await admin.PostAsJsonAsync("/api/invitation", new { email = NewEmail(), role = "Admin" });
        Assert.Equal(HttpStatusCode.Created, invite.StatusCode);
        Assert.Contains("\"invitedRole\":\"Admin\"", await invite.Content.ReadAsStringAsync());

        var number = await admin.PostAsJsonAsync("/api/invitation", new { email = NewEmail(), role = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, number.StatusCode);
    }

    [Fact]
    public async Task When_the_invitation_email_fails_nothing_is_saved_and_the_admin_can_try_again()
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        var email = NewEmail();
        factory.Emails.FailFor(email);

        var failed = await admin.PostAsJsonAsync("/api/invitation", new { email });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        Assert.DoesNotContain(email, await PendingEmailsAsync(admin));

        // #20: the unsent invitation used to stay, so this was a 409 for the next 7 days
        factory.Emails.StopFailingFor(email);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/invitation", new { email })).StatusCode);
    }

    [Fact]
    public async Task When_a_resent_email_fails_the_link_already_sent_keeps_working()
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        var (id, email) = await InviteAsync(admin);
        var token = factory.Emails.LastTokenFor(email);
        factory.Emails.FailFor(email);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await admin.PostAsync($"/api/invitation/{id}/resend", null)).StatusCode);

        // #20: the token used to be replaced anyway, so the invitee's only link stopped working
        var lookup = await factory.CreateClient().GetFromJsonAsync<InvitationDto>($"/api/invitation/token/{token}", GratiaApiFactory.Json);
        Assert.True(lookup!.IsValid);
    }

    // The seeded companies are unverified, so their admins can only invite addresses on their own domain
    private static string NewEmail(string domain = "acme.test") => $"{Guid.NewGuid():N}@{domain}";

    private static async Task<(Guid Id, string Email)> InviteAsync(HttpClient admin, string domain = "acme.test")
    {
        var email = NewEmail(domain);
        var response = await admin.PostAsJsonAsync("/api/invitation", new { email });
        response.EnsureSuccessStatusCode();
        var invitation = await response.Content.ReadFromJsonAsync<InvitationDto>(GratiaApiFactory.Json);
        return (invitation!.Id, email);
    }

    private static async Task<List<string>> PendingEmailsAsync(HttpClient admin) =>
        (await PendingAsync(admin)).Select(i => i.Email).ToList();

    private static async Task<List<InvitationDto>> PendingAsync(HttpClient admin) =>
        (await admin.GetFromJsonAsync<List<InvitationDto>>("/api/invitation/pending", GratiaApiFactory.Json))!;

    private Task<int> ExpireAsync(Guid invitationId) =>
        factory.WithDbAsync(db => db.CompanyInvitations.Where(i => i.Id == invitationId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.ExpiresAt, DateTime.UtcNow.AddMinutes(-1))));
}
