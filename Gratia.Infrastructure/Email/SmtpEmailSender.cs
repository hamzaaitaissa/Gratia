using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;

namespace Gratia.Infrastructure.Email
{
    public class SmtpEmailSender : IEmailSender
    {
        private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(30);

        private readonly EmailOptions _email;

        public SmtpEmailSender(IOptions<EmailOptions> emailOptions)
        {
            _email = emailOptions.Value;
        }

        public async Task SendAsync(string toEmail, EmailContent content, CancellationToken cancellationToken)
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_email.Email, _email.SenderName),
                Subject = content.Subject,
                SubjectEncoding = Encoding.UTF8,
                Body = content.HtmlBody,
                BodyEncoding = Encoding.UTF8,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            // UseDefaultCredentials must be set first: setting it clears Credentials
            using var smtpClient = new SmtpClient(_email.Host, _email.Port)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_email.Email, _email.Password)
            };

            // SmtpClient.Timeout only applies to the synchronous Send, so a stuck server would otherwise hold the request
            // (and the admin waiting on it) for as long as the connection stays open
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(SendTimeout);
            await smtpClient.SendMailAsync(message, timeout.Token);
        }
    }
}
