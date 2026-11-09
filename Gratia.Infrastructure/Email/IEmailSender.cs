namespace Gratia.Infrastructure.Email
{
    // How a finished email leaves the app: SMTP normally, the log in Development without SMTP credentials
    public interface IEmailSender
    {
        Task SendAsync(string toEmail, EmailContent content, CancellationToken cancellationToken);
    }
}
