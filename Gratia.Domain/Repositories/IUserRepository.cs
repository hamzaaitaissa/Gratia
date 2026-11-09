using Gratia.Domain.Entities;

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
        Task<bool> AnyInCompanyAsync(Guid companyId);

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
