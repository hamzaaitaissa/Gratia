using Gratia.Domain.Common;
using Gratia.Domain.Entities;
using Gratia.Domain.Enum;
using Gratia.Infrastructure.Data;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GratiaTests.Integration;

// #78: the tenant filter behind each query's own company check (#22), and the lists that make every new endpoint and
// entity a deliberate decision. #79: the database's own keys that keep a recognition or invitation in one company.
public sealed class TenantIsolationTests(GratiaApiFactory factory) : IClassFixture<GratiaApiFactory>
{
    // Every endpoint, and the test showing that another company's data stays out of its reach and looks the same as data
    // that doesn't exist. A new endpoint fails Every_endpoint_has_a_cross_company_test until it has a line here, naming a
    // test written for it.
    private static readonly Dictionary<string, string> CrossCompanyTests = new()
    {
        // No caller yet: these find the account, invitation or session by what only its owner has (a password, a token
        // from an email), or give the same answer whatever exists
        ["POST api/Auth/register-company"] = nameof(OnboardingTests.A_second_signup_for_a_domain_with_a_workspace_only_emails_a_notice),
        ["POST api/Auth/login"] = nameof(AuthEndpointsTests.A_session_belongs_to_its_users_company),
        ["POST api/Auth/refresh-token"] = nameof(AuthEndpointsTests.A_session_belongs_to_its_users_company),
        ["POST api/Auth/revoke-token"] = nameof(AuthEndpointsTests.A_session_belongs_to_its_users_company),
        ["POST api/Auth/forgot-password"] = nameof(AccountEndpointsTests.Forgot_password_answers_the_same_for_an_email_without_an_active_account),
        ["POST api/Auth/reset-password"] = nameof(AccountEndpointsTests.A_forgotten_password_is_reset_once_with_the_emailed_link),
        ["GET api/Invitation/token/{token}"] = nameof(InvitationEndpointsTests.An_unknown_token_is_not_found),
        ["POST api/Invitation/accept"] = nameof(InvitationEndpointsTests.Invited_user_can_accept_and_then_log_in),
        ["GET api/Auth/domain-claim/{token}"] = nameof(DomainVerificationTests.IT_claims_a_workspace_someone_else_set_up),
        ["POST api/Auth/domain-claim/check"] = nameof(DomainVerificationTests.IT_claims_a_workspace_someone_else_set_up),

        ["GET api/Company/me"] = nameof(CompanyEndpointsTests.Users_see_their_own_company),
        ["GET api/Company/{id:guid}"] = nameof(CompanyEndpointsTests.Another_company_looks_the_same_as_one_that_does_not_exist),
        ["PUT api/Company/{id:guid}"] = nameof(CompanyEndpointsTests.Admins_cannot_update_another_company),
        // The caller's own workspace and record: there's no id to point at another company's
        ["GET api/Company/domain-verification"] = nameof(DomainVerificationTests.Each_member_verifies_only_their_own_workspace_with_their_own_record),
        ["POST api/Company/domain-verification"] = nameof(DomainVerificationTests.Each_member_verifies_only_their_own_workspace_with_their_own_record),
        ["POST api/Company/domain-verification/check"] = nameof(DomainVerificationTests.Each_member_verifies_only_their_own_workspace_with_their_own_record),

        ["POST api/Invitation"] = nameof(InvitationEndpointsTests.An_invitation_is_always_to_the_admins_own_company),
        ["GET api/Invitation/pending"] = nameof(InvitationEndpointsTests.An_admin_cannot_see_resend_or_cancel_another_companys_invitations),
        ["POST api/Invitation/{id:guid}/resend"] = nameof(InvitationEndpointsTests.An_admin_cannot_see_resend_or_cancel_another_companys_invitations),
        ["DELETE api/Invitation/{id:guid}"] = nameof(InvitationEndpointsTests.An_admin_cannot_see_resend_or_cancel_another_companys_invitations),
        ["GET api/Invitation/import/template"] = nameof(InvitationImportTests.Only_admins_import_and_only_into_their_own_company),
        ["POST api/Invitation/import/preview"] = nameof(InvitationImportTests.Only_admins_import_and_only_into_their_own_company),
        ["POST api/Invitation/import"] = nameof(InvitationImportTests.Only_admins_import_and_only_into_their_own_company),

        ["POST api/Transaction"] = nameof(TransactionEndpointsTests.A_receiver_in_another_company_looks_like_one_that_does_not_exist),
        ["GET api/Transaction/{id:guid}"] =
            nameof(TransactionEndpointsTests.Only_the_sender_and_receiver_can_open_a_recognition_and_other_companies_cannot_tell_it_exists),
        ["GET api/Transaction/my-history"] = nameof(TransactionEndpointsTests.Each_list_shows_only_the_callers_own_recognitions),
        ["GET api/Transaction/sent"] = nameof(TransactionEndpointsTests.Each_list_shows_only_the_callers_own_recognitions),
        ["GET api/Transaction/received"] = nameof(TransactionEndpointsTests.Each_list_shows_only_the_callers_own_recognitions),
        ["GET api/Transaction/company-history"] = nameof(CompanyFeedEndpointsTests.The_feed_only_shows_the_callers_company),

        ["GET api/User"] = nameof(UserEndpointsTests.The_directory_only_lists_your_own_company),
        // The caller's own account: there's no id to point at someone else's
        ["GET api/User/me"] = nameof(AccountEndpointsTests.Me_is_the_caller),
        ["POST api/User/me/password"] = nameof(AccountEndpointsTests.Changing_the_password_signs_out_every_other_device_at_once),
        ["GET api/User/{userId:guid}"] = nameof(UserEndpointsTests.A_user_in_another_company_looks_like_one_that_does_not_exist),
        ["GET api/User/deactivated"] = nameof(UserEndpointsTests.The_deactivated_list_only_shows_your_own_company),
        ["PUT api/User/{id:guid}"] = nameof(UserEndpointsTests.Admins_cannot_update_deactivate_or_reactivate_users_in_another_company),
        ["POST api/User/{id:guid}/deactivate"] = nameof(UserEndpointsTests.Admins_cannot_update_deactivate_or_reactivate_users_in_another_company),
        ["POST api/User/{id:guid}/reactivate"] = nameof(UserEndpointsTests.Admins_cannot_update_deactivate_or_reactivate_users_in_another_company),
    };

