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
using System.Transactions;

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

        public async Task<ReadTransactionDto> CreateTransactionAsync(Guid senderId, CreateTransactionDto createReadTransactionDto)
        {
            var sender = await _userRepository.GetByIdAsync(senderId);
            if(sender == null) throw new KeyNotFoundException("Sender not found");
            var receiver = await _userRepository.GetByIdAsync(createReadTransactionDto.ReceiverId);
            if (receiver == null) throw new KeyNotFoundException("Receiver not found");
            if (createReadTransactionDto.Amount <= 0) throw new ArgumentException("You cant send negative points or 0 points");
            //check company
            if(sender.CompanyId !=  receiver.CompanyId) throw new InvalidOperationException("you are only allowed to exchange points with people within your company");
            if (sender.Id == createReadTransactionDto.ReceiverId) throw new InvalidOperationException("You cant send points to yourself?!");
            var transaction = new Domain.Entities.Transaction(
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

        public async Task<ReadTransactionHistoryDto> GetCompanyTransactionHistoryAsync(Guid companyId, int page = 1, int pageSize = 10)
        {
            var transactions = await _transactionRepository.GetCompanyTransactionHistoryAsync(companyId,page,pageSize);
            var transactionCount = transactions.Count();
            var transactionDtos = transactions.Select((t) =>new ReadTransactionDto
            {
                Id = t.Id,
                SenderId = t.SenderId,
                SenderName = t.Sender?.FullName ?? "",
                ReceiverId = t.ReceiverId,
                ReceiverName = t.Receiver?.FullName ?? "",
                CompanyId = t.CompanyId,
                CompanyName = t.Company?.Name ?? "",
                Amount = t.Amount,
                Message = t.Message,
                TypeOfDonation = t.TypeOfDonation,
                CreatedAt = t.CreatedDate
            });
            return new ReadTransactionHistoryDto
            {
                Transactions = transactionDtos,
                Page = page,
                PageSize = pageSize,
                TotalCount = transactionCount,
                TotalPages = (int)Math.Ceiling((double)transactionCount / pageSize)
            };

        }

        public async Task<IEnumerable<ReadTransactionDto>> GetReceivedTransactionsAsync(Guid userId)
        {
            var recievedTransactions = await _transactionRepository.GetByReceiverIdAsync(userId);
            return recievedTransactions.Select(t => new ReadTransactionDto
            {
                Id = t.Id,
                SenderId = t.SenderId,
                SenderName = t.Sender?.FullName ?? "",
                ReceiverId = t.ReceiverId,
                ReceiverName = t.Receiver?.FullName ?? "",
                CompanyId = t.CompanyId,
                CompanyName = t.Company?.Name ?? "",
                Amount = t.Amount,
                Message = t.Message,
                TypeOfDonation = t.TypeOfDonation,
                CreatedAt = t.CreatedDate
            });
        }

        public async Task<IEnumerable<ReadTransactionDto>> GetSentTransactionsAsync(Guid userId)
        {
            var senderTransactions = await _transactionRepository.GetBySenderIdAsync(userId);
            return senderTransactions.Select(t => new ReadTransactionDto
            {
                Id = t.Id,
                SenderId = t.SenderId,
                SenderName = t.Sender?.FullName ?? "",
                ReceiverId = t.ReceiverId,
                ReceiverName = t.Receiver?.FullName ?? "",
                CompanyId = t.CompanyId,
                CompanyName = t.Company?.Name ?? "",
                Amount = t.Amount,
                Message = t.Message,
                TypeOfDonation = t.TypeOfDonation,
                CreatedAt = t.CreatedDate
            });
        }

        public async Task<ReadTransactionDto> GetTransactionByIdAsync(Guid id)
        {
            var transaction = await _transactionRepository.GetByIdAsync(id);
            if(transaction == null)
            {
                throw new KeyNotFoundException("Transaction not found");
            }
            return new ReadTransactionDto
            {
                Id = transaction.Id,
                SenderId = transaction.SenderId,
                SenderName = transaction.Sender?.FullName ?? "",
                ReceiverId = transaction.ReceiverId,
                ReceiverName = transaction.Receiver?.FullName ?? "",
                CompanyId = transaction.CompanyId,
                CompanyName = transaction.Company?.Name ?? "",
                Amount = transaction.Amount,
                Message = transaction.Message,
                TypeOfDonation = transaction.TypeOfDonation,
                CreatedAt = transaction.CreatedDate
            };
        }

        public async Task<IEnumerable<ReadTransactionDto>> GetUserTransactionHistoryAsync(Guid userId)
        {
            var userTransactions = await _transactionRepository.GetUserTransactionHistoryAsync(userId);
            return userTransactions.Select(t => new ReadTransactionDto
            {
                Id = t.Id,
                SenderId = t.SenderId,
                SenderName = t.Sender?.FullName ?? "",
                ReceiverId = t.ReceiverId,
                ReceiverName = t.Receiver?.FullName ?? "",
                CompanyId = t.CompanyId,
                CompanyName = t.Company?.Name ?? "",
                Amount = t.Amount,
                Message = t.Message,
                TypeOfDonation = t.TypeOfDonation,
                CreatedAt = t.CreatedDate
            });
        }
    }
}
