namespace Gratia.Domain.Repositories
{
    /// <summary>
    /// Commits everything the repositories added, removed or changed during the current request
    /// in a single database transaction. Repositories never save on their own.
    /// </summary>
    public interface IUnitOfWork
    {
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Only for a use case that mixes immediate updates (such as <see cref="IUserRepository.TrySpendAvailablePointsAsync"/>)
        /// with tracked changes. Everything until <see cref="IDatabaseTransaction.CommitAsync"/>, SaveChangesAsync included,
        /// is one database transaction, and disposing it without committing rolls it all back.
        /// </summary>
        Task<IDatabaseTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    }

    public interface IDatabaseTransaction : IAsyncDisposable
    {
        Task CommitAsync(CancellationToken cancellationToken = default);
    }
}
