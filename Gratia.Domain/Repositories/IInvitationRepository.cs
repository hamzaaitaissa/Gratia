using Gratia.Domain.Entities;

namespace Gratia.Domain.Repositories
{
    public interface IInvitationRepository
    {
        Task<CompanyInvitation?> GetByIdAsync(Guid id);
        Task<CompanyInvitation?> GetByTokenAsync(string token);
        Task<CompanyInvitation?> GetByEmailAndCompanyIdAsync(string email, Guid companyId);
        // The same for many at once (an import, #54), untracked: the company's unused, unexpired invitations to these emails
        Task<IReadOnlyList<CompanyInvitation>> GetActiveByEmailsAsync(Guid companyId, IReadOnlyCollection<string> emails,
            CancellationToken cancellationToken = default);
        // The admins' list: invitations they sent that haven't been accepted, expired ones included so they can be
        // resent (#75), newest first. Only the latest per address, and no founding setup links (those aren't resent here).
        Task<IEnumerable<CompanyInvitation>> GetPendingInvitationsByCompanyIdAsync(Guid companyId);
        Task<bool> HasActiveInvitationsAsync(Guid companyId);

        // The background sender (#55), across companies. The ids of up to max invitations whose email is due, oldest first.
        Task<IReadOnlyList<Guid>> GetDueEmailIdsAsync(DateTime now, int max, CancellationToken cancellationToken = default);

        // Runs immediately as one conditional UPDATE: takes the email for this sender until leaseUntil, unless another
        // sender has it or it's no longer due. Only the sender that gets true sends it.
        Task<bool> TryClaimEmailAsync(Guid id, DateTime now, DateTime leaseUntil, CancellationToken cancellationToken = default);

        // Untracked, with the company and the inviter, for writing the email
        Task<CompanyInvitation?> GetForEmailAsync(Guid id, CancellationToken cancellationToken = default);

        // Run immediately, and only if the invitation still waits for an email with sentToken: a resend meanwhile has
        // queued a newer one, which mustn't be marked sent or lose its link.
        // Sent: sentToken becomes the link (a resend's pending one replaces the old), valid until expiresAt.
        Task<bool> MarkEmailSentAsync(Guid id, string sentToken, DateTime now, DateTime expiresAt, CancellationToken cancellationToken = default);
        // Failed: tried again at nextAttemptAt, or with null, Failed for good (a pending new link is dropped; the old one stays)
        Task RecordEmailFailureAsync(Guid id, string sentToken, DateTime? nextAttemptAt, CancellationToken cancellationToken = default);

        // Saved by IUnitOfWork.SaveChangesAsync
        void Add(CompanyInvitation invitation);
        void Remove(CompanyInvitation invitation);
    }
}
