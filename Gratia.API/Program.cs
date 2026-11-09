using Gratia.API.Middleware;
using Gratia.API.Services;
using Gratia.Application;
using Gratia.Application.Interfaces;
using Gratia.Application.Settings;
using Gratia.Infrastructure;
using Gratia.Infrastructure.Authentication;
using Gratia.Infrastructure.Email;
using Gratia.API;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Application and infrastructure layers
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

// JWT authentication. The API issues and validates tokens, so it refuses to start without valid JWT settings
// (the signing key comes from user-secrets in development, the AppSettings__Token environment variable elsewhere)
builder.Services.AddOptions<JwtOptions>().ValidateOnStart();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearerOptions, jwtOptions) =>
    {
        // Only tokens this API issued, for this API, signed the way TokenService signs them (#25)
        bearerOptions.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = jwtOptions.Value.CreateSigningKey(),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha512],
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Value.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Value.Audience,
            ValidateLifetime = true,
            // The default is 5 minutes. This API issues and checks its own tokens, so clocks can't drift far apart.
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

// Invitations and signup depend on email, so the API refuses to start without valid email settings and FrontendUrl.
// The one exception: in Development without SMTP credentials, emails (with their links) are written to the log instead.
builder.Services.AddOptions<FrontendOptions>().ValidateOnStart();
var smtpConfigured = builder.Configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>()?.IsValid() == true;
if (builder.Environment.IsDevelopment() && !smtpConfigured)
    builder.Services.UseEmailLogInsteadOfSmtp();
else
    builder.Services.AddOptions<EmailOptions>().ValidateOnStart();

// Anonymous endpoints that cost something, per client IP (RateLimitPolicies). Behind a proxy every client has the
// proxy's IP until ForwardedHeaders is set up (#45).
static RateLimitPartition<string> PerClientIp(HttpContext httpContext, string limitSetting, int defaultLimit, TimeSpan window) =>
    RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = httpContext.RequestServices.GetRequiredService<IConfiguration>().GetValue(limitSetting, defaultLimit),
            Window = window
        });

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(RateLimitPolicies.Signup, httpContext => PerClientIp(httpContext, "RateLimiting:SignupPerHour", 5, TimeSpan.FromHours(1)));
    options.AddPolicy(RateLimitPolicies.Login, httpContext => PerClientIp(httpContext, "RateLimiting:LoginPerMinute", 20, TimeSpan.FromMinutes(1)));
    options.AddPolicy(RateLimitPolicies.Refresh, httpContext => PerClientIp(httpContext, "RateLimiting:RefreshPerMinute", 60, TimeSpan.FromMinutes(1)));
    options.OnRejected = (context, cancellationToken) =>
    {
        // Seconds until the window resets, so a client knows when to retry
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

        return context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context.HttpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many requests",
                Detail = "Too many attempts. Try again later."
            }
        });
    };
});

// Web-only services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// The monthly points refill, on each company's own day (#19); PointsReset:Enabled=false turns it off
builder.Services.AddHostedService<PointsResetService>();

// Errors: exceptions from services become ProblemDetails responses
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Enums go over the wire as names ("Admin"), and requests can't use numbers (#52). The HTTP JSON options are set
// too, because the OpenAPI document reads those rather than the MVC ones.
static void UseEnumNames(JsonSerializerOptions json) => json.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
builder.Services.AddControllers().AddJsonOptions(options => UseEnumNames(options.JsonSerializerOptions));
builder.Services.ConfigureHttpJsonOptions(options => UseEnumNames(options.SerializerOptions));
builder.Services.AddOpenApi();

// The browser frontend is on another origin (TASKS.md #29)
builder.Services.AddFrontendCors();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

// Before authentication, so preflight requests are answered without a token, and before the rate limiter, so a
// 429 still carries the CORS headers the browser needs to read it
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.Run();
