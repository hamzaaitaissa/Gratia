using Gratia.Application.DTOs.Transaction;
using Gratia.Application.Exceptions;
using Gratia.Application.Mappings;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Transactions.Queries;

// Only the sender and the receiver may read a transaction
public sealed record GetTransactionByIdQuery(Guid TransactionId, Guid CallerUserId) : IQuery<ReadTransactionDto>;

public sealed class GetTransactionByIdQueryHandler(ITransactionRepository transactionRepository)
    : IQueryHandler<GetTransactionByIdQuery, ReadTransactionDto>
{
    public async ValueTask<ReadTransactionDto> Handle(GetTransactionByIdQuery query, CancellationToken cancellationToken)
    {
        var transaction = await transactionRepository.GetByIdAsync(query.TransactionId)
            ?? throw new NotFoundException("Transaction not found");

        if (query.CallerUserId != transaction.SenderId && query.CallerUserId != transaction.ReceiverId)
            throw new ForbiddenException("You are not allowed to retrieve this transaction");

        return transaction.ToReadDto();
    }
}
