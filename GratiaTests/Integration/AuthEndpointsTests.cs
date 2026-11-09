using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Gratia.Application.DTOs.Token;
using Microsoft.EntityFrameworkCore;

namespace GratiaTests.Integration;

// Sessions (#15): one refresh token per device, replaced on every refresh, and stored only as a hash (#27)
public sealed class AuthEndpointsTests(GratiaApiFactory factory) : IClassFixture<GratiaApiFactory>
{
    [Fact]
    public async Task Signing_in_on_a_second_device_keeps_the_first_signed_in()
    {
        var (_, email) = await factory.AddUserAsync(factory.Acme);
        var laptop = await LoginAsync(email);
        var phone = await LoginAsync(email);

        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(laptop.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(phone.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task A_refresh_token_works_once_and_then_its_replacement_does()
    {
        var (_, email) = await factory.AddUserAsync(factory.Acme);
        var first = await LoginAsync(email);

        var refreshed = await RefreshAsync(first.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var second = (await refreshed.Content.ReadFromJsonAsync<TokenResponseDto>())!;
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(first.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(second.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ClientWith(second.AccessToken).GetAsync("/api/company/me")).StatusCode);
    }

    [Fact]
    public async Task The_response_gives_the_real_expiry_and_only_a_hash_is_stored()
    {
        var (id, email) = await factory.AddUserAsync(factory.Acme);
        var tokens = await LoginAsync(email);

        // It used to say 1 hour while the server kept the token for 7 days
        var inSevenDays = DateTime.UtcNow.AddDays(7);
        Assert.InRange(tokens.RefreshTokenExpiryTime, inSevenDays.AddMinutes(-5), inSevenDays.AddMinutes(1));

        var stored = await factory.WithDbAsync(db => db.RefreshTokens.AsNoTracking().SingleAsync(t => t.UserId == id));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tokens.RefreshToken))), stored.TokenHash);
        Assert.Equal(tokens.RefreshTokenExpiryTime, stored.ExpiresAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task An_expired_refresh_token_is_refused()
    {
        var (id, email) = await factory.AddUserAsync(factory.Acme);
        var tokens = await LoginAsync(email);
        await factory.WithDbAsync(db => db.RefreshTokens.Where(t => t.UserId == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.ExpiresAt, DateTime.UtcNow.AddMinutes(-1))));

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(tokens.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Signing_out_with_the_refresh_token_ends_only_that_device()
    {
        var (_, email) = await factory.AddUserAsync(factory.Acme);
        var laptop = await LoginAsync(email);
        var phone = await LoginAsync(email);

        // Holding the refresh token is enough: an expired access token mustn't stop someone signing out
        var signOut = await factory.CreateClient().PostAsJsonAsync("/api/auth/revoke-token", laptop.RefreshToken);

        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(laptop.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(phone.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Signing_out_without_a_token_ends_every_device()
    {
        var (_, email) = await factory.AddUserAsync(factory.Acme);
        var laptop = await LoginAsync(email);
        var phone = await LoginAsync(email);

        // What the frontend sends today: an access token and no body
        var signOut = await ClientWith(laptop.AccessToken).PostAsync("/api/auth/revoke-token", null);

        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(laptop.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(phone.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Signing_out_needs_a_refresh_token_or_an_access_token()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().PostAsync("/api/auth/revoke-token", null)).StatusCode);

        // An unknown token gets the same answer as a real one, so this can't be used to test tokens
        var unknown = await factory.CreateClient().PostAsJsonAsync("/api/auth/revoke-token", "not-a-real-token");
        Assert.Equal(HttpStatusCode.NoContent, unknown.StatusCode);
    }

    [Fact]
    public async Task Deactivating_a_user_signs_them_out_everywhere_and_stops_them_signing_in()
    {
        var (id, email) = await factory.AddUserAsync(factory.Acme);
        var laptop = await LoginAsync(email);
        var phone = await LoginAsync(email);
        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/user/{id}/deactivate", null)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(laptop.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(phone.RefreshToken)).StatusCode);
        Assert.False(await factory.WithDbAsync(db => db.RefreshTokens.AnyAsync(t => t.UserId == id)));

        // With the right password they're told why. A wrong one gets the usual answer, so nobody else learns it.
        var login = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, hashedPassword = GratiaApiFactory.Password });
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        Assert.Contains("deactivated", await login.Content.ReadAsStringAsync());
        var wrongPassword = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, hashedPassword = "Wrong-password-1" });
        Assert.Equal(HttpStatusCode.BadRequest, wrongPassword.StatusCode);
    }

    [Fact]
    public async Task Signing_in_ignores_case_and_spaces_around_the_email()
    {
        var (_, email) = await factory.AddUserAsync(factory.Acme);

        await LoginAsync($"  {email.ToUpperInvariant()} ");
    }

    [Fact]
    public async Task A_refresh_picks_up_a_changed_role()
    {
        var (id, email) = await factory.AddUserAsync(factory.Acme);
        var before = await LoginAsync(email);
        Assert.Equal(HttpStatusCode.Forbidden, (await ClientWith(before.AccessToken).GetAsync("/api/invitation/pending")).StatusCode);

        var admin = await factory.CreateClientForAsync(factory.AcmeAdminEmail);
        var promote = await admin.PutAsJsonAsync($"/api/user/{id}", new
        {
            id,
            fullName = "Temp User",
            email,
            jobTitle = "Lead",
            role = "Admin"
        });
        promote.EnsureSuccessStatusCode();

        var after = (await (await RefreshAsync(before.RefreshToken)).Content.ReadFromJsonAsync<TokenResponseDto>())!;
        Assert.Equal(HttpStatusCode.OK, (await ClientWith(after.AccessToken).GetAsync("/api/invitation/pending")).StatusCode);
    }

    private async Task<TokenResponseDto> LoginAsync(string email)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, hashedPassword = GratiaApiFactory.Password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TokenResponseDto>())!;
    }

    // The API takes the refresh token as a bare JSON string, as the frontend sends it
    private Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        factory.CreateClient().PostAsJsonAsync("/api/auth/refresh-token", refreshToken);

    private HttpClient ClientWith(string accessToken)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }
}
