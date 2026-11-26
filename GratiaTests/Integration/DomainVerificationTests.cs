using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Gratia.Application.DTOs.CompanyDTO;
using Gratia.Application.DTOs.Invitation;
using Gratia.Application.DTOs.Token;
using Gratia.Application.Interfaces;
using Gratia.Domain.Entities;
using Gratia.Domain.Enum;
using Microsoft.EntityFrameworkCore;

namespace GratiaTests.Integration;

// Domain verification and claiming (#61). Each test has a company of its own, so it knows who its admins are and what's
// verified. The DNS is FakeDns: a test "adds" a record the way the domain's managers would.
public sealed class DomainVerificationTests(GratiaApiFactory factory) : IClassFixture<GratiaApiFactory>
{
    private const string VerificationUrl = "/api/company/domain-verification";
    private const string CheckUrl = "/api/company/domain-verification/check";

    [Fact]
    public async Task An_admin_verifies_the_domain_with_their_own_record_and_the_limits_are_lifted()
    {
        var (company, adminEmail) = await CompanyWithAdminAsync();
        var (_, otherAdminEmail) = await factory.AddUserAsync(company, role: UserRole.Admin);
        var admin = await factory.CreateClientForAsync(adminEmail);

        var before = await GetAsync<DomainVerificationDto>(admin, VerificationUrl);
        Assert.Equal((company.Domain, false, true, (DnsRecordDto?)null), (before.Domain, before.IsVerified, before.IsAdmin, before.Record));

        var started = await PostAsync<DomainVerificationDto>(admin, VerificationUrl);
        var record = started.Record!;
        Assert.Equal(("TXT", company.Domain), (record.Type, record.Name));
        Assert.Matches("^gratia-verify=[0-9a-f]{32}$", record.Value);
        // The same record every time, so one already added keeps working
        Assert.Equal(record.Value, (await PostAsync<DomainVerificationDto>(admin, VerificationUrl)).Record!.Value);
        Assert.Equal(record.Value, (await GetAsync<DomainVerificationDto>(admin, VerificationUrl)).Record!.Value);

        // Not there yet: a normal answer, and nothing changes
        var early = await PostAsync<DomainCheckResultDto>(admin, CheckUrl);
        Assert.Equal(DomainCheckStatus.NotFound, early.Status);
        Assert.Contains("isn't on", early.Message);
        Assert.False(await IsVerifiedAsync(company));

        factory.Dns.AddTxt(company.Domain!, "v=spf1 -all");
        factory.Dns.AddTxt(company.Domain!, record.Value);
        var check = await PostAsync<DomainCheckResultDto>(admin, CheckUrl);

        Assert.Equal(DomainCheckStatus.Verified, check.Status);
        Assert.Null(check.InvitationToken);
        Assert.True(await IsVerifiedAsync(company));
        var notice = Assert.Single(factory.Emails.SentTo(otherAdminEmail));
        Assert.Equal((EmailKind.DomainVerified, adminEmail, DomainVerifiedBy.Admin), (notice.Kind, notice.NewEmail, notice.By));
        Assert.Empty(factory.Emails.SentTo(adminEmail));

        // No more domain rule: contractors on other domains can be invited
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/invitation", new { email = $"{Guid.NewGuid():N}@contractor.test" })).StatusCode);
        // But a member's address still only changes to one on the domain, or the 409 for a taken address would tell an admin
        // whether any address has an account
        var (memberId, _) = await factory.AddUserAsync(company);
        var move = await admin.PutAsJsonAsync($"/api/user/{memberId}",
            new { id = memberId, fullName = "Temp User", email = factory.GlobexAdminEmail, jobTitle = "Tester", role = "Employee" });
        Assert.Equal(HttpStatusCode.BadRequest, move.StatusCode);
        Assert.Contains("invite them", await move.Content.ReadAsStringAsync());

        // Done: nothing left to start, and checking again changes nothing
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync(VerificationUrl, null)).StatusCode);
        Assert.Equal(DomainCheckStatus.Verified, (await PostAsync<DomainCheckResultDto>(admin, CheckUrl)).Status);
        Assert.Single(factory.Emails.SentTo(otherAdminEmail));
    }

    [Fact]
    public async Task A_member_who_manages_the_domain_becomes_an_admin_even_once_it_is_verified()
    {
        // The way back in if every admin leaves (#63): verified or not, whoever manages the domain can become an admin
        var (company, adminEmail) = await CompanyWithAdminAsync();
        await factory.WithDbAsync(db => db.Companies.Where(c => c.Id == company.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.DomainVerifiedAt, DateTime.UtcNow.AddDays(-30))));
        var (memberId, memberEmail) = await factory.AddUserAsync(company);
        var member = await factory.CreateClientForAsync(memberEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/invitation/pending")).StatusCode);

        var record = (await PostAsync<DomainVerificationDto>(member, VerificationUrl)).Record!;
        factory.Dns.AddTxt(company.Domain!, record.Value);
        var check = await PostAsync<DomainCheckResultDto>(member, CheckUrl);

        Assert.Equal(DomainCheckStatus.Verified, check.Status);
        Assert.Contains("you're now an admin", check.Message);
        Assert.Equal(UserRole.Admin, await factory.WithDbAsync(db => db.Users.Where(u => u.Id == memberId).Select(u => u.Role).SingleAsync()));
        // The old access token says Employee, so it stops working at once (#73); signing in again brings the admin role
        Assert.Equal(HttpStatusCode.Unauthorized, (await member.GetAsync("/api/user/me")).StatusCode);
        var asAdmin = await factory.CreateClientForAsync(memberEmail);
        Assert.Equal(HttpStatusCode.OK, (await asAdmin.GetAsync("/api/invitation/pending")).StatusCode);

        // The admin who was there stays one, and hears of it
        Assert.Equal(UserRole.Admin, await factory.WithDbAsync(db => db.Users.Where(u => u.Email == adminEmail).Select(u => u.Role).SingleAsync()));
        var notice = Assert.Single(factory.Emails.SentTo(adminEmail));
        Assert.Equal((EmailKind.DomainVerified, memberEmail, DomainVerifiedBy.MemberMadeAdmin), (notice.Kind, notice.NewEmail, notice.By));
    }

    [Fact]
    public async Task Each_member_verifies_only_their_own_workspace_with_their_own_record()
    {
        var (company, adminEmail) = await CompanyWithAdminAsync();
        var (otherCompany, otherAdminEmail) = await CompanyWithAdminAsync();
        var (_, memberEmail) = await factory.AddUserAsync(company);
        var admin = await factory.CreateClientForAsync(adminEmail);
        var member = await factory.CreateClientForAsync(memberEmail);
        var otherAdmin = await factory.CreateClientForAsync(otherAdminEmail);

        // The domain's managers add the admin's record
        var adminRecord = (await PostAsync<DomainVerificationDto>(admin, VerificationUrl)).Record!;
        factory.Dns.AddTxt(company.Domain!, adminRecord.Value);

        // A member can't finish with it: their own record is another, so nobody rides on a record added for someone else
        var memberRecord = (await PostAsync<DomainVerificationDto>(member, VerificationUrl)).Record!;
        Assert.NotEqual(adminRecord.Value, memberRecord.Value);
        Assert.Equal(DomainCheckStatus.NotFound, (await PostAsync<DomainCheckResultDto>(member, CheckUrl)).Status);
        Assert.Equal(UserRole.Employee, await factory.WithDbAsync(db => db.Users.Where(u => u.Email == memberEmail).Select(u => u.Role).SingleAsync()));

        // Another company's admin sees their own workspace only, and the record on this domain does nothing for them
        var theirs = await GetAsync<DomainVerificationDto>(otherAdmin, VerificationUrl);
        Assert.Equal((otherCompany.Domain, (DnsRecordDto?)null), (theirs.Domain, theirs.Record));
        var theirRecord = (await PostAsync<DomainVerificationDto>(otherAdmin, VerificationUrl)).Record!;
        Assert.Equal(otherCompany.Domain, theirRecord.Name);
        Assert.Equal(DomainCheckStatus.NotFound, (await PostAsync<DomainCheckResultDto>(otherAdmin, CheckUrl)).Status);
        Assert.False(await IsVerifiedAsync(otherCompany));
        Assert.False(await IsVerifiedAsync(company));
        Assert.Equal([company.Id, company.Id, otherCompany.Id], await factory.WithDbAsync(db => db.DomainVerifications
            .Where(v => v.Email == adminEmail || v.Email == memberEmail || v.Email == otherAdminEmail)
            .OrderBy(v => v.Email == otherAdminEmail).Select(v => v.CompanyId).ToListAsync()));

        Assert.Equal(DomainCheckStatus.Verified, (await PostAsync<DomainCheckResultDto>(admin, CheckUrl)).Status);
        Assert.False(await IsVerifiedAsync(otherCompany));
    }

    [Fact]
    public async Task Checking_needs_a_record_and_a_failed_lookup_says_to_try_again()
    {
        var (company, adminEmail) = await CompanyWithAdminAsync();
        var admin = await factory.CreateClientForAsync(adminEmail);

        var tooSoon = await admin.PostAsync(CheckUrl, null);
        Assert.Equal(HttpStatusCode.BadRequest, tooSoon.StatusCode);
        Assert.Contains("Get your record first", await tooSoon.Content.ReadAsStringAsync());

        var record = (await PostAsync<DomainVerificationDto>(admin, VerificationUrl)).Record!;
        factory.Dns.AddTxt(company.Domain!, record.Value);
        factory.Dns.FailFor(company.Domain!);
        var check = await PostAsync<DomainCheckResultDto>(admin, CheckUrl);

        Assert.Equal(DomainCheckStatus.LookupFailed, check.Status);
        Assert.Contains("didn't answer", check.Message);
        Assert.False(await IsVerifiedAsync(company));
    }

    [Fact]
    public async Task A_workspace_without_a_domain_has_nothing_to_verify()
    {
        // Founded with a personal address (Development only)
        var (company, adminEmail) = await CompanyWithAdminAsync();
        await factory.WithDbAsync(db => db.Companies.Where(c => c.Id == company.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.Domain, (string?)null)));
        var admin = await factory.CreateClientForAsync(adminEmail);

        Assert.Null((await GetAsync<DomainVerificationDto>(admin, VerificationUrl)).Domain);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync(VerificationUrl, null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync(CheckUrl, null)).StatusCode);
    }

    [Fact]
    public async Task IT_claims_a_workspace_someone_else_set_up()
    {
        var (company, founderEmail) = await CompanyWithAdminAsync();
        var (otherCompany, otherAdminEmail) = await CompanyWithAdminAsync();
        var it = $"it@{company.Domain}";

        // Signing up finds the workspace, and the notice links to claiming it
        var claimToken = await SignupForClaimAsync(it);
        var anonymous = factory.CreateClient();
        var claim = await anonymous.GetFromJsonAsync<DomainClaimDto>($"/api/auth/domain-claim/{claimToken}", GratiaApiFactory.Json);
        Assert.Equal((company.Name, company.Domain, it, false), (claim!.CompanyName, claim.Domain, claim.Email, claim.IsVerified));
        Assert.Matches("^gratia-verify=[0-9a-f]{32}$", claim.Record.Value);
        Assert.InRange(claim.ExpiresAt, DateTime.UtcNow.AddDays(6.9), DateTime.UtcNow.AddDays(7));

        Assert.Equal(DomainCheckStatus.NotFound, (await CheckClaimAsync(claimToken)).Status);

        // IT adds the record to the domain
        factory.Dns.AddTxt(company.Domain!, claim.Record.Value);
        var check = await CheckClaimAsync(claimToken);

        Assert.Equal(DomainCheckStatus.Verified, check.Status);
        Assert.True(await IsVerifiedAsync(company));
        var invitation = await anonymous.GetFromJsonAsync<InvitationDto>($"/api/invitation/token/{check.InvitationToken}", GratiaApiFactory.Json);
        Assert.Equal((it, UserRole.Admin, company.Name), (invitation!.Email, invitation.InvitedRole, invitation.CompanyName));
        // Checking again gives the same invitation, and tells nobody twice
        Assert.Equal(check.InvitationToken, (await CheckClaimAsync(claimToken)).InvitationToken);

        var accept = await anonymous.PostAsJsonAsync("/api/invitation/accept",
            new { token = check.InvitationToken, fullName = "Ivy Tech", password = GratiaApiFactory.Password });
        var tokens = (await accept.Content.ReadFromJsonAsync<TokenResponseDto>())!;
        var itAdmin = factory.CreateClient();
        itAdmin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await itAdmin.GetAsync("/api/invitation/pending")).StatusCode);

        // Nothing merged or removed: the founder is still an admin, and heard of it once
        Assert.Equal(UserRole.Admin, await factory.WithDbAsync(db => db.Users.Where(u => u.Email == founderEmail).Select(u => u.Role).SingleAsync()));
        var notice = Assert.Single(factory.Emails.SentTo(founderEmail));
        Assert.Equal((EmailKind.DomainVerified, it, DomainVerifiedBy.InvitedAsAdmin), (notice.Kind, notice.NewEmail, notice.By));

        // The link only ever claims its own workspace
        Assert.Equal(company.Id, await factory.WithDbAsync(db => db.Users.Where(u => u.Email == it).Select(u => u.CompanyId).SingleAsync()));
        Assert.False(await IsVerifiedAsync(otherCompany));
        Assert.Empty(factory.Emails.SentTo(otherAdminEmail));
    }

    [Fact]
    public async Task A_claimants_pending_invitation_becomes_an_admin_one()
    {
        var (company, adminEmail) = await CompanyWithAdminAsync();
        var claimant = $"{Guid.NewGuid():N}@{company.Domain}";
        var admin = await factory.CreateClientForAsync(adminEmail);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/invitation", new { email = claimant })).StatusCode);

        var claimToken = await SignupForClaimAsync(claimant);
        var record = (await factory.CreateClient().GetFromJsonAsync<DomainClaimDto>($"/api/auth/domain-claim/{claimToken}", GratiaApiFactory.Json))!.Record;
        factory.Dns.AddTxt(company.Domain!, record.Value);
        var check = await CheckClaimAsync(claimToken);

        Assert.Equal(DomainCheckStatus.Verified, check.Status);
        var invitations = await factory.WithDbAsync(db => db.CompanyInvitations.Where(i => i.Email == claimant).ToListAsync());
        var invitation = Assert.Single(invitations);
        Assert.Equal((UserRole.Admin, check.InvitationToken), (invitation.InvitedRole, invitation.InvitationToken));
    }

    [Fact]
    public async Task Signing_up_again_sends_a_new_claim_link_with_the_same_record()
    {
        var (company, _) = await CompanyWithAdminAsync();
        var claimant = $"{Guid.NewGuid():N}@{company.Domain}";
        var anonymous = factory.CreateClient();

        var first = await SignupForClaimAsync(claimant);
        var record = (await anonymous.GetFromJsonAsync<DomainClaimDto>($"/api/auth/domain-claim/{first}", GratiaApiFactory.Json))!.Record;
        var second = await SignupForClaimAsync(claimant);

        Assert.NotEqual(first, second);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/auth/domain-claim/{first}")).StatusCode);
        Assert.Equal(record.Value,
            (await anonymous.GetFromJsonAsync<DomainClaimDto>($"/api/auth/domain-claim/{second}", GratiaApiFactory.Json))!.Record.Value);
    }

    [Fact]
    public async Task If_the_notice_fails_the_last_claim_link_keeps_working()
    {
        var (company, _) = await CompanyWithAdminAsync();
        var claimant = $"{Guid.NewGuid():N}@{company.Domain}";
        var first = await SignupForClaimAsync(claimant);

        factory.Emails.FailFor(claimant);
        var failed = await factory.CreateClient().PostAsJsonAsync("/api/auth/register-company", new { companyName = "X", fullName = "Y", email = claimant });
        factory.Emails.StopFailingFor(claimant);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().GetAsync($"/api/auth/domain-claim/{first}")).StatusCode);
    }

    [Fact]
    public async Task An_unknown_or_expired_claim_link_is_not_found()
    {
        var (company, _) = await CompanyWithAdminAsync();
        var claimant = $"{Guid.NewGuid():N}@{company.Domain}";
        var token = await SignupForClaimAsync(claimant);
        var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/auth/domain-claim/not-a-token")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await anonymous.PostAsJsonAsync("/api/auth/domain-claim/check", new { token = "not-a-token" })).StatusCode);

        await factory.WithDbAsync(db => db.DomainVerifications.Where(v => v.Email == claimant)
            .ExecuteUpdateAsync(setters => setters.SetProperty(v => v.LinkExpiresAt, DateTime.UtcNow.AddMinutes(-1))));
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/auth/domain-claim/{token}")).StatusCode);
        factory.Dns.AddTxt(company.Domain!, await RecordValueAsync(claimant));
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.PostAsJsonAsync("/api/auth/domain-claim/check", new { token })).StatusCode);
        Assert.False(await IsVerifiedAsync(company));
    }

    [Fact]
    public async Task A_claim_whose_address_got_an_account_meanwhile_makes_that_account_an_admin_or_is_refused()
    {
        // A claim link goes only to an address without an account, but one may have come since (an invitation accepted)
        var (company, _) = await CompanyWithAdminAsync();
        var member = await ClaimThenAccountAsync(company, company);
        var deactivated = await ClaimThenAccountAsync(company, company, deactivate: true);
        var elsewhere = await ClaimThenAccountAsync(company, factory.Globex);

        var promoted = await CheckClaimAsync(member.Token);
        Assert.Equal(DomainCheckStatus.Verified, promoted.Status);
        Assert.Null(promoted.InvitationToken);
        Assert.Contains("Sign in", promoted.Message);
        Assert.Equal(UserRole.Admin, await factory.WithDbAsync(db => db.Users.Where(u => u.Email == member.Email).Select(u => u.Role).SingleAsync()));

        // Like every way in without an access token (#16)
        var refused = await factory.CreateClient().PostAsJsonAsync("/api/auth/domain-claim/check", new { token = deactivated.Token });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("deactivated", await refused.Content.ReadAsStringAsync());
        Assert.Equal(UserRole.Employee, await factory.WithDbAsync(db => db.Users.Where(u => u.Email == deactivated.Email).Select(u => u.Role).SingleAsync()));

        var taken = await factory.CreateClient().PostAsJsonAsync("/api/auth/domain-claim/check", new { token = elsewhere.Token });
        Assert.Equal(HttpStatusCode.BadRequest, taken.StatusCode);
        Assert.Contains("another Gratia workspace", await taken.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_record_works_once_so_a_demoted_person_cannot_use_it_again()
    {
        // Records tend to stay in the DNS. An old one shows that someone managed the domain then, not now.
        var (company, adminEmail) = await CompanyWithAdminAsync();
        var (memberId, memberEmail) = await factory.AddUserAsync(company);
        var member = await factory.CreateClientForAsync(memberEmail);
        var record = (await PostAsync<DomainVerificationDto>(member, VerificationUrl)).Record!;
        factory.Dns.AddTxt(company.Domain!, record.Value);
        Assert.Equal(DomainCheckStatus.Verified, (await PostAsync<DomainCheckResultDto>(member, CheckUrl)).Status);

        await DemoteAsync(adminEmail, memberId, memberEmail);
        member = await factory.CreateClientForAsync(memberEmail);

        Assert.Equal(HttpStatusCode.BadRequest, (await member.PostAsync(CheckUrl, null)).StatusCode);
        Assert.Equal(UserRole.Employee, await RoleAsync(memberEmail));
        Assert.Null((await GetAsync<DomainVerificationDto>(member, VerificationUrl)).Record);

        // Still the way back in for whoever manages the domain now: it takes a new record
        var renewed = (await PostAsync<DomainVerificationDto>(member, VerificationUrl)).Record!;
        Assert.NotEqual(record.Value, renewed.Value);
        Assert.Equal(DomainCheckStatus.NotFound, (await PostAsync<DomainCheckResultDto>(member, CheckUrl)).Status);
        factory.Dns.AddTxt(company.Domain!, renewed.Value);
        Assert.Equal(DomainCheckStatus.Verified, (await PostAsync<DomainCheckResultDto>(member, CheckUrl)).Status);
        Assert.Equal(UserRole.Admin, await RoleAsync(memberEmail));
    }

    [Fact]
    public async Task A_claim_works_once_so_a_demoted_claimant_cannot_use_its_link_again()
    {
        var (company, adminEmail) = await CompanyWithAdminAsync();
        var claimant = $"{Guid.NewGuid():N}@{company.Domain}";
        var token = await SignupForClaimAsync(claimant);
        factory.Dns.AddTxt(company.Domain!, await RecordValueAsync(claimant));
        var invitationToken = (await CheckClaimAsync(token)).InvitationToken;
        var accept = await factory.CreateClient().PostAsJsonAsync("/api/invitation/accept",
            new { token = invitationToken, fullName = "Ivy Tech", password = GratiaApiFactory.Password });
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        await DemoteAsync(adminEmail, await factory.UserIdAsync(claimant), claimant);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await factory.CreateClient().PostAsJsonAsync("/api/auth/domain-claim/check", new { token })).StatusCode);
        Assert.Equal(UserRole.Employee, await RoleAsync(claimant));
    }

    [Fact]
    public async Task An_admin_cannot_use_a_colleagues_record_by_taking_their_address()
    {
        // Admins change members' addresses, their own included, without the new mailbox confirming it
        var (company, adminEmail) = await CompanyWithAdminAsync();
        var adminId = await factory.UserIdAsync(adminEmail);
        var it = $"it@{company.Domain}";
        await SignupForClaimAsync(it);
        factory.Dns.AddTxt(company.Domain!, await RecordValueAsync(it)); // IT's record is on the domain, for IT

        var admin = await factory.CreateClientForAsync(adminEmail);
        var taken = await admin.PutAsJsonAsync($"/api/user/{adminId}",
            new { id = adminId, fullName = "Temp User", email = it, jobTitle = "Tester", role = "Admin" });
        Assert.Equal(HttpStatusCode.OK, taken.StatusCode);
        var disguised = await factory.CreateClientForAsync(it);

        Assert.Equal(HttpStatusCode.BadRequest, (await disguised.PostAsync(CheckUrl, null)).StatusCode);
        Assert.Null((await GetAsync<DomainVerificationDto>(disguised, VerificationUrl)).Record);
        await PostAsync<DomainVerificationDto>(disguised, VerificationUrl);
        Assert.Equal(DomainCheckStatus.NotFound, (await PostAsync<DomainCheckResultDto>(disguised, CheckUrl)).Status);
        Assert.False(await IsVerifiedAsync(company));
    }

    [Fact]
    public async Task Changing_a_members_address_voids_their_record()
    {
        // Or an admin could point the account at a mailbox of their own, reset its password, and finish with its record
        var (company, adminEmail) = await CompanyWithAdminAsync();
        var (memberId, memberEmail) = await factory.AddUserAsync(company);
        var member = await factory.CreateClientForAsync(memberEmail);
        var record = (await PostAsync<DomainVerificationDto>(member, VerificationUrl)).Record!;
        factory.Dns.AddTxt(company.Domain!, record.Value);

        var admin = await factory.CreateClientForAsync(adminEmail);
        foreach (var email in new[] { $"{Guid.NewGuid():N}@{company.Domain}", memberEmail }) // there and back
            (await admin.PutAsJsonAsync($"/api/user/{memberId}",
                new { id = memberId, fullName = "Temp User", email, jobTitle = "Tester", role = "Employee" })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.BadRequest, (await member.PostAsync(CheckUrl, null)).StatusCode);
        Assert.Equal(UserRole.Employee, await RoleAsync(memberEmail));
        Assert.False(await IsVerifiedAsync(company));
    }

    [Fact]
    public async Task Two_checks_of_one_claim_at_the_same_moment_grant_one_invitation()
    {
        var (company, adminEmail) = await CompanyWithAdminAsync();
        var claimant = $"{Guid.NewGuid():N}@{company.Domain}";
        var token = await SignupForClaimAsync(claimant);
        factory.Dns.AddTxt(company.Domain!, await RecordValueAsync(claimant));
        DomainCheckResultDto? faster = null;
        factory.Dns.DuringNextLookup(company.Domain!, async () => faster = await CheckClaimAsync(token));

        var slower = await CheckClaimAsync(token);

        Assert.Equal((DomainCheckStatus.Verified, faster!.InvitationToken), (slower.Status, slower.InvitationToken));
        Assert.Equal(1, await factory.WithDbAsync(db => db.CompanyInvitations.CountAsync(i => i.Email == claimant)));
        Assert.Single(factory.Emails.SentTo(adminEmail)); // told once
    }

    [Fact]
    public async Task Two_checks_by_one_member_at_the_same_moment_count_once()
    {
        var (company, adminEmail) = await CompanyWithAdminAsync();
        var (_, memberEmail) = await factory.AddUserAsync(company);
        var member = await factory.CreateClientForAsync(memberEmail);
        factory.Dns.AddTxt(company.Domain!, (await PostAsync<DomainVerificationDto>(member, VerificationUrl)).Record!.Value);
        DomainCheckResultDto? faster = null;
        factory.Dns.DuringNextLookup(company.Domain!, async () => faster = await PostAsync<DomainCheckResultDto>(member, CheckUrl));

        var slower = await PostAsync<DomainCheckResultDto>(member, CheckUrl);

        Assert.Equal((DomainCheckStatus.Verified, DomainCheckStatus.Verified), (faster!.Status, slower.Status));
        Assert.Single(factory.Emails.SentTo(adminEmail));
    }

    [Fact]
    public async Task A_member_deactivated_during_the_lookup_is_not_made_an_admin_and_nothing_is_verified()
    {
        var (company, adminEmail) = await CompanyWithAdminAsync();
        var admin = await factory.CreateClientForAsync(adminEmail);
        var (memberId, memberEmail) = await factory.AddUserAsync(company);
        var member = await factory.CreateClientForAsync(memberEmail);
        factory.Dns.AddTxt(company.Domain!, (await PostAsync<DomainVerificationDto>(member, VerificationUrl)).Record!.Value);
        factory.Dns.DuringNextLookup(company.Domain!, async () =>
            (await admin.PostAsync($"/api/user/{memberId}/deactivate", null)).EnsureSuccessStatusCode());

        Assert.Equal(HttpStatusCode.NotFound, (await member.PostAsync(CheckUrl, null)).StatusCode);

        Assert.Equal(UserRole.Employee, await RoleAsync(memberEmail));
        Assert.False(await IsVerifiedAsync(company));
        // Rolled back together
        Assert.False(await factory.WithDbAsync(db => db.DomainVerifications.AnyAsync(v => v.UserId == memberId && v.VerifiedAt != null)));
    }

    [Fact]
    public async Task Signing_up_again_after_a_claim_was_used_gives_a_new_record()
    {
        var (company, _) = await CompanyWithAdminAsync();
        var claimant = $"{Guid.NewGuid():N}@{company.Domain}";
        var token = await SignupForClaimAsync(claimant);
        var anonymous = factory.CreateClient();
        var used = await RecordValueAsync(claimant);
        factory.Dns.AddTxt(company.Domain!, used);
        await CheckClaimAsync(token);
        Assert.True((await anonymous.GetFromJsonAsync<DomainClaimDto>($"/api/auth/domain-claim/{token}", GratiaApiFactory.Json))!.IsUsed);

        // Their invitation expires unused
        await factory.WithDbAsync(db => db.CompanyInvitations.Where(i => i.Email == claimant)
            .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.ExpiresAt, DateTime.UtcNow.AddMinutes(-1))));
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsJsonAsync("/api/auth/domain-claim/check", new { token })).StatusCode);

        var again = await SignupForClaimAsync(claimant);
        var claim = (await anonymous.GetFromJsonAsync<DomainClaimDto>($"/api/auth/domain-claim/{again}", GratiaApiFactory.Json))!;
        Assert.False(claim.IsUsed);
        Assert.NotEqual(used, claim.Record.Value);
        Assert.Equal(DomainCheckStatus.NotFound, (await CheckClaimAsync(again)).Status); // the old record doesn't count
    }

    private async Task DemoteAsync(string adminEmail, Guid userId, string email)
    {
        var admin = await factory.CreateClientForAsync(adminEmail);
        (await admin.PutAsJsonAsync($"/api/user/{userId}",
            new { id = userId, fullName = "Temp User", email, jobTitle = "Tester", role = "Employee" })).EnsureSuccessStatusCode();
    }

    private Task<UserRole> RoleAsync(string email) =>
        factory.WithDbAsync(db => db.Users.Where(u => u.Email == email).Select(u => u.Role).SingleAsync());

    // A company of its own with one admin, so a test knows exactly who hears of a verification
    private async Task<(Company Company, string AdminEmail)> CompanyWithAdminAsync()
    {
        var company = await factory.AddCompanyAsync();
        var (_, email) = await factory.AddUserAsync(company, role: UserRole.Admin);
        return (company, email);
    }

    // Signs up with an address on a domain whose workspace exists, and returns the claim link's token from the notice
    private async Task<string> SignupForClaimAsync(string email)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register-company", new { companyName = "Mine", fullName = "Ivy Tech", email });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var notice = factory.Emails.SentTo(email).Last();
        Assert.Equal(EmailKind.WorkspaceExists, notice.Kind);
        return notice.Token!;
    }

    // A claim link, its record on the domain, and then an account with the address in accountCompany
    private async Task<(string Email, string Token)> ClaimThenAccountAsync(Company company, Company accountCompany, bool deactivate = false)
    {
        var email = $"{Guid.NewGuid():N}@{company.Domain}";
        var token = await SignupForClaimAsync(email);
        factory.Dns.AddTxt(company.Domain!, await RecordValueAsync(email));
        var (id, _) = await factory.AddUserAsync(accountCompany);
        await factory.WithDbAsync(db => db.Users.Where(u => u.Id == id).ExecuteUpdateAsync(setters => setters
            .SetProperty(u => u.Email, email)
            .SetProperty(u => u.DeactivatedAt, deactivate ? DateTime.UtcNow : null)));
        return (email, token);
    }

    private Task<string> RecordValueAsync(string email) =>
        factory.WithDbAsync(db => db.DomainVerifications.Where(v => v.Email == email && v.UserId == null)
            .Select(v => DomainVerification.RecordPrefix + v.RecordToken).SingleAsync());

    private async Task<DomainCheckResultDto> CheckClaimAsync(string token)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/domain-claim/check", new { token });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DomainCheckResultDto>(GratiaApiFactory.Json))!;
    }

    private Task<bool> IsVerifiedAsync(Company company) =>
        factory.WithDbAsync(db => db.Companies.Where(c => c.Id == company.Id).Select(c => c.DomainVerifiedAt != null).SingleAsync());

    private static async Task<T> GetAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(GratiaApiFactory.Json))!;
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string url)
    {
        var response = await client.PostAsync(url, null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(GratiaApiFactory.Json))!;
    }
}
