using Gratia.Application.DTOs.Transaction;
using Gratia.Domain.Entities;

namespace Gratia.Application.Mappings;

public static class TransactionMappings
{
    // The names come from navigation properties, so queries must Include Sender, Receiver and Company
    public static ReadTransactionDto ToReadDto(this Transaction transaction) => new()
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
