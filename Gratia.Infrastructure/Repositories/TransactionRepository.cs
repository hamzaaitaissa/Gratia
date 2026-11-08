using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;
using Gratia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Gratia.Infrastructure.Repositories
{
    public class TransactionRepository : ITransactionRepository
    {

        private readonly GratiaDbContext _gratiaContext;

        public TransactionRepository(GratiaDbContext gratiaDbContext)
        {
            _gratiaContext = gratiaDbContext;
        }

        public void Add(Transaction transaction)
        {
            _gratiaContext.Transactions.Add(transaction);
        }

        public async Task<IEnumerable<Transaction>> GetByCompanyIdAsync(Guid companyId)
        {
            return await _gratiaContext.Transactions.Where(t => t.CompanyId == companyId).OrderByDescending(t => t.CreatedDate).ToListAsync();
        }

        public async Task<Transaction?> GetByIdAsync(Guid id)
        {
            return await _gratiaContext.Transactions
                .Include(t => t.Sender)
                .Include(t => t.Receiver)
                .Include(t => t.Company)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<IEnumerable<Transaction>> GetByReceiverIdAsync(Guid receiverId)
        {
            return await _gratiaContext.Transactions
                .Where(t => t.ReceiverId == receiverId).Include(t => t.Sender)
                .Include(t => t.Receiver)
                .Include(t => t.Company)
                .OrderByDescending(t => t.CreatedDate).
                ToListAsync();
        }

        public async Task<IEnumerable<Transaction>> GetBySenderIdAsync(Guid senderId)
        {
            return await _gratiaContext.Transactions
                .Where(t => t.SenderId == senderId)
                .Include(t => t.Sender)
                .Include(t => t.Receiver)
                .Include(t => t.Company)
                .OrderByDescending(t => t.CreatedDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<Transaction>> GetCompanyTransactionHistoryAsync(Guid companyId, int page = 1, int pageSize = 10)
        {
            return await _gratiaContext.Transactions.Where(t => t.CompanyId == companyId)
                .Skip(page).Take(pageSize).ToListAsync();
        }

        public async Task<int> GetTotalTransactionCountAsync(Guid companyId)
        {
            return await _gratiaContext.Transactions.Where(t => t.CompanyId == companyId).CountAsync();
        }

        public async Task<IEnumerable<Transaction>> GetUserTransactionHistoryAsync(Guid userId)
        {
            return await _gratiaContext.Transactions
                            .Where(t => t.SenderId == userId || t.ReceiverId == userId)
                            .Include(t => t.Sender)
                            .Include(t => t.Receiver)
                            .Include(t => t.Company)
                            .OrderByDescending(t => t.CreatedDate)
                            .ToListAsync();
        }
    }
}
