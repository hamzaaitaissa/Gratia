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
        Task<Transaction> CreateAsync(Transaction transaction);
        Task<Transaction> GetByIdAsync(Guid id);
        Task<IEnumerable<Transaction>> GetByCompanyIdAsync(Guid companyId);
        Task<IEnumerable<Transaction>> GetBySenderIdAsync(Guid senderId);
        Task<IEnumerable<Transaction>> GetByReceiverIdAsync(Guid receiverId);
        Task<IEnumerable<Transaction>> GetUserTransactionHistoryAsync(Guid userId);
        Task<Transaction> UpdateAsync(Transaction transaction);
        Task<bool> DeleteAsync(Guid id);
        Task<IEnumerable<Transaction>> GetCompanyTransactionHistoryAsync(Guid companyId, int page = 1, int pageSize = 10);
        Task<int> GetTotalTransactionCountAsync(Guid companyId);
    }
}
