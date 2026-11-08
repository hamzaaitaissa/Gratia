using Gratia.Application.Interfaces;
using Gratia.Domain.Repositories;
using Gratia.Infrastructure.Authentication;
using Gratia.Infrastructure.Data;
using Gratia.Infrastructure.Email;
using Gratia.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

            // Authentication. Only the API checks these settings at startup (.ValidateOnStart() in its Program.cs);
            // the Worker never issues tokens, so it can run without the signing key.
            services.AddOptions<JwtOptions>()
                .Bind(configuration.GetSection(JwtOptions.SectionName))
                .Validate(o => Encoding.UTF8.GetByteCount(o.Token) >= JwtOptions.MinimumKeyBytes,
                    "AppSettings:Token is missing or shorter than 64 bytes. Set it with " +
                    "'dotnet user-secrets set AppSettings:Token <key> --project Gratia.API' in development, " +
                    "or the AppSettings__Token environment variable elsewhere.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer) && !string.IsNullOrWhiteSpace(o.Audience),
                    "AppSettings:Issuer and AppSettings:Audience must be configured.");

            services.AddScoped<ITokenService, TokenService>();
            services.AddSingleton<IPasswordHashingService, PasswordHashingService>();

            // Email
            services.AddTransient<IEmailService, EmailService>();

            return services;
        }
    }
}
