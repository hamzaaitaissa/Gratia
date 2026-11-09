using System.Net;

namespace GratiaTests.Integration;

// The browser frontend runs on another origin, so it can only use the API through CORS (TASKS.md #29)
public sealed class CorsTests(GratiaApiFactory factory) : IClassFixture<GratiaApiFactory>
{
    private const string Frontend = "http://localhost:5173"; // AppSettings:FrontendUrl in GratiaApiFactory

    [Fact]
    public async Task The_frontend_may_call_the_api_with_a_token()
    {
        var preflight = await factory.CreateClient().SendAsync(Preflight("/api/user", Frontend));

        Assert.Equal(HttpStatusCode.NoContent, preflight.StatusCode);
        Assert.Equal(Frontend, AllowedOrigin(preflight));
        Assert.Contains("authorization", string.Join(",", preflight.Headers.GetValues("Access-Control-Allow-Headers")), StringComparison.OrdinalIgnoreCase);
        Assert.False(preflight.Headers.Contains("Access-Control-Allow-Credentials")); // tokens go in a header, not cookies
    }

    [Fact]
    public async Task Other_sites_may_not()
    {
        var preflight = await factory.CreateClient().SendAsync(Preflight("/api/user", "https://evil.test"));

        Assert.Null(AllowedOrigin(preflight));
    }

    [Fact]
    public async Task The_frontend_can_read_error_responses()
    {
        // Errors are written by the exception handler; the browser hides them from the page without the CORS header
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/invitation/token/not-a-real-token");
        request.Headers.Add("Origin", Frontend);

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(Frontend, AllowedOrigin(response));
    }

    [Fact]
    public async Task Configured_origins_replace_the_frontend_url()
    {
        // A path and trailing slash are fine: only the origin (scheme, host and port) is compared
        using var configured = factory.WithWebHostBuilder(builder => builder
            .UseSetting("Cors:AllowedOrigins:0", "https://app.gratia.test/some/path/")
            .UseSetting("Cors:AllowedOrigins:1", "https://admin.gratia.test:8443"));
        var client = configured.CreateClient();

        Assert.Equal("https://app.gratia.test", AllowedOrigin(await client.SendAsync(Preflight("/api/user", "https://app.gratia.test"))));
        Assert.Equal("https://admin.gratia.test:8443", AllowedOrigin(await client.SendAsync(Preflight("/api/user", "https://admin.gratia.test:8443"))));
        Assert.Null(AllowedOrigin(await client.SendAsync(Preflight("/api/user", Frontend))));
    }

    private static HttpRequestMessage Preflight(string path, string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        return request;
    }

    private static string? AllowedOrigin(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values) ? values.Single() : null;
}
