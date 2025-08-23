using Gratia.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Domain.Repositories
{
    public interface ITransactionRepository
    {
        Task<Transaction> AddAsync(Transaction transaction);
        Task<Transaction?> GetByIdAsync(Guid id, Guid CompanyId);
        Task<IEnumerable<Transaction>> GetByUserIdAsync(Guid userId, Guid CompanyId);
        Task<IEnumerable<Transaction>> GetByCompanyIdAsync(Guid companyId);
        Task<IEnumerable<Transaction>> GetRecentAsync(Guid companyId, int count);
    }
}
