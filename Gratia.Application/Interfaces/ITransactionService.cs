using Gratia.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Application.Interfaces
{
    public interface ITransactionService
    {
        Task<Transaction> TradeAsync(Guid senderId, Guid receiverId, int points, Guid companyId);
        Task<Transaction?> GetTransactionAsync(Guid transactionId, Guid companyId);
        Task<IEnumerable<Transaction>> GetUserTransactionsAsync(Guid userId, Guid companyId);
        Task<IEnumerable<Transaction>> GetRecentTransactionsAsync(Guid companyId, int count);
    }
}
