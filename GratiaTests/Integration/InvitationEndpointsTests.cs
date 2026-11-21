using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Net.Http.Headers;
using Gratia.Application.DTOs.CompanyDTO;
using Gratia.Application.DTOs.Invitation;
using Gratia.Application.Features.Invitations.Commands;
using Gratia.Domain.Enum;
using Gratia.Infrastructure.Repositories;
using Gratia.Application.DTOs.Token;
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

        // The email goes out in the background (#55): queued, then sent
        Assert.Equal("Pending", created.GetProperty("emailStatus").GetString());
        Assert.Empty(factory.Emails.SentTo(email));
        await factory.SendInvitationEmailsAsync();
        Assert.Single(factory.Emails.SentTo(email));
        var listed = Assert.Single(await PendingAsync(admin), i => i.Email == email);
        Assert.Equal(InvitationEmailStatus.Sent, listed.EmailStatus);
        Assert.NotNull(listed.EmailSentAt);

        // The invitee opens the link from the email
        var token = factory.Emails.LastTokenFor(email);
        var anonymous = factory.CreateClient();
        var lookup = await anonymous.GetFromJsonAsync<InvitationDto>($"/api/invitation/token/{token}", GratiaApiFactory.Json);
        Assert.Equal("Acme", lookup!.CompanyName);
        Assert.Equal("Acme Admin", lookup.InvitedByUserName);
        Assert.True(lookup.IsValid);

        var accept = await anonymous.PostAsJsonAsync("/api/invitation/accept",
            new { token, fullName = "New Colleague", password = GratiaApiFactory.Password });
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        // Signed in on the spot (#66), in the inviting company, with the invited role, and the session can be renewed
        var tokens = (await accept.Content.ReadFromJsonAsync<TokenResponseDto>())!;
        var member = factory.CreateClient();
        member.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        Assert.Equal("Acme", (await member.GetFromJsonAsync<ReadCompanyDto>("/api/company/me"))!.Name);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/invitation/pending")).StatusCode); // an employee
        Assert.Equal(HttpStatusCode.OK, (await anonymous.PostAsJsonAsync("/api/auth/refresh-token", tokens.RefreshToken)).StatusCode);

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

        // Until the new email has gone out, the old link keeps working (#20, #55)
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().GetAsync($"/api/invitation/token/{oldToken}")).StatusCode);
        await factory.SendInvitationEmailsAsync();

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

        // Its 7 days start again once the new email has gone out (#55)
        await factory.SendInvitationEmailsAsync();
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
    public async Task An_invitation_is_always_to_the_admins_own_company()
    {
        // The company comes from the admin's token; one named in the body is ignored
        var acmeAdmin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        var email = NewEmail();
        var response = await acmeAdmin.PostAsJsonAsync("/api/invitation", new { email, companyId = factory.Globex.Id });
        response.EnsureSuccessStatusCode();

        var invitation = (await response.Content.ReadFromJsonAsync<InvitationDto>(GratiaApiFactory.Json))!;
        Assert.Equal("Acme", invitation.CompanyName);
        Assert.Equal(factory.Acme.Id,
            await factory.WithDbAsync(db => db.CompanyInvitations.Where(i => i.Id == invitation.Id).Select(i => i.CompanyId).SingleAsync()));
        Assert.Contains(email, await PendingEmailsAsync(acmeAdmin));
        Assert.DoesNotContain(email, await PendingEmailsAsync(await factory.CreateClientForAsync(factory.GlobexAdminEmail)));
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
    public async Task A_failing_email_is_tried_again_then_shown_as_failed_for_resend()
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        var email = NewEmail();
        factory.Emails.FailFor(email);

        // The invite itself succeeds: the email is queued (#55). Before, a mail server failure was a 503 here.
        var invite = await admin.PostAsJsonAsync("/api/invitation", new { email });
        Assert.Equal(HttpStatusCode.Created, invite.StatusCode);
        var id = (await invite.Content.ReadFromJsonAsync<InvitationDto>(GratiaApiFactory.Json))!.Id;

        // Tried 5 times, each after the wait the last failure set, and not before it
        Task<int> AttemptsAsync() => factory.WithDbAsync(db => db.CompanyInvitations.Where(i => i.Id == id).Select(i => i.EmailAttempts).SingleAsync());
        var now = DateTime.UtcNow;
        await factory.SendInvitationEmailsAsync(now);
        Assert.Equal(1, await AttemptsAsync());
        foreach (var (wait, attempts) in new[] { (1, 2), (5, 3), (30, 4), (120, 5) })
        {
            await factory.SendInvitationEmailsAsync(now.AddMinutes(wait - 0.5));
            Assert.Equal(attempts - 1, await AttemptsAsync());
            Assert.Equal(InvitationEmailStatus.Pending, (await PendingAsync(admin)).Single(i => i.Id == id).EmailStatus);
            now = now.AddMinutes(wait);
            await factory.SendInvitationEmailsAsync(now);
            Assert.Equal(attempts, await AttemptsAsync());
        }
        Assert.Equal(InvitationEmailStatus.Failed, (await PendingAsync(admin)).Single(i => i.Id == id).EmailStatus);
        await factory.SendInvitationEmailsAsync(now.AddDays(1));
        Assert.Equal(5, await AttemptsAsync()); // given up on until it's resent

        // Inviting again says what to do; Resend sends it
        var again = await admin.PostAsJsonAsync("/api/invitation", new { email });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Contains("couldn't be sent. Resend it", await again.Content.ReadAsStringAsync());

        factory.Emails.StopFailingFor(email);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/invitation/{id}/resend", null)).StatusCode);
        await factory.SendInvitationEmailsAsync();
        Assert.Equal(InvitationEmailStatus.Sent, (await PendingAsync(admin)).Single(i => i.Id == id).EmailStatus);
        Assert.Single(factory.Emails.SentTo(email));
        var lookup = await factory.CreateClient().GetFromJsonAsync<InvitationDto>($"/api/invitation/token/{factory.Emails.LastTokenFor(email)}",
            GratiaApiFactory.Json);
        Assert.True(lookup!.IsValid);
    }

    [Fact]
    public async Task When_a_resent_email_fails_the_link_already_sent_keeps_working()
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        var (id, email) = await InviteAsync(admin);
        var token = factory.Emails.LastTokenFor(email);
        factory.Emails.FailFor(email);

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/invitation/{id}/resend", null)).StatusCode);
        await factory.SendInvitationEmailsAsync();
        Assert.Single(factory.Emails.SentTo(email));

        // #20: the new link only replaces the old one once its email has gone out
        var lookup = await factory.CreateClient().GetFromJsonAsync<InvitationDto>($"/api/invitation/token/{token}", GratiaApiFactory.Json);
        Assert.True(lookup!.IsValid);

        factory.Emails.StopFailingFor(email);
        await factory.SendInvitationEmailsAsync(DateTime.UtcNow.AddMinutes(1));
        Assert.Equal(2, factory.Emails.SentTo(email).Count);
        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().GetAsync($"/api/invitation/token/{token}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().GetAsync($"/api/invitation/token/{factory.Emails.LastTokenFor(email)}")).StatusCode);
    }

    [Fact]
    public async Task An_invitation_cancelled_before_its_email_goes_out_sends_nothing()
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        var email = NewEmail();
        var invite = await admin.PostAsJsonAsync("/api/invitation", new { email });
        var id = (await invite.Content.ReadFromJsonAsync<InvitationDto>(GratiaApiFactory.Json))!.Id;

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/invitation/{id}")).StatusCode);
        await factory.SendInvitationEmailsAsync();

        Assert.Empty(factory.Emails.SentTo(email));
    }

    [Fact]
    public async Task An_email_is_sent_by_one_sender_even_when_two_run_at_once()
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        var email = NewEmail();
        var invite = await admin.PostAsJsonAsync("/api/invitation", new { email });
        var id = (await invite.Content.ReadFromJsonAsync<InvitationDto>(GratiaApiFactory.Json))!.Id;

        // Two API instances' senders: whichever claims the email first sends it, until its lease ends
        var now = DateTime.UtcNow;
        await using var first = factory.CreateDbContextWithNoCaller();
        await using var second = factory.CreateDbContextWithNoCaller();
        Assert.True(await new InvitationRepository(first).TryClaimEmailAsync(id, now, now + SendInvitationEmailsCommandHandler.Lease));
        Assert.False(await new InvitationRepository(second).TryClaimEmailAsync(id, now, now + SendInvitationEmailsCommandHandler.Lease));
        await factory.SendInvitationEmailsAsync(now);
        Assert.Empty(factory.Emails.SentTo(email));

        // A sender that stopped halfway: once its lease is over, another one sends the email
        await factory.SendInvitationEmailsAsync(now + SendInvitationEmailsCommandHandler.Lease);
        Assert.Single(factory.Emails.SentTo(email));
    }

    [Fact]
    public async Task A_resend_while_the_email_is_on_its_way_is_not_lost()
    {
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        var email = NewEmail();
        var invite = await admin.PostAsJsonAsync("/api/invitation", new { email });
        var id = (await invite.Content.ReadFromJsonAsync<InvitationDto>(GratiaApiFactory.Json))!.Id;
        var firstToken = await factory.WithDbAsync(db => db.CompanyInvitations.Where(i => i.Id == id).Select(i => i.InvitationToken).SingleAsync());

        // A sender has claimed the first email and is sending it when the admin clicks Resend
        var now = DateTime.UtcNow;
        await using var sender = factory.CreateDbContextWithNoCaller();
        var senderRepository = new InvitationRepository(sender);
        Assert.True(await senderRepository.TryClaimEmailAsync(id, now, now + SendInvitationEmailsCommandHandler.Lease));
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/invitation/{id}/resend", null)).StatusCode);

        // The first email's success doesn't count for the resend: its newer link still has to go out
        Assert.False(await senderRepository.MarkEmailSentAsync(id, firstToken, now, now.AddDays(7)));
        Assert.Equal(InvitationEmailStatus.Pending, (await PendingAsync(admin)).Single(i => i.Id == id).EmailStatus);

        await factory.SendInvitationEmailsAsync(now.AddSeconds(1));
        var newToken = factory.Emails.LastTokenFor(email);
        Assert.NotEqual(firstToken, newToken);
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().GetAsync($"/api/invitation/token/{newToken}")).StatusCode);
        Assert.Equal(InvitationEmailStatus.Sent, (await PendingAsync(admin)).Single(i => i.Id == id).EmailStatus);
    }

    // The seeded companies are unverified, so their admins can only invite addresses on their own domain
    private static string NewEmail(string domain = "acme.test") => $"{Guid.NewGuid():N}@{domain}";

    // Invites and lets the email go out, as the background sender would (#55)
    private async Task<(Guid Id, string Email)> InviteAsync(HttpClient admin, string domain = "acme.test")
    {
        var email = NewEmail(domain);
        var response = await admin.PostAsJsonAsync("/api/invitation", new { email });
        response.EnsureSuccessStatusCode();
        var invitation = await response.Content.ReadFromJsonAsync<InvitationDto>(GratiaApiFactory.Json);
        await factory.SendInvitationEmailsAsync();
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
