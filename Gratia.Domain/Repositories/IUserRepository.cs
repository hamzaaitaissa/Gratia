using Gratia.Domain.Entities;
using Gratia.Domain.Enum;

namespace Gratia.Domain.Repositories
{
    // Users are deactivated, never deleted (#16). The directory reads (GetActiveInCompanyAsync, GetByIdInCompanyAsync)
    // leave deactivated users out; the lookups by id or email find them too, so their email stays taken.
    public interface IUserRepository
    {
        // The directory: active users in the company, by name
        Task<IEnumerable<User>> GetActiveInCompanyAsync(Guid companyId);
        Task<IEnumerable<User>> GetDeactivatedInCompanyAsync(Guid companyId);

        Task<User?> GetByIdAsync(Guid id);

        // Null when the user doesn't exist, belongs to another company or is deactivated, so callers can't tell these apart
        Task<User?> GetByIdInCompanyAsync(Guid id, Guid companyId);
        // The same, but finds deactivated users too (for reactivating them)
        Task<User?> GetByIdInCompanyIncludingDeactivatedAsync(Guid id, Guid companyId);

        // Expects a normalized email (EmailAddresses.Normalize)
        Task<User?> GetByEmailAsync(string email);
        // The same for many at once (an import, #54), untracked: the users with any of these emails, in any company
        Task<IReadOnlyList<User>> GetByEmailsAsync(IReadOnlyCollection<string> emails, CancellationToken cancellationToken = default);
        Task<bool> AnyInCompanyAsync(Guid companyId);

        // Whether the user is active, in that company, has that role and that session stamp: what an access token claims
        // (#73, #50). One primary-key lookup with nothing loaded, because it runs on every authenticated request.
        Task<bool> IsAccessTokenCurrentAsync(Guid id, Guid companyId, UserRole role, Guid sessionStamp, CancellationToken cancellationToken = default);

        // The user a forgotten-password link was sent to, by the SHA-256 of its token (#50), deactivated or not
        Task<User?> GetByPasswordResetTokenHashAsync(string tokenHash);

        // Whether the company has an active admin other than exceptUserId (#63)
        Task<bool> HasOtherActiveAdminAsync(Guid companyId, Guid exceptUserId, CancellationToken cancellationToken = default);

        // Saved by IUnitOfWork.SaveChangesAsync
        void Add(User user);

        // Refills a company's points to give (#19). Runs immediately as one UPDATE, outside the unit of work; use it with
        // ICompanyRepository.TryStartPointsPeriodAsync in one transaction. Returns the number of users updated.
        Task<int> ResetAvailablePointsAsync(Guid companyId, int points, CancellationToken cancellationToken = default);

        // Point transfers (#9, #11): each runs immediately as one atomic UPDATE that reads the balance in the database,
        // so concurrent sends can't spend the same points and concurrent gifts can't overwrite each other.
        // Use them inside IUnitOfWork.BeginTransactionAsync so they commit together with the rest of the use case.
        // TrySpendAvailablePointsAsync returns false, changing nothing, when fewer than amount points are left.
        Task<bool> TrySpendAvailablePointsAsync(Guid userId, int amount, CancellationToken cancellationToken = default);
        Task AddAcquiredPointsAsync(Guid userId, int amount, CancellationToken cancellationToken = default);
    }
}
