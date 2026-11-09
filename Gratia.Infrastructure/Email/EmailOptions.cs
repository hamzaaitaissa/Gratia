using System.Net.Mail;

namespace Gratia.Infrastructure.Email
{
    public sealed class EmailOptions
    {
        // Kept as "EMAIL_CONFIGURATION" so the existing appsettings and user-secrets keys keep working
        public const string SectionName = "EMAIL_CONFIGURATION";

        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 587;

        // The address mail is sent from, which is also the SMTP user name
        public string Email { get; set; } = string.Empty;

        // Secret: user-secrets in development, the EMAIL_CONFIGURATION__PASSWORD environment variable elsewhere
        public string Password { get; set; } = string.Empty;

        public string SenderName { get; set; } = "Gratia";

        public bool IsValid() =>
            !string.IsNullOrWhiteSpace(Host)
            && Port is > 0 and <= 65535
            && MailAddress.TryCreate(Email, out _)
            && !string.IsNullOrEmpty(Password);
    }
}
