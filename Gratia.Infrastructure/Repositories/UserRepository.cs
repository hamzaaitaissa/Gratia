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
            if(user == null) throw new ArgumentNullException(nameof(user));
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

        public async Task<User> GetByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Email cannot be empty.", nameof(email));
            return await _gratiaDbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<User> GetByIdAsync(Guid id)
        {
            if (id == Guid.Empty) throw new ArgumentNullException("User id cannot be empty", nameof(id));
            var user = await _gratiaDbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
            return user;
        }

        public async Task<User> GetUserByRefreshTokenAsync(string refreshToken)
        {
            var user = await _gratiaDbContext.Users.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken);
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
