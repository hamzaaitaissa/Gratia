using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;
using Gratia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Infrastructure.Repositories
{
    public class TransactionRepository : ITransactionRepository
    {

        private readonly GratiaDbContext _grainContext;

        public TransactionRepository(GratiaDbContext gratiaDbContext)
        {
            _grainContext = gratiaDbContext;
        }

        public async Task<Transaction> AddAsync(Transaction transaction)
        {
            if(transaction == null) throw new ArgumentNullException(nameof(transaction));
            await _grainContext.Transactions.AddAsync(transaction);
            await _grainContext.SaveChangesAsync();
            return transaction;
        }

        public async Task<IEnumerable<Transaction>> GetByCompanyIdAsync(Guid companyId)
        {
            var transactions = await _grainContext.Transactions.AsNoTracking().Where(t => t.CompanyId == companyId).ToListAsync();
            return transactions;
        }

        public async Task<Transaction?> GetByIdAsync(Guid id, Guid companyId)
        {
            return await _grainContext.Transactions.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == companyId);
        }

        public Task<IEnumerable<Transaction>> GetByUserIdAsync(Guid userId,Guid CompanyId)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Transaction>> GetRecentAsync(Guid companyId, int count)
        {
            throw new NotImplementedException();
        }
    }
}
