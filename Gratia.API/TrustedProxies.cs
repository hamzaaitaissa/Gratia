using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Gratia.API
{
    /// <summary>
    /// Behind a reverse proxy or load balancer (TASKS.md #45), every connection comes from the proxy. The client's IP and
    /// scheme are then read from the proxy's X-Forwarded-For and X-Forwarded-Proto headers, so the per-IP rate limits
    /// count each client, not the proxy, and HTTPS redirection sees the request the client made.
    /// <para>
    /// The headers are only believed from the proxies listed in <c>ForwardedHeaders:KnownProxies</c> (addresses) and
    /// <c>ForwardedHeaders:KnownNetworks</c> (CIDR ranges, e.g. a Docker network), and from loopback, which ASP.NET trusts
    /// by default. From anyone else they're ignored: a client could otherwise pick a new IP, and a fresh rate limit, with
    /// every request.
    /// </para>
    /// Only the last hop is read (ForwardLimit 1), which fits one proxy in front of the API.
    /// </summary>
    public static class TrustedProxies
    {
        public const string SectionName = "ForwardedHeaders";

        public static IServiceCollection AddTrustedProxies(this IServiceCollection services)
        {
            services.AddOptions<ForwardedHeadersOptions>().Configure<IConfiguration>((options, configuration) =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

                foreach (var proxy in Read(configuration, "KnownProxies"))
                    options.KnownProxies.Add(IPAddress.TryParse(proxy, out var address)
                        ? address
                        : throw Invalid("KnownProxies", proxy, "an IP address such as 10.0.0.5"));

                foreach (var network in Read(configuration, "KnownNetworks"))
                    options.KnownIPNetworks.Add(System.Net.IPNetwork.TryParse(network, out var range)
                        ? range
                        : throw Invalid("KnownNetworks", network, "a CIDR range such as 172.18.0.0/16"));
            });

            return services;
        }

        private static string[] Read(IConfiguration configuration, string key) =>
            configuration.GetSection($"{SectionName}:{key}").Get<string[]>() ?? [];

        private static InvalidOperationException Invalid(string key, string value, string expected) =>
            new($"{SectionName}:{key} must only contain {expected}; '{value}' isn't one.");
    }
}
