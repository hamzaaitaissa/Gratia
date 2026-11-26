using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gratia.Application.DTOs.Token;
using Gratia.Application.Features.Invitations.Commands;
using Gratia.Application.Interfaces;
using Gratia.Domain.Entities;
using Gratia.Domain.Enum;
using Gratia.Infrastructure.Data;
using Mediator;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GratiaTests.Integration;

/// <summary>
/// Runs the real API in memory against a throwaway SQLite database, a random JWT key and a fake email sender,
/// so the tests never touch the local SQL Server, user-secrets or SMTP, and run the same way in CI.
/// </summary>
public sealed class GratiaApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "Password123!";

    // How the API writes JSON: the web defaults, with enums as names (#52). Use it to read DTOs that contain a role.
    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    // An in-memory SQLite database lives as long as its connection stays open
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public FakeEmailService Emails { get; } = new();

    // The domains' DNS, for domain verification (#61): no real lookups
    public FakeDns Dns { get; } = new();

    // Two provisional (unverified) companies, so the tests can check that one company's admin can't touch the other's data
    public Company Acme { get; } = new("Acme", "https://example.com/acme.png", "#112233", "#445566") { Domain = "acme.test" };
    public Company Globex { get; } = new("Globex", "https://example.com/globex.png", "#665544", "#332211") { Domain = "globex.test" };
    public string AcmeAdminEmail => "admin@acme.test";
    public string AcmeEmployeeEmail => "employee@acme.test";
    public string GlobexAdminEmail => "admin@globex.test";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not Development, so user-secrets (the real JWT key and email credentials) are never loaded
        builder.UseEnvironment("Testing");
        builder.UseSetting("AppSettings:Token", Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)));
        builder.UseSetting("AppSettings:FrontendUrl", "http://localhost:5173");
        builder.UseSetting("EMAIL_CONFIGURATION:EMAIL", "noreply@gratia.test"); // never used: FakeEmailService replaces SMTP
        builder.UseSetting("EMAIL_CONFIGURATION:PASSWORD", "unused");
        // Every test request comes from the same (empty) IP. RateLimitTests lowers these on a copy of the factory.
        builder.UseSetting("RateLimiting:SignupPerHour", "1000");
        builder.UseSetting("RateLimiting:LoginPerMinute", "1000");
        builder.UseSetting("RateLimiting:RefreshPerMinute", "1000");
        builder.UseSetting("RateLimiting:PasswordResetPerHour", "1000");
        builder.UseSetting("RateLimiting:DomainChecksPerMinute", "1000");
        // The seeded companies are unverified; InvitationQuotaTests lowers the daily invitation cap (#65) on a copy
        builder.UseSetting("Onboarding:UnverifiedInvitationsPerDay", "1000");
        // The background refill would share the SQLite connection with the tests; PointsCycleTests sends the command itself
        builder.UseSetting("PointsReset:Enabled", "false");
        // Likewise the background invitation emails (#55): tests send them with SendInvitationEmailsAsync
        builder.UseSetting("InvitationEmails:Enabled", "false");

        builder.ConfigureTestServices(services =>
        {
            // Replace the SQL Server registration entirely, rather than adding a second provider
            services.RemoveAll<DbContextOptions<GratiaDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<GratiaDbContext>>();
            services.AddDbContext<GratiaDbContext>(options => options.UseSqlite(_connection));

            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(Emails);

            services.RemoveAll<IDomainVerifier>();
            services.AddSingleton<IDomainVerifier>(Dns);
        });
    }

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GratiaDbContext>();
        if (!db.Database.IsSqlite())
            throw new InvalidOperationException("Integration tests must run against the in-memory SQLite database.");

        await db.Database.EnsureCreatedAsync();

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHashingService>();
        User NewUser(string name, string email, UserRole role, Company company) =>
            new(name, email, hasher.HashPassword(Password), "Tester", role, 0, 25, company.Id) { Company = company };

        db.Companies.AddRange(Acme, Globex);
        db.Users.AddRange(
            NewUser("Acme Admin", AcmeAdminEmail, UserRole.Admin, Acme),
            NewUser("Acme Employee", AcmeEmployeeEmail, UserRole.Employee, Acme),
            NewUser("Globex Admin", GlobexAdminEmail, UserRole.Admin, Globex));
        await db.SaveChangesAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _connection.DisposeAsync();
    }

    /// <summary>
    /// Runs code against the test database, for setting up a state the API can't reach or checking the result. It sees
    /// every company's rows; the API's own context sees only the caller's company (#78).
    /// </summary>
    public async Task<T> WithDbAsync<T>(Func<GratiaDbContext, Task<T>> action)
    {
        await using var db = new AllCompaniesDbContext(DbOptions);
        return await action(db);
    }

    /// <summary>A context that sees only this company's rows, like a request from one of its members (#78).</summary>
    public GratiaDbContext CreateDbContextFor(Company company) => new(DbOptions, new CallerIn(company.Id));

    /// <summary>A context with no caller, like the background refill's or an anonymous request's: it sees nothing (#78).</summary>
    public GratiaDbContext CreateDbContextWithNoCaller() => new(DbOptions);

    // The API's database, for contexts made outside a request. Not the API's own options: those belong to a scope.
    private DbContextOptions<GratiaDbContext> DbOptions => new DbContextOptionsBuilder<GratiaDbContext>().UseSqlite(_connection).Options;

    // The API's model without the tenant filter. Its own type, so EF builds and caches its model apart from the API's.
    private sealed class AllCompaniesDbContext(DbContextOptions<GratiaDbContext> options) : GratiaDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
                entityType.SetQueryFilter(TenantFilter, null);
        }
    }

    private sealed class CallerIn(Guid companyId) : ICurrentUserService
    {
        public Guid UserId => Guid.Empty;
        public Guid CompanyId => companyId;
        public UserRole? Role => null;
    }

    /// <summary>
    /// Adds a throwaway user who can log in with <see cref="Password"/>, for tests that change balances or delete
    /// users, so they don't affect the seeded users that other tests in the class rely on.
    /// </summary>
    public async Task<(Guid Id, string Email)> AddUserAsync(Company company, int pointsAvailable = 25, UserRole role = UserRole.Employee)
    {
        using var scope = Services.CreateScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHashingService>();
        var db = scope.ServiceProvider.GetRequiredService<GratiaDbContext>();

        var user = new User("Temp User", $"{Guid.NewGuid():N}@{company.Domain}", hasher.HashPassword(Password), "Tester",
            role, 0, pointsAvailable, company.Id);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user.Id, user.Email);
    }

    /// <summary>An empty company of its own, for tests that need to know exactly who its members are.</summary>
    public async Task<Company> AddCompanyAsync()
    {
        var company = new Company("Temp Co", "", "#123456", "#654321") { Domain = $"{Guid.NewGuid():N}.test" };
        await WithDbAsync(db =>
        {
            db.Companies.Add(company);
            return db.SaveChangesAsync();
        });
        return company;
    }

    /// <summary>
    /// Sends the invitation emails that are due, as the API's background sender would (#55). Invites and resends only
    /// queue their email. Returns how many went out.
    /// </summary>
    public async Task<int> SendInvitationEmailsAsync(DateTime? now = null)
    {
        using var scope = Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new SendInvitationEmailsCommand(now ?? DateTime.UtcNow));
    }

    public Task<Guid> UserIdAsync(string email) =>
        WithDbAsync(db => db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync());

    /// <summary>Logs in through the API and returns a client that sends the access token.</summary>
    public async Task<HttpClient> CreateClientForAsync(string email, string password = Password)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, hashedPassword = password });
        response.EnsureSuccessStatusCode();

        var tokens = await response.Content.ReadFromJsonAsync<TokenResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        return client;
    }
}
