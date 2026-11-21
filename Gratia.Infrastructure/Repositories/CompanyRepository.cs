using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;
using Gratia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Gratia.Infrastructure.Repositories
{
    public class CompanyRepository : ICompanyRepository
    {
        private readonly GratiaDbContext _gratiaDbContext;

        public CompanyRepository(GratiaDbContext gratiaDbContext)
        {
            _gratiaDbContext = gratiaDbContext;
        }

        public void Add(Company company)
        {
            _gratiaDbContext.Companies.Add(company);
        }

        public void Remove(Company company)
        {
            _gratiaDbContext.Companies.Remove(company);
        }

        public async Task<Company?> GetByIdAsync(Guid id)
        {
            return await _gratiaDbContext.Companies.FindAsync(id);
        }

        // Across companies (#78): signup looks for the domain's workspace, and has no caller
        public async Task<Company?> GetByDomainAsync(string domain)
        {
            return await _gratiaDbContext.Companies.AcrossCompanies().FirstOrDefaultAsync(c => c.Domain == domain);
        }

        // Across companies (#78): the background refill goes through all of them
        public async Task<IReadOnlyList<Company>> GetAllForPointsResetAsync(CancellationToken cancellationToken = default)
        {
            return await _gratiaDbContext.Companies.AcrossCompanies().AsNoTracking().ToListAsync(cancellationToken);
        }

        public async Task<bool> TryStartPointsPeriodAsync(Guid companyId, DateTime periodStart, CancellationToken cancellationToken = default)
        {
            // The check is in the WHERE clause, so a second job waits for the first one's row lock and then finds
            // the period already recorded. Across companies (#78): the background refill has no caller.
            var updated = await _gratiaDbContext.Companies
                .AcrossCompanies()
                .Where(c => c.Id == companyId && (c.PointsPeriodStartedAt == null || c.PointsPeriodStartedAt < periodStart))
                .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.PointsPeriodStartedAt, (DateTime?)periodStart), cancellationToken);
            return updated == 1;
        }

        public async Task<bool> TryCountInvitationAsync(Guid companyId, DateTime now, int limit, CancellationToken cancellationToken = default)
        {
            // Every SET reads the row as it was, so both see whether the window had ended
            var ended = now - Company.InvitationWindow;
            var updated = await _gratiaDbContext.Companies
                .Where(c => c.Id == companyId
                    && (c.InvitationWindowStart == null || c.InvitationWindowStart <= ended || c.InvitationsInWindow < limit))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(c => c.InvitationsInWindow,
                        c => c.InvitationWindowStart == null || c.InvitationWindowStart <= ended ? 1 : c.InvitationsInWindow + 1)
                    .SetProperty(c => c.InvitationWindowStart,
                        c => c.InvitationWindowStart == null || c.InvitationWindowStart <= ended ? now : c.InvitationWindowStart),
                    cancellationToken);
            return updated == 1;
        }

        public async Task LockAsync(Guid companyId, CancellationToken cancellationToken = default)
        {
            // SQL Server takes the exclusive row lock even though the value doesn't change, and other writers to the row
            // wait for the commit (checked on LocalDB). SQLite locks the whole database for a write.
            await _gratiaDbContext.Companies
                .Where(c => c.Id == companyId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.Name, c => c.Name), cancellationToken);
        }
    }
}
