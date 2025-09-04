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

        public async Task<Transaction> CreateAsync(Transaction transaction)
        {
            await _gratiaContext.Transactions.AddAsync(transaction);
            await _gratiaContext.SaveChangesAsync();
            return transaction;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var transaction = await _gratiaContext.Transactions.FindAsync(id);
            if (transaction == null) return false;

            _gratiaContext.Transactions.Remove(transaction);
            await _gratiaContext.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<Transaction>> GetByCompanyIdAsync(Guid companyId)
        {
            return await _gratiaContext.Transactions.Where(t => t.CompanyId == companyId).OrderByDescending(t => t.CreatedDate).ToListAsync();
        }

        public async Task<Transaction> GetByIdAsync(Guid id)
        {
            var transaction = await _gratiaContext.Transactions
                .Include(t => t.Sender)
                .Include(t => t.Receiver)
                .Include(t => t.Company)
                .FirstOrDefaultAsync(t => t.Id == id)
                ;

            if (transaction is null)
                throw new KeyNotFoundException($"Transaction {id} not found.");

            return transaction;
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

        public async Task<Transaction> UpdateAsync(Transaction transaction)
        {
            _gratiaContext.Transactions.Update(transaction);
            await _gratiaContext.SaveChangesAsync();
            return transaction;
        }
    }
}
