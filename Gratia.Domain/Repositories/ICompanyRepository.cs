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

        // Runs immediately as one conditional UPDATE: counts one more invitation email in the company's window (#65),
        // starting a new window when the last one has ended, unless the window already has limit emails. Returns false,
        // changing nothing, at the limit. Checked and counted in one statement, so invitations sent at the same moment
        // can't go past it. Use it inside IUnitOfWork.BeginTransactionAsync, with the invitation it counts.
        Task<bool> TryCountInvitationAsync(Guid companyId, DateTime now, int limit, CancellationToken cancellationToken = default);

        // Runs immediately: an UPDATE that changes nothing but holds the company row's lock until the transaction ends.
        // Changes that must each see the other's result, such as removing admins (#63), then take turns. Use it inside
        // IUnitOfWork.BeginTransactionAsync, before reading what the change depends on.
        Task LockAsync(Guid companyId, CancellationToken cancellationToken = default);

        // Saved by IUnitOfWork.SaveChangesAsync
        void Add(Company company);
        void Remove(Company company);
    }
}
