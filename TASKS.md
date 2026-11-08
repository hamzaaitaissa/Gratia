# Gratia — tasks

`- [ ]` is open, `- [x]` is done. Numbers #1–#50 come from the first code review and are kept so tasks can be referred to by number. New tasks take the next free number (currently #61).

Suggested order: section 1, then 2 and 3 (write tests from section 6 as you go), then 7 and 8.

## 1. Blockers: main flows and the frontend

- [ ] **#1 No way to create a company's first admin.** `POST /api/company` is anonymous, but `POST /api/user` requires Admin. Add a "register company + first admin" endpoint. This depends on the onboarding decision (#49).
- [ ] **#3 Inviting throws.** `InvitationRepository` filters on the computed `IsExpired`, which EF can't translate to SQL (lines 35 and 45). Use `i.ExpiresAt > DateTime.UtcNow`.
- [ ] **#4 No `InvitationController`.**
  - The handlers already exist in `Features/Invitations`. Add endpoints for invite (Admin), list pending (Admin), cancel (Admin), look up by token (anonymous) and accept (anonymous).
  - Add a `ResendInvitationCommand`. The old `ResendInvitationAsync` was only a `NotImplementedException` stub and was dropped.
- [ ] **#5 Updating a user always returns 400.** `[AllowedValues("Employee", "Admin")]` on the `UserRole` enum always fails (`UpdateUserDto.cs`).
- [ ] **#6 Updating a company returns 500.** `[Authorize("Admin")]` names a policy that doesn't exist; use `[Authorize(Roles = "Admin")]` (`CompanyController.cs`). The Id half of #6 is done.
- [ ] **#29 No CORS policy**, so the browser will block the frontend. Make the allowed origins configurable.
- [ ] **#51 Password lengths disagree.** `AcceptInvitationDto` allows 6 characters but `LoginDto` requires 8, so some invited users can never log in. Use 8 everywhere.
- [ ] **#52 `role` is serialized inconsistently.** It's a string in `ReadUserDto` but a number (0/1) in `UpdateUserDto`, `InviteUserDto` and `InvitationDto`. Register a global `JsonStringEnumConverter` and remove the per-property converter.

## 2. Business logic

- [ ] **#9 No balance check when sending points**, so balances can go negative. Throw a `BusinessRuleException` when the amount is greater than `NumberOfPointsAvailable` (`CreateTransactionCommandHandler`).
- [ ] **#10 Starting points are swapped.**
  - New users get 25 received and 0 to give (the `User.cs` defaults, and `AcceptInvitationCommandHandler` passes `25, 0`).
  - They should get the monthly allowance to give and 0 received.
  - Fix the welcome email text as well ("You've been given 25 points").
- [ ] **#11 Race when sending points.** Two simultaneous sends can both pass the balance check. Add a concurrency token (a `rowversion` on `User`) or a conditional `UPDATE … WHERE NumberOfPointsAvailable >= @amount`. Atomicity is already done through the unit of work.
- [ ] **#13 / #14 Company history (the feed) is broken** (`TransactionRepository.GetCompanyTransactionHistoryAsync` and `GetCompanyTransactionHistoryQueryHandler`):
  - It uses `.Skip(page)`; it should be `.Skip((page - 1) * pageSize)`.
  - `TotalCount`, and so `TotalPages`, counts only the current page. Use `GetTotalTransactionCountAsync`.
  - It has no `OrderByDescending(CreatedDate)`.
  - It has no `Include` for Sender, Receiver or Company, so the names come back empty.
  - `page` and `pageSize` aren't validated (use `page >= 1` and `1..100`).
  - The `CreateTransactionAsync` response also has an empty `CompanyName`.
- [ ] **#15 Refresh tokens.**
  - The response says it expires in 1 hour (`TokenService.cs:37`), but the database stores 7 days (`LoginCommandHandler` and `RefreshTokenCommandHandler`). Make it one setting.
  - Each user has only one refresh token, so logging in on a second device logs out the first. Keep that, or move to a `RefreshTokens` table.
