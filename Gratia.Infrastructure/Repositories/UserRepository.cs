using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;
using Gratia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Gratia.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly GratiaDbContext _gratiaDbContext;

        public UserRepository(GratiaDbContext gratiaDbContext)
        {
            _gratiaDbContext = gratiaDbContext;
        }

        public void Add(User user)
        {
            _gratiaDbContext.Users.Add(user);
        }

        public async Task<IEnumerable<User>> GetActiveInCompanyAsync(Guid companyId)
        {
            return await _gratiaDbContext.Users
                .AsNoTracking()
                .Where(u => u.CompanyId == companyId && u.DeactivatedAt == null)
                .OrderBy(u => u.FullName)
                .ToListAsync();
        }

        public async Task<IEnumerable<User>> GetDeactivatedInCompanyAsync(Guid companyId)
        {
            return await _gratiaDbContext.Users
                .AsNoTracking()
                .Where(u => u.CompanyId == companyId && u.DeactivatedAt != null)
                .OrderBy(u => u.FullName)
                .ToListAsync();
        }

        // Single-entity reads are tracked so services can change them and save through the unit of work
        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _gratiaDbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<User?> GetByIdAsync(Guid id)
        {
            return await _gratiaDbContext.Users.FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<User?> GetByIdInCompanyAsync(Guid id, Guid companyId)
        {
            return await _gratiaDbContext.Users.FirstOrDefaultAsync(u => u.Id == id && u.CompanyId == companyId && u.DeactivatedAt == null);
        }

        public async Task<User?> GetByIdInCompanyIncludingDeactivatedAsync(Guid id, Guid companyId)
        {
            return await _gratiaDbContext.Users.FirstOrDefaultAsync(u => u.Id == id && u.CompanyId == companyId);
        }

        public async Task<bool> AnyInCompanyAsync(Guid companyId)
        {
            return await _gratiaDbContext.Users.AnyAsync(u => u.CompanyId == companyId);
        }

        // Deactivated users too, so they have this month's allowance if they're reactivated
        public async Task<int> ResetAvailablePointsAsync(Guid companyId, int points, CancellationToken cancellationToken = default)
        {
            return await _gratiaDbContext.Users
                .Where(u => u.CompanyId == companyId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.NumberOfPointsAvailable, points), cancellationToken);
        }

        public async Task<bool> TrySpendAvailablePointsAsync(Guid userId, int amount, CancellationToken cancellationToken = default)
        {
            // The balance check is in the WHERE clause, so it sees the committed balance, and the row lock makes a
            // concurrent send wait for this one and then check again
            var updated = await _gratiaDbContext.Users
                .Where(u => u.Id == userId && u.NumberOfPointsAvailable >= amount)
                .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.NumberOfPointsAvailable, u => u.NumberOfPointsAvailable - amount),
                    cancellationToken);
            return updated == 1;
        }

        public async Task AddAcquiredPointsAsync(Guid userId, int amount, CancellationToken cancellationToken = default)
        {
            await _gratiaDbContext.Users
                .Where(u => u.Id == userId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.NumberOfPointsAcquired, u => u.NumberOfPointsAcquired + amount),
                    cancellationToken);
        }
    }
}
