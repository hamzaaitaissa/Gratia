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

        public async Task<Company?> GetByDomainAsync(string domain)
        {
            return await _gratiaDbContext.Companies.FirstOrDefaultAsync(c => c.Domain == domain);
        }

        public async Task<IReadOnlyList<Company>> GetAllForPointsResetAsync(CancellationToken cancellationToken = default)
        {
            return await _gratiaDbContext.Companies.AsNoTracking().ToListAsync(cancellationToken);
        }

        public async Task<bool> TryStartPointsPeriodAsync(Guid companyId, DateTime periodStart, CancellationToken cancellationToken = default)
        {
            // The check is in the WHERE clause, so a second job waits for the first one's row lock and then finds
            // the period already recorded
            var updated = await _gratiaDbContext.Companies
                .Where(c => c.Id == companyId && (c.PointsPeriodStartedAt == null || c.PointsPeriodStartedAt < periodStart))
                .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.PointsPeriodStartedAt, (DateTime?)periodStart), cancellationToken);
            return updated == 1;
        }
    }
}
