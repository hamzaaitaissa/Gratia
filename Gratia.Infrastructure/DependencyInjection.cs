using Gratia.Application.Interfaces;
using Gratia.Application.Settings;
using Gratia.Domain.Repositories;
using Gratia.Infrastructure.Authentication;
using Gratia.Infrastructure.Data;
using Gratia.Infrastructure.Dns;
using Gratia.Infrastructure.Email;
using Gratia.Infrastructure.Repositories;
using Gratia.Infrastructure.Spreadsheets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using DnsClient;
using System.Text;

namespace Gratia.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            // Persistence
            services.AddDbContext<GratiaDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));
            services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<GratiaDbContext>());

            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<ICompanyRepository, CompanyRepository>();
            services.AddScoped<ITransactionRepository, TransactionRepository>();
            services.AddScoped<IInvitationRepository, InvitationRepository>();
            services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
            services.AddScoped<ILoginAttemptRepository, LoginAttemptRepository>();
            services.AddScoped<IDomainVerificationRepository, DomainVerificationRepository>();

            // Authentication. Only the API checks these settings at startup (.ValidateOnStart() in its Program.cs);
            // the Worker never issues tokens, so it can run without the signing key.
            services.AddOptions<JwtOptions>()
                .Bind(configuration.GetSection(JwtOptions.SectionName))
                .Validate(o => Encoding.UTF8.GetByteCount(o.Token) >= JwtOptions.MinimumKeyBytes,
                    "AppSettings:Token is missing or shorter than 64 bytes. Set it with " +
                    "'dotnet user-secrets set AppSettings:Token <key> --project Gratia.API' in development, " +
                    "or the AppSettings__Token environment variable elsewhere.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer) && !string.IsNullOrWhiteSpace(o.Audience),
                    "AppSettings:Issuer and AppSettings:Audience must be configured.")
                .Validate(o => o.AccessTokenMinutes > 0 && o.RefreshTokenDays > 0
                        && TimeSpan.FromMinutes(o.AccessTokenMinutes) < TimeSpan.FromDays(o.RefreshTokenDays),
                    "AppSettings:AccessTokenMinutes and AppSettings:RefreshTokenDays must be positive, and an access token " +
                    "must expire before the refresh token that renews it.");

            services.AddScoped<ITokenService, TokenService>();
            services.AddSingleton<IPasswordHashingService, PasswordHashingService>();

            // Email. Only the API sends mail, so only it checks these at startup (.ValidateOnStart() in its Program.cs).
            services.AddOptions<EmailOptions>()
                .Bind(configuration.GetSection(EmailOptions.SectionName))
                .Validate(o => o.IsValid(),
                    "EMAIL_CONFIGURATION needs HOST, PORT, a valid EMAIL and PASSWORD. Set EMAIL and PASSWORD with " +
                    "'dotnet user-secrets set EMAIL_CONFIGURATION:EMAIL <address> --project Gratia.API' (and :PASSWORD) in development, " +
                    "or the EMAIL_CONFIGURATION__EMAIL / EMAIL_CONFIGURATION__PASSWORD environment variables elsewhere.");
            services.AddOptions<FrontendOptions>()
                .Bind(configuration.GetSection(FrontendOptions.SectionName))
                .Validate(o => o.IsValid(),
                    "AppSettings:FrontendUrl must be the web app's absolute http(s) address, used for links in emails " +
                    "(the AppSettings__FrontendUrl environment variable outside Development).");
            services.AddTransient<IEmailSender, SmtpEmailSender>();
            services.AddTransient<IEmailService, EmailService>();

            // Bulk invitations (#54)
            services.AddSingleton<IInvitationSpreadsheet, InvitationSpreadsheet>();

            // Domain verification (#61). No cache: someone checking again after adding the record must get a fresh answer.
            // Only the API checks these settings at startup (.ValidateOnStart() in its Program.cs).
            services.AddOptions<DomainVerificationOptions>()
                .Bind(configuration.GetSection(DomainVerificationOptions.SectionName))
                .Validate(o => o.IsValid(),
                    "DomainVerification:NameServers must be IP addresses, optionally with a port (1.1.1.1:53), and " +
                    "DomainVerification:TimeoutSeconds between 1 and 30.");
            services.AddSingleton<IDnsQuery>(sp =>
            {
                var settings = sp.GetRequiredService<IOptions<DomainVerificationOptions>>().Value;
                var nameServers = settings.NameServers
                    .Select(s => DomainVerificationOptions.TryParse(s, out var endPoint) ? new NameServer(endPoint) : null)
                    .OfType<NameServer>()
                    .ToArray();
                var options = nameServers.Length == 0 ? new LookupClientOptions() : new LookupClientOptions(nameServers);
                options.UseCache = false;
                options.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
                options.Retries = 1;
                options.ThrowDnsErrors = false;
                return new LookupClient(options);
            });
            services.AddSingleton<IDomainVerifier, DnsDomainVerifier>();

            return services;
        }

        /// <summary>
        /// Development without SMTP credentials: write emails to the log instead of sending them.
        /// The log then holds working invitation links, so never call this outside Development.
        /// </summary>
        public static IServiceCollection UseEmailLogInsteadOfSmtp(this IServiceCollection services)
        {
            services.Replace(ServiceDescriptor.Transient<IEmailSender, LogEmailSender>());
            return services;
        }
    }
}
