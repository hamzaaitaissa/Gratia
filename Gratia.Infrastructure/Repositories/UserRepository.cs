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

        public void Remove(User user)
        {
            _gratiaDbContext.Users.Remove(user);
        }

        public async Task<IEnumerable<User>> GetAllAsync()
        {
            return await _gratiaDbContext.Users.AsNoTracking().ToListAsync();
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

        public async Task<User?> GetUserByRefreshTokenAsync(string refreshToken)
        {
            return await _gratiaDbContext.Users.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken);
        }

        public async Task<bool> AnyInCompanyAsync(Guid companyId)
        {
            return await _gratiaDbContext.Users.AnyAsync(u => u.CompanyId == companyId);
        }

        public async Task<int> ResetAvailablePointsAsync(int points)
        {
            return await _gratiaDbContext.Users
                .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.NumberOfPointsAvailable, points));
        }
    }
}
