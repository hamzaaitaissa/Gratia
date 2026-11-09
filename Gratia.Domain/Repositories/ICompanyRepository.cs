using Gratia.Domain.Entities;

namespace Gratia.Domain.Repositories
{
    public interface ICompanyRepository
    {
        Task<Company?> GetByIdAsync(Guid id);
        Task<Company?> GetByDomainAsync(string domain);

        // Every company, untracked. Only for the points reset job, never for a request: requests see one company.
        Task<IReadOnlyList<Company>> GetAllForPointsResetAsync(CancellationToken cancellationToken = default);

        // Runs immediately as one conditional UPDATE: records that the period starting at periodStart has been
        // refilled, unless that's already recorded. Of two reset jobs running at once, only the one that gets true
        // may refill, so a period is refilled once. Use it inside IUnitOfWork.BeginTransactionAsync, with the refill.
        Task<bool> TryStartPointsPeriodAsync(Guid companyId, DateTime periodStart, CancellationToken cancellationToken = default);

        // Saved by IUnitOfWork.SaveChangesAsync
        void Add(Company company);
        void Remove(Company company);
    }
}
