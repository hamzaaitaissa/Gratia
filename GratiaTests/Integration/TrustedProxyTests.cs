using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

namespace GratiaTests.Integration;

// Behind a reverse proxy (#45): the client's IP and scheme come from the proxy's X-Forwarded-For and X-Forwarded-Proto,
// and only from a proxy the API is told about. Each test runs on its own copy of the API that trusts 10.0.0.5, allows
// 2 sign-ins a minute per IP and knows an HTTPS port, so it redirects plain HTTP.
public sealed class TrustedProxyTests(GratiaApiFactory factory) : IClassFixture<GratiaApiFactory>
{
    private const string Proxy = "10.0.0.5";

    [Fact]
    public async Task Behind_the_proxy_each_client_has_its_own_limit()
    {
        await using var api = BehindProxy();

        // The proxy ends TLS and passes the request on over HTTP, saying it came in over HTTPS
        Assert.Equal(400, await SignInAsync(api, from: Proxy, forwardedFor: "198.51.100.1", forwardedProto: "https"));
        Assert.Equal(400, await SignInAsync(api, from: Proxy, forwardedFor: "198.51.100.1", forwardedProto: "https"));
        Assert.Equal(429, await SignInAsync(api, from: Proxy, forwardedFor: "198.51.100.1", forwardedProto: "https"));

        // Before #45, everyone behind the proxy shared its limit
        Assert.Equal(400, await SignInAsync(api, from: Proxy, forwardedFor: "198.51.100.2", forwardedProto: "https"));
    }

    [Fact]
    public async Task Anyone_else_cannot_pick_their_ip_with_the_header()
    {
        await using var api = BehindProxy();

        // A new X-Forwarded-For each time would be a new limit each time, if the API believed it
        Assert.Equal(400, await SignInAsync(api, from: "203.0.113.9", forwardedFor: "198.51.100.3", https: true));
        Assert.Equal(400, await SignInAsync(api, from: "203.0.113.9", forwardedFor: "198.51.100.4", https: true));
        Assert.Equal(429, await SignInAsync(api, from: "203.0.113.9", forwardedFor: "198.51.100.5", https: true));
    }

    [Fact]
    public async Task The_redirect_to_https_follows_the_scheme_the_client_used()
    {
        await using var api = BehindProxy();

        // Not redirected again and again when the client already used HTTPS
        Assert.Equal(400, await SignInAsync(api, from: Proxy, forwardedFor: "198.51.100.6", forwardedProto: "https"));
        Assert.Equal(307, await SignInAsync(api, from: Proxy, forwardedFor: "198.51.100.6", forwardedProto: "http"));

        // Only the proxy can say the request was HTTPS
        Assert.Equal(307, await SignInAsync(api, from: "203.0.113.10", forwardedFor: "198.51.100.7", forwardedProto: "https"));
    }

    [Fact]
    public async Task A_proxy_that_is_not_an_address_stops_the_api_from_starting()
    {
        // Rather than trusting nobody without a word, which would put every client back on the proxy's limit
        await using var api = factory.WithWebHostBuilder(builder => builder.UseSetting("ForwardedHeaders:KnownProxies:0", "proxy.internal"));

        var error = Assert.ThrowsAny<Exception>(() => api.CreateClient());
        Assert.Contains("ForwardedHeaders:KnownProxies must only contain an IP address", error.ToString());
    }

    private WebApplicationFactory<Program> BehindProxy() => factory.WithWebHostBuilder(builder =>
    {
        builder.UseSetting("ForwardedHeaders:KnownProxies:0", Proxy);
        builder.UseSetting("RateLimiting:LoginPerMinute", "2");
        builder.UseSetting("https_port", "443");
    });

    // A wrong password for an email of its own each time, so the per-email limit (#72) never answers instead
    private static async Task<int> SignInAsync(WebApplicationFactory<Program> api, string from, string forwardedFor,
        string? forwardedProto = null, bool https = false)
    {
        var context = await api.Server.SendAsync(request =>
        {
            request.Connection.RemoteIpAddress = IPAddress.Parse(from);
            request.Request.Scheme = https ? "https" : "http";
            request.Request.Method = HttpMethods.Post;
            request.Request.Path = "/api/auth/login";
            request.Request.Headers["X-Forwarded-For"] = forwardedFor;
            if (forwardedProto is not null)
                request.Request.Headers["X-Forwarded-Proto"] = forwardedProto;
            request.Request.ContentType = "application/json";
            request.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(
                $$"""{"email":"{{Guid.NewGuid():N}}@acme.test","hashedPassword":"Wrong-password-1"}"""));
        });
        return context.Response.StatusCode;
    }
}
