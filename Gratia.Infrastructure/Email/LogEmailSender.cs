using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Gratia.Infrastructure.Email
{
    /// <summary>
    /// Development only, when SMTP isn't configured: writes each email to the log instead of sending it, so the
    /// invitation and signup flows can be tried without an email account. The log then contains working links,
    /// which is why this is never used outside Development.
    /// </summary>
    public partial class LogEmailSender : IEmailSender
    {
        private readonly ILogger<LogEmailSender> _logger;

        public LogEmailSender(ILogger<LogEmailSender> logger)
        {
            _logger = logger;
        }

        public Task SendAsync(string toEmail, EmailContent content, CancellationToken cancellationToken)
        {
            _logger.LogWarning("SMTP isn't configured, so this email was logged instead of sent.\nTo: {To}\nSubject: {Subject}\n{Body}",
                toEmail, content.Subject, ToPlainText(content.HtmlBody));
            return Task.CompletedTask;
        }

        private static string ToPlainText(string html)
        {
            var withLinks = LinkRegex().Replace(html, "$2 ($1)");
            var text = TagRegex().Replace(withLinks, string.Empty);
            return WebUtility.HtmlDecode(text).Trim();
        }

        [GeneratedRegex("<a [^>]*href=\"([^\"]*)\"[^>]*>(.*?)</a>")]
        private static partial Regex LinkRegex();

        [GeneratedRegex("<[^>]+>")]
        private static partial Regex TagRegex();
    }
}
