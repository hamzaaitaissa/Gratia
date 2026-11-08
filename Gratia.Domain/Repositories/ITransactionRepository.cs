using Gratia.Domain.Entities;

namespace Gratia.Domain.Repositories
{
    public interface ITransactionRepository
    {
        Task<Transaction?> GetByIdAsync(Guid id);
        Task<IEnumerable<Transaction>> GetByCompanyIdAsync(Guid companyId);
        Task<IEnumerable<Transaction>> GetBySenderIdAsync(Guid senderId);
        Task<IEnumerable<Transaction>> GetByReceiverIdAsync(Guid receiverId);
        Task<IEnumerable<Transaction>> GetUserTransactionHistoryAsync(Guid userId);
        Task<IEnumerable<Transaction>> GetCompanyTransactionHistoryAsync(Guid companyId, int page = 1, int pageSize = 10);
        Task<int> GetTotalTransactionCountAsync(Guid companyId);

        // Saved by IUnitOfWork.SaveChangesAsync
        void Add(Transaction transaction);
    }
}
