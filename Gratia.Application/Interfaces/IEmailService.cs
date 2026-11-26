namespace Gratia.Application.Interfaces
{
    // Who verified a company's domain (#61), for the notice to its other admins
    public enum DomainVerifiedBy
    {
        // One of its admins
        Admin,
        // A member, who became an admin by it
        MemberMadeAdmin,
        // Someone without an account, who was invited as an admin by it
        InvitedAsAdmin
    }

    // Every method throws EmailDeliveryException when the email can't be sent (#20)
    public interface IEmailService
    {
        // An invitation to join an existing company. It names the inviter, so the recipient can judge it.
        Task SendInvitationEmailAsync(string toEmail, string token, string companyName, string inviterName, string inviterEmail,
            CancellationToken cancellationToken = default);

        // Instead of the invitation, when the address already has an account in another workspace (#61). An address
        // belongs to one workspace, but the admin isn't told: who has a Gratia account is only the address owner's business.
        Task SendInvitationAccountElsewhereEmailAsync(string toEmail, string companyName, string inviterName, string inviterEmail,
            CancellationToken cancellationToken = default);

        // The founder's link to finish setting up a new company at signup
        Task SendCompanySetupEmailAsync(string toEmail, string founderName, string companyName, string token,
            CancellationToken cancellationToken = default);

        // Signup notices. The signup response is the same in every case, so only the mailbox owner learns
        // that the address already has an account, or that the company already has a workspace. The workspace notice has
        // a link (claimToken) for whoever manages the domain to claim the workspace through DNS (#61).
        Task SendAccountExistsEmailAsync(string toEmail, CancellationToken cancellationToken = default);
        Task SendWorkspaceExistsEmailAsync(string toEmail, string companyName, string domain, string claimToken,
            CancellationToken cancellationToken = default);

        Task SendWelcomeEmailAsync(string toEmail, string fullName, string companyName, int monthlyAllowance,
            CancellationToken cancellationToken = default);

        // Forgotten password (#50). The answer to the request is the same either way, so only the mailbox owner learns
        // whether the address has an active account: it gets the link, or the notice that there's none.
        Task SendPasswordResetEmailAsync(string toEmail, string fullName, string token, CancellationToken cancellationToken = default);
        Task SendPasswordResetUnavailableEmailAsync(string toEmail, CancellationToken cancellationToken = default);

        // Security notices (#50): the password was changed or reset, or an admin changed the sign-in email (to the old address)
        Task SendPasswordChangedEmailAsync(string toEmail, string fullName, CancellationToken cancellationToken = default);
        Task SendEmailChangedEmailAsync(string oldEmail, string newEmail, string companyName, CancellationToken cancellationToken = default);

        // To a company's other admins: someone proved they manage its domain (#61), which verified it or made them an admin
        Task SendDomainVerifiedEmailAsync(string toEmail, string companyName, string domain, string verifiedByEmail, DomainVerifiedBy by,
            CancellationToken cancellationToken = default);
    }
}
