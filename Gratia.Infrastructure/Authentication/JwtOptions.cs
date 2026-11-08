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

        public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(Token));
    }
}
