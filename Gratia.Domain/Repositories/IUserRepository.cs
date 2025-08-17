using Gratia.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Domain.Repositories
{
    public interface IUserRepository
    {
        Task<User> AddAsync(User user);
        Task<IEnumerable<User>> GetAllAsync();
        Task<User> GetByIdAsync(Guid id);
        Task DeleteAsync(Guid id);
        Task<User> UpdateAsync(User user);
        Task<User> GetByEmailAsync(string email);
        Task<User> GetUserByRefreshTokenAsync(string refreshToken);
    }
}