using Gratia.Domain.Entities;

namespace Gratia.Domain.Repositories
{
    // Domain verification (#61). The reads are tracked, for changing and saving through the unit of work.
    public interface IDomainVerificationRepository
    {
        // The member's own, if they've started one. By account, never by email: admins change members' emails.
        Task<DomainVerification?> GetForMemberAsync(Guid companyId, Guid userId, CancellationToken cancellationToken = default);

        // The claim of someone without an account, if they've started one. Expects a normalized email.
        Task<DomainVerification?> GetClaimAsync(Guid companyId, string email, CancellationToken cancellationToken = default);

        // The claim an emailed link belongs to, by the SHA-256 of its token, with its company. Expired links too: the caller checks.
        Task<DomainVerification?> GetByLinkTokenHashAsync(string linkTokenHash, CancellationToken cancellationToken = default);

        // Runs immediately as one conditional UPDATE: spends the record that was looked up, unless it's already spent or
        // replaced. Only the request that gets true may act on it, so a record works once however many checks run at the
        // same moment. Use it inside IUnitOfWork.BeginTransactionAsync, with what it grants.
        Task<bool> TrySpendAsync(Guid id, string recordToken, DateTime now, CancellationToken cancellationToken = default);

        // Runs immediately: deletes the member's own, when their email changes. Use it inside IUnitOfWork.BeginTransactionAsync.
        Task DeleteForMemberAsync(Guid companyId, Guid userId, CancellationToken cancellationToken = default);

        // Saved by IUnitOfWork.SaveChangesAsync
        void Add(DomainVerification verification);
    }
}
