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

        public async Task<IEnumerable<Company>> GetAllAsync()
        {
            return await _gratiaDbContext.Companies.AsNoTracking().ToListAsync();
        }

        public async Task<Company?> GetByIdAsync(Guid id)
        {
            return await _gratiaDbContext.Companies.FindAsync(id);
        }
    }
}
