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
        bearerOptions.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = jwtOptions.Value.CreateSigningKey(),
            ValidateIssuer = false,
            ValidateAudience = false
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

// Signup sends email and creates rows, so each client IP gets a few attempts per hour
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(RateLimitPolicies.Signup, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = httpContext.RequestServices.GetRequiredService<IConfiguration>().GetValue("RateLimiting:SignupPerHour", 5),
                Window = TimeSpan.FromHours(1)
            }));
    options.OnRejected = (context, cancellationToken) =>
        context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context.HttpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many requests",
                Detail = "Too many attempts. Try again later."
            }
        });
});

// Web-only services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Errors: exceptions from services become ProblemDetails responses
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.Run();
