using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Gratia.Application.DTOs.Token;
using Gratia.Application.DTOs.UserDTO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GratiaTests.Integration;

// The signed-in user's own account (#50): who they are, their password, and a forgotten one. Every test has a user of
// its own, so changing a password never touches the seeded users.
public sealed class AccountEndpointsTests(GratiaApiFactory factory) : IClassFixture<GratiaApiFactory>
{
    private const string NewPassword = "New-password-1";

    [Fact]
    public async Task Me_is_the_caller()
    {
        var (id, email) = await factory.AddUserAsync(factory.Globex);

        var me = await (await factory.CreateClientForAsync(email)).GetFromJsonAsync<ReadUserDto>("/api/user/me", GratiaApiFactory.Json);

        Assert.Equal((id, email, factory.Globex.Id), (me!.Id, me.Email, me.CompanyId));
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/user/me")).StatusCode);
    }

    [Fact]
    public async Task Changing_the_password_needs_the_current_one()
    {
        var (_, email) = await factory.AddUserAsync(factory.Acme);
        var client = await factory.CreateClientForAsync(email);

        var response = await client.PostAsJsonAsync("/api/user/me/password", new { currentPassword = "Wrong-password-1", newPassword = NewPassword });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Your current password isn't right.", (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);
        Assert.Equal(HttpStatusCode.OK, await StatusOfAsync(client));
        await factory.CreateClientForAsync(email); // the old password still works
        Assert.DoesNotContain(factory.Emails.SentTo(email), e => e.Kind == EmailKind.PasswordChanged);
    }

