using System.Net;
using System.Net.Http.Json;
using Gratia.Application.DTOs.Token;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GratiaTests.Integration;

// Login and refresh are limited per client IP (#27). Every test request has the same (empty) IP, so each test runs on
// its own copy of the API with low limits; the shared factory's limits are high enough never to get in the way.
public sealed class RateLimitTests(GratiaApiFactory factory) : IClassFixture<GratiaApiFactory>
{
    [Fact]
    public async Task Login_attempts_are_limited_per_client()
    {
        await using var api = WithLimit("RateLimiting:LoginPerMinute", 2);
        var client = api.CreateClient();
        var wrong = new { email = factory.AcmeEmployeeEmail, hashedPassword = "Wrong-password-1" };

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/login", wrong)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/login", wrong)).StatusCode);

        // Even the right password: the limit is on attempts, not failures
        var limited = await client.PostAsJsonAsync("/api/auth/login", new { email = factory.AcmeEmployeeEmail, hashedPassword = GratiaApiFactory.Password });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.InRange(int.Parse(limited.Headers.GetValues("Retry-After").Single()), 1, 60);
        Assert.Equal("application/problem+json", limited.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Refreshes_are_limited_per_client()
    {
        await using var api = WithLimit("RateLimiting:RefreshPerMinute", 1);
        var client = api.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = factory.AcmeEmployeeEmail, hashedPassword = GratiaApiFactory.Password });
        var tokens = (await login.Content.ReadFromJsonAsync<TokenResponseDto>())!;

        var first = await client.PostAsJsonAsync("/api/auth/refresh-token", tokens.RefreshToken);
        var renewed = (await first.Content.ReadFromJsonAsync<TokenResponseDto>())!;
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/auth/refresh-token", renewed.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task A_rejected_request_tells_the_browser_when_to_retry()
    {
        await using var api = WithLimit("RateLimiting:LoginPerMinute", 1);
        var client = api.CreateClient();
        HttpRequestMessage FromTheFrontend() => new(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = factory.AcmeEmployeeEmail, hashedPassword = GratiaApiFactory.Password }),
            Headers = { { "Origin", "http://localhost:5173" } }
        };
        await client.SendAsync(FromTheFrontend());

        var response = await client.SendAsync(FromTheFrontend());

        // Retry-After isn't a header scripts can read on another origin unless the API exposes it
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Contains("Retry-After", response.Headers.GetValues("Access-Control-Expose-Headers").Single());
    }

    private WebApplicationFactory<Program> WithLimit(string setting, int limit) =>
        factory.WithWebHostBuilder(builder => builder.UseSetting(setting, limit.ToString()));
}