    [Fact]
    public void Every_endpoint_has_a_cross_company_test()
    {
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["ANY"]).Select(method => $"{method} {e.RoutePattern.RawText}"))
            .ToList();

        Assert.Empty(endpoints.Except(CrossCompanyTests.Keys)); // new: add it to CrossCompanyTests, with a test written for it
        Assert.Empty(CrossCompanyTests.Keys.Except(endpoints)); // gone or renamed: update CrossCompanyTests
    }

    [Fact]
    public void Every_entity_is_kept_to_one_company_or_holds_no_company_data()
    {
        // Refresh tokens and sign-in attempts are looked up by token or email, and hold nothing about a company
        using var db = factory.CreateDbContextWithNoCaller();
        var unfiltered = db.Model.GetEntityTypes()
            .Where(e => e.FindDeclaredQueryFilter(GratiaDbContext.TenantFilter) is null)
            .Select(e => e.ClrType.Name)
            .Order();

        Assert.Equal([nameof(LoginAttempt), nameof(RefreshToken)], unfiltered);
    }

    [Fact]
    public async Task A_query_that_forgets_the_company_sees_only_the_callers()
    {
        await AddRecognitionAndInvitationAsync(factory.Acme);
        await AddRecognitionAndInvitationAsync(factory.Globex);
        await using var db = factory.CreateDbContextFor(factory.Acme);

        // No Where at all, as a query that forgets the company check would be
        Assert.Equal(await IdsAsync<User>(u => u.CompanyId == factory.Acme.Id), (await db.Users.Select(u => u.Id).ToListAsync()).Order());
        Assert.Equal([factory.Acme.Id], await db.Companies.Select(c => c.Id).ToListAsync());
        Assert.Equal(await IdsAsync<Transaction>(t => t.CompanyId == factory.Acme.Id), (await db.Transactions.Select(t => t.Id).ToListAsync()).Order());
        Assert.Equal(await IdsAsync<CompanyInvitation>(i => i.CompanyId == factory.Acme.Id),
            (await db.CompanyInvitations.Select(i => i.Id).ToListAsync()).Order());

        // Find, the includes and the bulk updates are filtered the same way
        Assert.Null(await db.Companies.FindAsync(factory.Globex.Id));
        Assert.All(await db.Transactions.Include(t => t.Sender).ToListAsync(), t => Assert.Equal(factory.Acme.Id, t.Sender.CompanyId));
        Assert.Equal((await IdsAsync<User>(u => u.CompanyId == factory.Acme.Id)).Count(),
            await db.Users.ExecuteUpdateAsync(setters => setters.SetProperty(u => u.JobTitle, u => u.JobTitle)));
    }

    [Fact]
    public async Task With_no_caller_a_query_sees_nothing_unless_it_opts_out()
    {
        await AddRecognitionAndInvitationAsync(factory.Globex);
        await using var db = factory.CreateDbContextWithNoCaller();

        // A background job or an anonymous request
        Assert.Equal(0, await db.Users.CountAsync());
        Assert.Equal(0, await db.Companies.CountAsync());
        Assert.Equal(0, await db.Transactions.CountAsync());
        Assert.Equal(0, await db.CompanyInvitations.CountAsync());
        Assert.Equal(0, await db.Users.ExecuteUpdateAsync(setters => setters.SetProperty(u => u.JobTitle, u => u.JobTitle)));

        // What the cross-company reads do (AcrossCompanies)
        Assert.Equal((await IdsAsync<User>(_ => true)).Count(), await db.Users.IgnoreQueryFilters([GratiaDbContext.TenantFilter]).CountAsync());
    }

    [Fact]
    public async Task The_database_refuses_a_recognition_invitation_or_domain_verification_across_companies()
    {
        // #79: whatever a handler does, not only what CreateTransactionCommand and InviteUserCommand check
        var (acmeId, _) = await factory.AddUserAsync(factory.Acme);
        var (globexId, _) = await factory.AddUserAsync(factory.Globex);
        var acmeAdminId = await factory.UserIdAsync(factory.AcmeAdminEmail);
        var globexAdminId = await factory.UserIdAsync(factory.GlobexAdminEmail);

        await AssertRefusedAsync(Recognition(acmeId, globexId, factory.Acme)); // the receiver is elsewhere
        await AssertRefusedAsync(Recognition(globexId, acmeId, factory.Acme)); // the sender
        await AssertRefusedAsync(Recognition(globexId, globexAdminId, factory.Acme)); // both, in the wrong company's feed
        await AssertRefusedAsync(Invitation(factory.Acme, invitedBy: globexAdminId));
        await AssertRefusedAsync(new DomainVerification(factory.Acme.Id, globexAdminId, "x@acme.test", "token")); // #61

        // Inside one company they're fine, and so is a founding invitation, which has no inviter
        Assert.Equal(3, await factory.WithDbAsync(db =>
        {
            db.AddRange(Recognition(acmeId, acmeAdminId, factory.Acme), Invitation(factory.Acme, acmeAdminId), Invitation(factory.Acme, null));
            return db.SaveChangesAsync();
        }));
    }

    [Fact]
    public async Task A_user_cannot_move_to_another_company()
    {
        // (Id, CompanyId) is the key their recognitions and invitations point at (#79)
        var (id, _) = await factory.AddUserAsync(factory.Acme);

        await Assert.ThrowsAsync<InvalidOperationException>(() => factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.Id == id)).CompanyId = factory.Globex.Id;
            return await db.SaveChangesAsync();
        }));
        Assert.Equal(factory.Acme.Id, await factory.WithDbAsync(db => db.Users.Where(u => u.Id == id).Select(u => u.CompanyId).SingleAsync()));
    }

    private Task AssertRefusedAsync(object row) =>
        Assert.ThrowsAsync<DbUpdateException>(() => factory.WithDbAsync(db =>
        {
            db.Add(row);
            return db.SaveChangesAsync();
        }));

    private static Transaction Recognition(Guid senderId, Guid receiverId, Company company) =>
        new(senderId, receiverId, 1, "Thanks", "Teamwork", company.Id);

    private static CompanyInvitation Invitation(Company company, Guid? invitedBy) =>
        new($"{Guid.NewGuid():N}@{company.Domain}", Guid.NewGuid().ToString("N"), DateTime.UtcNow.AddDays(1), UserRole.Employee, company.Id, invitedBy);

    // A recognition between two new members, and an invitation, so the company has one of each
    private async Task AddRecognitionAndInvitationAsync(Company company)
    {
        var (senderId, _) = await factory.AddUserAsync(company);
        var (receiverId, _) = await factory.AddUserAsync(company);
        await factory.WithDbAsync(db =>
        {
            db.AddRange(Recognition(senderId, receiverId, company), Invitation(company, invitedBy: null));
            return db.SaveChangesAsync();
        });
    }

    // The ids as the database holds them, whatever the company
    private Task<IOrderedEnumerable<Guid>> IdsAsync<T>(System.Linq.Expressions.Expression<Func<T, bool>> where) where T : BaseEntity =>
        factory.WithDbAsync(async db => (await db.Set<T>().Where(where).Select(e => e.Id).ToListAsync()).Order());
}
