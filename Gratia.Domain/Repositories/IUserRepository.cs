using Gratia.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Domain.Repositories
{
    internal interface IUserRepository
    {
        Task<User> AddAsync(User user);
        Task<IEnumerable<User>> GetAllAsync();
        Task<User> GetByIdAsync(int id);
        Task DeleteAsync(int id);
    }
}
