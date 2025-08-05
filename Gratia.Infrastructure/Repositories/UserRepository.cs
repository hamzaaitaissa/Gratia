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
        public async Task<User> AddAsync(User user)
        {
            await _gratiaDbContext.Users.AddAsync(user);
            await _gratiaDbContext.SaveChangesAsync();
            return user;
        }

        public async Task DeleteAsync(Guid id)
        {
            var user = await _gratiaDbContext.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user != null)
            {
                _gratiaDbContext.Users.Remove(user);
                await _gratiaDbContext.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<User>> GetAllAsync()
        {
           var users = await _gratiaDbContext.Users.AsNoTracking().ToListAsync();
            return users;
        }

        public Task<User> GetByEmail(string email)
        {
            throw new NotImplementedException();
        }

        public async Task<User> GetByIdAsync(Guid id)
        {
            var user = await _gratiaDbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
            return user;
        }

        public async Task<User> UpdateAsync(User user)
        {
            _gratiaDbContext.Users.Update(user);
            await _gratiaDbContext.SaveChangesAsync();
            return user;
        }
    }
}