    [Fact]
    public async Task Changing_the_password_signs_out_every_other_device_at_once()
    {
        var (_, email) = await factory.AddUserAsync(factory.Acme);
        var laptop = await LoginAsync(email, GratiaApiFactory.Password);
        var phone = await LoginAsync(email, GratiaApiFactory.Password);

        var response = await ClientWith(laptop.AccessToken)
            .PostAsJsonAsync("/api/user/me/password", new { currentPassword = GratiaApiFactory.Password, newPassword = NewPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var renewed = (await response.Content.ReadFromJsonAsync<TokenResponseDto>())!;

        // This device carries on with the new tokens. Every other session ends at once, its access token included:
        // before #50 that one kept working for up to an hour.
        Assert.Equal(HttpStatusCode.OK, await StatusOfAsync(ClientWith(renewed.AccessToken)));
        Assert.Equal(HttpStatusCode.Unauthorized, await StatusOfAsync(ClientWith(phone.AccessToken)));
        Assert.Equal(HttpStatusCode.Unauthorized, await StatusOfAsync(ClientWith(laptop.AccessToken)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(phone.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(laptop.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(renewed.RefreshToken)).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await SignInAsync(email, GratiaApiFactory.Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(email, NewPassword)).StatusCode);
        Assert.Contains(factory.Emails.SentTo(email), e => e.Kind == EmailKind.PasswordChanged);
    }

    [Fact]
    public async Task A_forgotten_password_is_reset_once_with_the_emailed_link()
    {
        var (id, email) = await factory.AddUserAsync(factory.Acme);
        var phone = await LoginAsync(email, GratiaApiFactory.Password);

        Assert.Equal(HttpStatusCode.Accepted, (await ForgotAsync($"  {email.ToUpperInvariant()} ")).StatusCode);
        var token = Assert.Single(factory.Emails.SentTo(email), e => e.Kind == EmailKind.PasswordReset).Token!;
        // Only a hash is stored: the token from the email is as good as a password for an hour
        Assert.NotEqual(token, await factory.WithDbAsync(db => db.Users.Where(u => u.Id == id).Select(u => u.PasswordResetTokenHash).SingleAsync()));

        var reset = await ResetAsync(token, NewPassword);

        // Signed in here; every other session has ended
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        var tokens = (await reset.Content.ReadFromJsonAsync<TokenResponseDto>())!;
        Assert.Equal(HttpStatusCode.OK, await StatusOfAsync(ClientWith(tokens.AccessToken)));
        Assert.Equal(HttpStatusCode.Unauthorized, await StatusOfAsync(ClientWith(phone.AccessToken)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(phone.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SignInAsync(email, GratiaApiFactory.Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(email, NewPassword)).StatusCode);
        Assert.Contains(factory.Emails.SentTo(email), e => e.Kind == EmailKind.PasswordChanged);

        // The link works once
        var again = await ResetAsync(token, "Another-password-1");
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal("This link is invalid or has expired. Ask for a new one.", (await again.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);
    }

    [Fact]
    public async Task Forgot_password_answers_the_same_for_an_email_without_an_active_account()
    {
        var unknown = $"{Guid.NewGuid():N}@acme.test";
        var (id, deactivated) = await factory.AddUserAsync(factory.Acme);
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/user/{id}/deactivate", null)).StatusCode);

        // The same answer as for an account; only the mailbox owner learns there's none
        foreach (var email in new[] { unknown, deactivated })
        {
            Assert.Equal(HttpStatusCode.Accepted, (await ForgotAsync(email)).StatusCode);
            Assert.Equal(EmailKind.PasswordResetUnavailable, Assert.Single(factory.Emails.SentTo(email)).Kind);
        }
        Assert.Null(await factory.WithDbAsync(db => db.Users.Where(u => u.Id == id).Select(u => u.PasswordResetTokenHash).SingleAsync()));
    }

    [Fact]
    public async Task A_reset_link_stops_working_after_an_hour()
    {
        var (id, email) = await factory.AddUserAsync(factory.Acme);
        await ForgotAsync(email);
        await factory.WithDbAsync(db => db.Users.Where(u => u.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.PasswordResetExpiresAt, DateTime.UtcNow.AddMinutes(-1))));

        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(factory.Emails.LastTokenFor(email), NewPassword)).StatusCode);
        await factory.CreateClientForAsync(email); // the password is unchanged
    }

    [Fact]
    public async Task A_link_stops_working_when_its_user_is_deactivated()
    {
        var (id, email) = await factory.AddUserAsync(factory.Acme);
        await ForgotAsync(email);
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/user/{id}/deactivate", null)).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(factory.Emails.LastTokenFor(email), NewPassword)).StatusCode);

        // Nothing changed: reactivated, they sign in with the password they had
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/user/{id}/reactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(email, GratiaApiFactory.Password)).StatusCode);
    }

    [Fact]
    public async Task A_new_reset_link_replaces_the_last_one()
    {
        var (_, email) = await factory.AddUserAsync(factory.Acme);
        await ForgotAsync(email);
        var first = factory.Emails.LastTokenFor(email);
        await ForgotAsync(email);

        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(first, NewPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ResetAsync(factory.Emails.LastTokenFor(email), NewPassword)).StatusCode);
    }

    [Fact]
    public async Task Resetting_the_password_lifts_the_sign_in_lock()
    {
        // Locked out by failed sign-ins (#72), which is often why people reset
        var (_, email) = await factory.AddUserAsync(factory.Acme);
        for (var i = 0; i < 5; i++)
            await SignInAsync(email, "Wrong-password-1");
        Assert.Equal(HttpStatusCode.TooManyRequests, (await SignInAsync(email, GratiaApiFactory.Password)).StatusCode);

        await ForgotAsync(email);
        Assert.Equal(HttpStatusCode.OK, (await ResetAsync(factory.Emails.LastTokenFor(email), NewPassword)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(email, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task When_the_reset_email_fails_the_link_already_sent_keeps_working()
    {
        var (_, email) = await factory.AddUserAsync(factory.Acme);
        await ForgotAsync(email);
        var sent = factory.Emails.LastTokenFor(email);

        factory.Emails.FailFor(email);
        try
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await ForgotAsync(email)).StatusCode);
        }
        finally
        {
            factory.Emails.StopFailingFor(email);
        }

        // Undone (#20): the link nobody received doesn't replace the one that was
        Assert.Equal(HttpStatusCode.OK, (await ResetAsync(sent, NewPassword)).StatusCode);
    }

    private Task<HttpResponseMessage> ForgotAsync(string email) =>
        factory.CreateClient().PostAsJsonAsync("/api/auth/forgot-password", new { email });

    private Task<HttpResponseMessage> ResetAsync(string token, string newPassword) =>
        factory.CreateClient().PostAsJsonAsync("/api/auth/reset-password", new { token, newPassword });

    private Task<HttpResponseMessage> SignInAsync(string email, string password) =>
        factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, hashedPassword = password });

    private async Task<TokenResponseDto> LoginAsync(string email, string password)
    {
        var response = await SignInAsync(email, password);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TokenResponseDto>())!;
    }

    private Task<HttpResponseMessage> RefreshAsync(string? refreshToken) =>
        factory.CreateClient().PostAsJsonAsync("/api/auth/refresh-token", refreshToken);

    private HttpClient ClientWith(string? accessToken)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private static async Task<HttpStatusCode> StatusOfAsync(HttpClient client) => (await client.GetAsync("/api/user/me")).StatusCode;
}
