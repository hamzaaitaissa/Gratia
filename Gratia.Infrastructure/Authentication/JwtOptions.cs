using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Gratia.Infrastructure.Authentication
{
    public class JwtOptions
    {
        // Kept under "AppSettings" so the existing appsettings and user-secrets keys keep working
        public const string SectionName = "AppSettings";

        // HS512 needs a signing key of at least 512 bits
        public const int MinimumKeyBytes = 64;

        // Secret: user-secrets in development, the AppSettings__Token environment variable elsewhere
        public string Token { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;

        // How long an access token works, and how long a device stays signed in without using the app (#15).
        // Every refresh starts a new RefreshTokenDays period.
        public int AccessTokenMinutes { get; set; } = 60;
        public int RefreshTokenDays { get; set; } = 7;

        public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(Token));
    }
}
