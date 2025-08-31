using Gratia.Application.DTOs.Transaction;
using Gratia.Application.Interfaces;
using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Application.Services
{
    public class TransactionService : ITransactionService
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly IUserRepository _userRepository;

        public TransactionService(ITransactionRepository transactionRepository, IUserRepository userRepository)
        {
            _transactionRepository = transactionRepository;
            _userRepository = userRepository;
        }

        public async Task<ReadTransactionDto> CreateTransactionAsync(Guid senderId, CreateReadTransactionDto createReadTransactionDto)
        {
            var sender = await _userRepository.GetByIdAsync(senderId);
            if(sender == null) throw new KeyNotFoundException("Sender not found");
            var receiver = await _userRepository.GetByIdAsync(createReadTransactionDto.ReceiverId);
            if (receiver == null) throw new KeyNotFoundException("Receiver not found");
            if (createReadTransactionDto.Amount <= 0) throw new ArgumentException("You cant send negative points or 0 points");
            //check company
            if(sender.CompanyId !=  receiver.CompanyId) throw new InvalidOperationException("you are only allowed to exchange points with people within your company");
            if (sender.Id == createReadTransactionDto.ReceiverId) throw new InvalidOperationException("You cant send points to yourself?!");
            var transaction = new Transaction(
                senderId,
                createReadTransactionDto.ReceiverId,
                createReadTransactionDto.Amount,
                createReadTransactionDto.Message,
                createReadTransactionDto.TypeOfDonation,
                sender.CompanyId);
           
            sender.NumberOfPointsAvailable -= createReadTransactionDto.Amount;
            receiver.NumberOfPointsAcquired += createReadTransactionDto.Amount;

            var createdTransaction = await _transactionRepository.CreateAsync(transaction);

            await _userRepository.UpdateAsync(sender);
            await _userRepository.UpdateAsync(receiver);

            return new ReadTransactionDto
            {
                Id = createdTransaction.Id,
                SenderId = createdTransaction.SenderId,
                SenderName = sender.FullName,
                ReceiverId = createdTransaction.ReceiverId,
                ReceiverName = receiver.FullName,
                CompanyId = createdTransaction.CompanyId,
                CompanyName = sender.Company?.Name ?? "",
                Amount = createdTransaction.Amount,
                Message = createdTransaction.Message,
                TypeOfDonation = createdTransaction.TypeOfDonation,
                CreatedAt = createdTransaction.CreatedDate
            };

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
