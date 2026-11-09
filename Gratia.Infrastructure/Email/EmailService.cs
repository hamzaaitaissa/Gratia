using Gratia.Application.Exceptions;
using Gratia.Application.Interfaces;
using Gratia.Application.Settings;
using Microsoft.Extensions.Options;

namespace Gratia.Infrastructure.Email
{
    // Builds each email from EmailTemplates and the frontend's links; IEmailSender delivers it
    public class EmailService : IEmailService
    {
        private readonly IEmailSender _sender;
        private readonly FrontendOptions _frontend;

        public EmailService(IEmailSender sender, IOptions<FrontendOptions> frontendOptions)
        {
            _sender = sender;
            _frontend = frontendOptions.Value;
        }

        public Task SendInvitationEmailAsync(string toEmail, string token, string companyName, string inviterName, string inviterEmail,
            CancellationToken cancellationToken = default) =>
            SendAsync(toEmail, EmailTemplates.Invitation(companyName, inviterName, inviterEmail, AcceptUrl(token)), cancellationToken);

        public Task SendCompanySetupEmailAsync(string toEmail, string founderName, string companyName, string token,
            CancellationToken cancellationToken = default) =>
            SendAsync(toEmail, EmailTemplates.CompanySetup(founderName, companyName, AcceptUrl(token)), cancellationToken);

        public Task SendAccountExistsEmailAsync(string toEmail, CancellationToken cancellationToken = default) =>
            SendAsync(toEmail, EmailTemplates.AccountExists(FrontendUrl("/login")), cancellationToken);

        public Task SendWorkspaceExistsEmailAsync(string toEmail, string companyName, CancellationToken cancellationToken = default) =>
            SendAsync(toEmail, EmailTemplates.WorkspaceExists(companyName), cancellationToken);

        public Task SendWelcomeEmailAsync(string toEmail, string fullName, string companyName, int monthlyAllowance,
            CancellationToken cancellationToken = default) =>
            SendAsync(toEmail, EmailTemplates.Welcome(fullName, companyName, monthlyAllowance, FrontendUrl("/login")), cancellationToken);

        private string AcceptUrl(string token) => FrontendUrl($"/accept-invitation/{Uri.EscapeDataString(token)}");

        private string FrontendUrl(string path) => _frontend.FrontendUrl.TrimEnd('/') + path;

        // Any failure other than the caller cancelling becomes EmailDeliveryException (503), so handlers can undo what
        // the email was about without knowing SMTP's exception types (#20)
        private async Task SendAsync(string toEmail, EmailContent content, CancellationToken cancellationToken)
        {
            try
            {
                await _sender.SendAsync(toEmail, content, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new EmailDeliveryException("The email couldn't be sent. Try again in a moment.", ex);
            }
        }
    }
}
