using Gratia.Application.DTOs.Transaction;
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

        public Task<ReadTransactionDto> CreateTransactionAsync(Guid senderId, CreateReadTransactionDto createReadTransactionDto)
        {
            throw new NotImplementedException();
        }

        public Task<ReadTransactionHistoryDto> GetCompanyTransactionHistoryAsync(Guid companyId, int page = 1, int pageSize = 10)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<ReadTransactionDto>> GetReceivedTransactionsAsync(Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<ReadTransactionDto>> GetSentTransactionsAsync(Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task<ReadTransactionDto> GetTransactionByIdAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<ReadTransactionDto>> GetUserTransactionHistoryAsync(Guid userId)
        {
            throw new NotImplementedException();
        }
    }
}
