namespace Gratia.Domain.Repositories
{
    /// <summary>
    /// Commits everything the repositories added, removed or changed during the current request
    /// in a single database transaction. Repositories never save on their own.
    /// </summary>
    public interface IUnitOfWork
    {
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
