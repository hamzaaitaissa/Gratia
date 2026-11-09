namespace Gratia.Application.Interfaces
{
    // Every method throws EmailDeliveryException when the email can't be sent (#20)
    public interface IEmailService
    {
        // An invitation to join an existing company. It names the inviter, so the recipient can judge it.
        Task SendInvitationEmailAsync(string toEmail, string token, string companyName, string inviterName, string inviterEmail,
            CancellationToken cancellationToken = default);

        // The founder's link to finish setting up a new company at signup
        Task SendCompanySetupEmailAsync(string toEmail, string founderName, string companyName, string token,
            CancellationToken cancellationToken = default);

        // Signup notices. The signup response is the same in every case, so only the mailbox owner learns
        // that the address already has an account, or that the company already has a workspace.
        Task SendAccountExistsEmailAsync(string toEmail, CancellationToken cancellationToken = default);
        Task SendWorkspaceExistsEmailAsync(string toEmail, string companyName, CancellationToken cancellationToken = default);

        Task SendWelcomeEmailAsync(string toEmail, string fullName, string companyName, int monthlyAllowance,
            CancellationToken cancellationToken = default);
    }
}
