using Gratia.Application.DTOs.Transaction;
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
        Task<ReadTransactionDto> CreateTransactionAsync(Guid senderId, CreateTransactionDto createReadTransactionDto);
        Task<ReadTransactionDto> GetTransactionByIdAsync(Guid id);
        Task<IEnumerable<ReadTransactionDto>> GetUserTransactionHistoryAsync(Guid userId);
        Task<ReadTransactionHistoryDto> GetCompanyTransactionHistoryAsync(Guid companyId, int page = 1, int pageSize = 10);
        Task<IEnumerable<ReadTransactionDto>> GetSentTransactionsAsync(Guid userId);
        Task<IEnumerable<ReadTransactionDto>> GetReceivedTransactionsAsync(Guid userId);
    }
}
