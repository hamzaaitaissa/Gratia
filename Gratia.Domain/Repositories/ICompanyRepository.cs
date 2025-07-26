using Gratia.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Domain.Repositories
{
    internal interface ICompanyRepository
    {
        Task AddAsync(Company company);
        Task UpdateAsync(Company company);
        Task DeleteAsync();
    }
}
