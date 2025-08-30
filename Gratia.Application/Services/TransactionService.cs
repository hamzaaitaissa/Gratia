using Gratia.Application.Interfaces;
using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Application.Services
{
    public class TransactionService : ITransactionService
    {
        private readonly ITransactionRepository _transactionRepository;

        public TransactionService(ITransactionRepository transactionRepository)
        {
            _transactionRepository = transactionRepository;
        }

        public Task<IEnumerable<Transaction>> GetRecentTransactionsAsync(Guid companyId, int count)
        {
            throw new NotImplementedException();
        }

        public Task<Transaction?> GetTransactionAsync(Guid transactionId, Guid companyId)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Transaction>> GetUserTransactionsAsync(Guid userId, Guid companyId)
        {
            throw new NotImplementedException();
        }

        public Task<Transaction> TradeAsync(Guid senderId, Guid receiverId, int points, Guid companyId)
        {
            throw new NotImplementedException();
        }
    }
}
