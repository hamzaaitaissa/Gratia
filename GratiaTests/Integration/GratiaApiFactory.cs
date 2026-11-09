using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gratia.Application.DTOs.Token;
using Gratia.Application.Interfaces;
using Gratia.Domain.Entities;
using Gratia.Domain.Enum;
using Gratia.Infrastructure.Data;
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
        // The background refill would share the SQLite connection with the tests; PointsCycleTests sends the command itself
        builder.UseSetting("PointsReset:Enabled", "false");

        builder.ConfigureTestServices(services =>
        {
            // Replace the SQL Server registration entirely, rather than adding a second provider
            services.RemoveAll<DbContextOptions<GratiaDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<GratiaDbContext>>();
            services.AddDbContext<GratiaDbContext>(options => options.UseSqlite(_connection));

            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(Emails);
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

    /// <summary>Runs code against the test database, for setting up a state the API can't reach or checking the result.</summary>
    public async Task<T> WithDbAsync<T>(Func<GratiaDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<GratiaDbContext>());
    }

    /// <summary>
    /// Adds a throwaway user who can log in with <see cref="Password"/>, for tests that change balances or delete
    /// users, so they don't affect the seeded users that other tests in the class rely on.
    /// </summary>
    public async Task<(Guid Id, string Email)> AddUserAsync(Company company, int pointsAvailable = 25)
    {
        using var scope = Services.CreateScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHashingService>();
        var db = scope.ServiceProvider.GetRequiredService<GratiaDbContext>();

        var user = new User("Temp User", $"{Guid.NewGuid():N}@{company.Domain}", hasher.HashPassword(Password), "Tester",
            UserRole.Employee, 0, pointsAvailable, company.Id);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user.Id, user.Email);
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
