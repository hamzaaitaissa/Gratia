using Gratia.Domain.Entities;

namespace Gratia.Domain.Repositories
{
    // Sign-in attempts per email (#72). Everything but Add runs immediately.
    public interface ILoginAttemptRepository
    {
        // Saved by IUnitOfWork.SaveChangesAsync
        void Add(LoginAttempt attempt);

        // When the email's other attempts made after `since` were made, oldest first
        Task<IReadOnlyList<DateTime>> GetTimesSinceAsync(string email, DateTime since, Guid exceptId, CancellationToken cancellationToken = default);

        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        Task DeleteForEmailAsync(string email, CancellationToken cancellationToken = default);

        // Housekeeping across all emails: attempts made at or before `cutoff` no longer count
        Task DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken = default);
    }
}
