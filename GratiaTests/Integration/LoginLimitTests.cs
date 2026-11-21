using System.Net;
using System.Net.Http.Json;
using Gratia.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GratiaTests.Integration;

// Failed sign-ins are limited per email (#72), on top of the per-IP limit (#27, which GratiaApiFactory sets out of the
// way). Each test signs in with emails of its own, so the counts don't mix.
public sealed class LoginLimitTests(GratiaApiFactory factory) : IClassFixture<GratiaApiFactory>
{
    private const string WrongPassword = "Wrong-password-1";
    private const string LockedFor15Minutes = "Too many failed sign-ins with this email. Try again in 15 minutes.";

    [Fact]
    public async Task After_five_failures_the_email_is_locked_even_against_the_right_password()
    {
        var company = await factory.AddCompanyAsync();
        var (userId, email) = await factory.AddUserAsync(company);
        var (_, colleague) = await factory.AddUserAsync(company);

        // However the email is typed, it's one email
        foreach (var typed in new[] { email, email.ToUpperInvariant(), $" {email} ", email, email })
            Assert.Equal(HttpStatusCode.BadRequest, (await LoginAsync(typed, WrongPassword)).StatusCode);

        var locked = await LoginAsync(email, GratiaApiFactory.Password);

        Assert.Equal(HttpStatusCode.TooManyRequests, locked.StatusCode);
        Assert.InRange(int.Parse(locked.Headers.GetValues("Retry-After").Single()), 14 * 60, 15 * 60);
        var problem = (await locked.Content.ReadFromJsonAsync<ProblemDetails>())!;
        Assert.Equal(("Too many attempts", LockedFor15Minutes), (problem.Title, problem.Detail));
        Assert.Equal(0, await factory.WithDbAsync(db => db.RefreshTokens.CountAsync(t => t.UserId == userId)));

        // Per email: a colleague behind the same IP signs in as usual
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(colleague, GratiaApiFactory.Password)).StatusCode);
    }

    [Fact]
    public async Task An_email_without_an_account_is_locked_the_same_way()
    {
        // Otherwise five wrong passwords would tell anyone whether an email has an account
        var email = $"{Guid.NewGuid():N}@nobody.test";
        await FailAsync(email, LoginAttempt.MaxFailures);

        var locked = await LoginAsync(email, WrongPassword);

        Assert.Equal(HttpStatusCode.TooManyRequests, locked.StatusCode);
        var problem = (await locked.Content.ReadFromJsonAsync<ProblemDetails>())!;
        Assert.Equal(("Too many attempts", LockedFor15Minutes), (problem.Title, problem.Detail));
    }

    [Fact]
    public async Task The_lock_ends_when_the_failures_are_old_and_trying_meanwhile_does_not_extend_it()
    {
        var (_, email) = await factory.AddUserAsync(await factory.AddCompanyAsync());
        await FailAsync(email, LoginAttempt.MaxFailures);

        // The owner keeps trying while it's locked. Those attempts check no password, so they aren't failures.
        Assert.Equal(HttpStatusCode.TooManyRequests, (await LoginAsync(email, GratiaApiFactory.Password)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await LoginAsync(email, GratiaApiFactory.Password)).StatusCode);
        Assert.Equal(LoginAttempt.MaxFailures, await AttemptsAsync(email));

        await AgeAttemptsAsync(email, LoginAttempt.Window);

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(email, GratiaApiFactory.Password)).StatusCode);
    }

    [Fact]
    public async Task Signing_in_clears_the_failures()
    {
        var (_, email) = await factory.AddUserAsync(await factory.AddCompanyAsync());
        await FailAsync(email, LoginAttempt.MaxFailures - 1);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(email, GratiaApiFactory.Password)).StatusCode);

        // A fresh count: as many failures again, and the right password still gets in
        await FailAsync(email, LoginAttempt.MaxFailures - 1);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(email, GratiaApiFactory.Password)).StatusCode);
    }

    [Fact]
    public async Task Attempts_older_than_the_window_are_deleted_by_any_sign_in()
    {
        var stale = $"{Guid.NewGuid():N}@nobody.test";
        await FailAsync(stale, 1);
        await AgeAttemptsAsync(stale, LoginAttempt.Window);

        await LoginAsync(factory.AcmeEmployeeEmail, GratiaApiFactory.Password);

        Assert.Equal(0, await AttemptsAsync(stale));
    }

    private Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, hashedPassword = password });

    private async Task FailAsync(string email, int times)
    {
        for (var i = 0; i < times; i++)
            Assert.Equal(HttpStatusCode.BadRequest, (await LoginAsync(email, WrongPassword)).StatusCode);
    }

    private Task<int> AttemptsAsync(string email) =>
        factory.WithDbAsync(db => db.LoginAttempts.CountAsync(a => a.Email == email));

    // As if they'd all been made `age` ago
    private Task<int> AgeAttemptsAsync(string email, TimeSpan age)
    {
        var then = DateTime.UtcNow - age;
        return factory.WithDbAsync(db => db.LoginAttempts.Where(a => a.Email == email)
            .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.CreatedDate, then)));
    }
}
