using Gratia.Application.Settings;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Gratia.API
{
    /// <summary>
    /// The browser origins allowed to call the API (TASKS.md #29): <c>Cors:AllowedOrigins</c> when it's set, otherwise
    /// the origin of <c>AppSettings:FrontendUrl</c>. Tokens travel in the Authorization header rather than cookies,
    /// so credentials aren't allowed.
    /// </summary>
    public static class FrontendCors
    {
        public const string SectionName = "Cors:AllowedOrigins";

        public static IServiceCollection AddFrontendCors(this IServiceCollection services)
        {
            services.AddCors();

            // Configured lazily, once FrontendOptions has been validated at startup
            services.AddOptions<CorsOptions>()
                .Configure<IConfiguration, IOptions<FrontendOptions>>((cors, configuration, frontend) =>
                {
                    var origins = AllowedOrigins(configuration, frontend.Value);
                    cors.AddDefaultPolicy(policy => policy
                        .WithOrigins(origins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        // So the frontend can tell how long to wait after a 429 (#27)
                        .WithExposedHeaders(HeaderNames.RetryAfter)
                        .SetPreflightMaxAge(TimeSpan.FromHours(1)));
                });

            return services;
        }

        private static string[] AllowedOrigins(IConfiguration configuration, FrontendOptions frontend)
        {
            var configured = configuration.GetSection(SectionName).Get<string[]>() ?? [];
            var urls = configured.Length > 0 ? configured : [frontend.FrontendUrl];
            return urls.Select(ToOrigin).Distinct().ToArray();
        }

        // An origin is scheme://host[:port], with no path or trailing slash, or it never matches the browser's Origin header
        private static string ToOrigin(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                ? uri.GetLeftPart(UriPartial.Authority)
                : throw new InvalidOperationException($"{SectionName} must only contain absolute http(s) URLs; '{url}' isn't one.");
    }
}
