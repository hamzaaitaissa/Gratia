using Gratia.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Domain.Repositories
{
    public interface ICompanyRepository
    {
        Task<Company> AddAsync(Company company);
        Task<Company> UpdateAsync(Company company);
        Task DeleteAsync(Guid Id);
        Task<Company> GetAsync(Guid Id);
        Task<IEnumerable<Company>> GetAllAsync();
    }
}