- [ ] **#16 Deleting a user who has transactions fails** on the `Restrict` foreign key, giving a 500. Replace delete with deactivation (an `IsActive` flag); deactivated users can't log in and are hidden from the directory.
- [ ] **#17 Updating a user forces a new password**, and a changed email isn't checked for uniqueness (it hits the unique index and returns 500).
  - Drop the password from `UpdateUserDto` (it gets its own change-password endpoint, #50).
  - Throw a `ConflictException` on a duplicate email.
- [ ] **#18 `[Required]` on a `Guid` does nothing** (`RegisterUserDto.CompanyId`). This goes away when #23 removes `CompanyId` from the body.
- [ ] **#19 Points reset.**
  - The day is hard-coded (`Day == 21`, `PointResetWorker.cs:23`).
  - One allowance applies to every company.
  - The Worker isn't deployed with Docker, so points never reset in a container (see #36).
- [ ] **#20 Invitation edge cases.**
  - The pending list includes invitations that were already used; add `!IsUsed`.
  - `GetByTokenAsync` doesn't `Include` `Company` or `InvitedByUser`, so their names come back empty.
  - The invitation is saved before the email is sent, so if SMTP fails the caller gets a 500 but the invitation exists. See #55.
- [ ] **#21 Email.**
  - `IsBodyHtml` isn't set, so messages arrive as raw HTML.
  - `FrontendUrl` isn't configured anywhere, so links have no host.
  - The welcome email is never sent.
  - `SmtpClient` is never disposed.
  - `EmailService` reads `IConfiguration` directly; give it an options class.

## 3. Security and multi-tenancy

- [ ] **#22 No tenant isolation.**
  - `GET /api/user` returns users from every company.
  - Admins can edit or delete users in other companies.
  - `GET /api/company` and `/api/company/{id}` are anonymous.
  - Fix: scope every query to `ICurrentUserService.CompanyId`, possibly with an EF global query filter.
- [ ] **#23 `RegisterUserDto` accepts `CompanyId` and both point balances from the client.** Take the company from the token and set the balances by the rules, or drop `POST /api/user` in favour of invitations.
- [ ] **#25 JWT issuer and audience aren't validated** (`ValidateIssuer`/`ValidateAudience = false` in `Program.cs`). Validate them against `JwtOptions` and set `ClockSkew`.
- [ ] **#26 Anyone can create companies** (`POST /api/company` is anonymous). Resolved together with #1 and #49.
- [ ] **#27 Login has no rate limiting or lockout**, and refresh tokens are stored in plain text. Store a hash.
- [ ] **#28 Invitation tokens leak.** `InvitationToken` is returned in the invite and pending-list responses; remove it from `InvitationDto`.

## 4. Architecture and cleanup

- [ ] **#33 The domain model is anemic.** Move the rules onto the entities (for example `sender.GivePoints(receiver, amount)`) and make setters private. Remove `Transaction.Update`.
- [ ] **#36 The Worker is a second app to deploy** and isn't in Docker. Host the reset job in the API as a `BackgroundService`, or add the Worker to compose.
- [ ] **#37 The Domain has persistence attributes.** The `[ForeignKey]` attributes in `User.cs` and `CompanyInvitation.cs` duplicate the Fluent configuration; remove them.
- [ ] **#39 Naming.**
  - The `Persistence/` folder uses the namespace `Gratia.Infrastructure.Data`.
  - The `Transaction` entity collides with `System.Transactions`.
  - Input DTOs call the plain password `HashedPassword`; rename it to `Password`. This changes the frontend contract.
- [ ] **#40 Time handling.**
  - `BaseEntity.CreatedDate` uses `DateTime.Now`; switch to `UtcNow`.
  - Dates read from SQL come back with `Kind = Unspecified`, so the JSON has no `Z` suffix. Serialize them as UTC.
  - Consider `TimeProvider`, which makes expiry and the reset testable.
- [ ] **#47 45 build warnings, mostly nullable (CS8618 on DTOs and entities).** Fix them, then turn on `TreatWarningsAsErrors`. This includes the CS8604 in `EmailService.cs:59`.
- [ ] **#48 Leftovers:**
  - `ITokenService.GetPrincipalFromExpiredToken` is never used.
  - The unused `TransactionRepository.GetByCompanyIdAsync`.
  - `Gratia.API.http` is still the weather-forecast template.
  - `Gratia.Application/Notes.md` describes another project.
  - Unused usings and the unused `ICompanyRepository.Remove`.

## 5. DevOps and configuration

- [ ] **#42 `compose.yaml` has no database**, and the connection string uses Windows authentication against `HAMZA\SQLEXPRESS`.
  - Add a SQL Server service.
  - Pass `ConnectionStrings__DefaultConnection`, `AppSettings__Token` and the email credentials as environment variables.
  - Test `docker compose up --build`; the image has never been built.
- [ ] **#43 Config is untidy.**
  - `FrontendUrl` is missing.
  - The connection string is duplicated in the API and Worker `appsettings.json`.
  - `<WarningsAsErrors>false</WarningsAsErrors>` in `Gratia.Application.csproj` does nothing; remove it.
- [ ] **#44 CI only runs for `main`.** Add `workflow_dispatch` and/or the working branches.
- [ ] **#45 `UseHttpsRedirection` doesn't fit a container** that only serves HTTP. Configure `ForwardedHeaders` when it runs behind a proxy.

## 6. Tests

- [ ] **#46 There's only one test** (`UserControllerTests`, which mocks everything).
  - Add service tests for the point rules, invitations and the unit of work (one save per use case).
  - Add `WebApplicationFactory` integration tests for auth, ProblemDetails responses and tenant scoping.
  - Write them alongside each fix.

## 7. Product features

- [ ] **#49 Decide how companies get onboarded:** self-serve "register company + first admin", or a platform super-admin (the TODO in `UserController.cs`).
- [ ] **#50 Account endpoints:** `GET /api/user/me`, change password and forgot password.
- [ ] **#56 Engagement features:** a leaderboard; per-company settings (allowance, reset day, and categories instead of free-text `TypeOfDonation`); a use for received points, such as rewards.
- [ ] **#53 Points-cycle endpoint:** the allowance and next refill date, for the frontend's balance card.
- [ ] **#54 Bulk invitations from a spreadsheet.** Depends on #4 and #55.
  - **Template:** `GET /api/invitations/import/template` returns an .xlsx (ClosedXML) with the columns Email (required), Full name, Job title, and Role (as a dropdown).
  - **Preview:** `POST /api/invitations/import/preview` takes a multipart upload and returns each row with a status (`ready`, `error`, `duplicate`, `member`, `invited`).
  - **Confirm:** `POST /api/invitations/import` takes the rows as JSON, re-checks each one, creates every invitation in one save, and returns `{invited, skipped}`.
  - **Database:** add `FullName` and `JobTitle` columns to `CompanyInvitation` (migration) and pre-fill them on the accept form.
  - **Limits:**
    - Admin only, and the company always comes from the token.
    - Maximum 1 MB and 500 rows.
    - In the "skipped rows" download, escape cells that start with `=`, `+`, `-` or `@`.
- [ ] **#55 Send invitation emails in the background** (the Worker or a hosted service, plus a "sent" flag on the invitation), so bulk invites don't time out and Resend has something to act on.

## 8. Frontend

- [ ] **#57 Choose the stack:** React + TypeScript (recommended) or Blazor WebAssembly.
- [ ] **#58 Run the design brief in Claude Design:** design system, screens, prototype and handoff notes.
- [ ] **#59 Scaffold the app:**
  - A typed API client generated from `/openapi/v1.json`.
  - Auth: the access token, refresh on 401, and sign-out through revoke.
  - Runtime theming from the company colours.
  - Then build the screens from the brief.

## Done

- [x] Moved to .NET 10, fixed the NuGet package versions, and updated CI and the Dockerfile for .NET 10 (with a working Alpine publish).
- [x] #41 Migrations are tracked in git.
- [x] #24 The JWT key was rotated and moved to user-secrets; startup fails fast without a valid key (at least 64 bytes).
- [x] #30 Infrastructure code moved out of Application: tokens, hashing and email are in Infrastructure, `CurrentUserService` is in the API, and BCrypt is removed.
- [x] #31 The half-done CQRS split and MediatR were removed, then replaced properly by #60.
- [x] **#60 CQRS with Mediator** (the source generator, MIT licensed).
  - Every use case is a command or query with its handler in `Gratia.Application/Features/`, and the service classes are gone.
  - A `LoggingBehavior` pipeline runs around every message.
  - The controllers and the Worker only send messages.
- [x] #38 Mapping lives in one place (`Gratia.Application/Mappings/`).
- [x] #32 Unit of work (`GratiaDbContext : IUnitOfWork`), with one save per use case.
- [x] #34 Exceptions, `GlobalExceptionHandler` and ProblemDetails; controllers have no try/catch.
- [x] #35 `AddApplication()` / `AddInfrastructure()` are used by both the API and the Worker; options are validated.
- [x] Fixed by the refactor:
  - #2 Invited users can log in, because there's one hasher.
  - #7 `Forbid("...")` no longer throws.
  - #8 Records that aren't found return 404.
  - #12 Tracked updates write only the changed columns.
  - #6 (half) A company update keeps its Id.
  - #18 (half) Exceptions now match the errors.
  - #19 (half) The reset is one `ExecuteUpdateAsync`.
  - #20 (half) Accepting an invitation is one save.
