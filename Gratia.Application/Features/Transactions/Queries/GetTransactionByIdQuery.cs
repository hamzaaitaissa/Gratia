using Gratia.Application.DTOs.Transaction;
using Gratia.Application.Exceptions;
using Gratia.Application.Mappings;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Transactions.Queries;

// Only the sender and the receiver may read a transaction. Another company's transaction gets the same 404 as one that
// doesn't exist (#71), so the 403 only ever confirms an id within the caller's own company.
public sealed record GetTransactionByIdQuery(Guid TransactionId, Guid CallerUserId, Guid CallerCompanyId) : IQuery<ReadTransactionDto>;

public sealed class GetTransactionByIdQueryHandler(ITransactionRepository transactionRepository)
    : IQueryHandler<GetTransactionByIdQuery, ReadTransactionDto>
{
    public async ValueTask<ReadTransactionDto> Handle(GetTransactionByIdQuery query, CancellationToken cancellationToken)
    {
        var transaction = await transactionRepository.GetByIdAsync(query.TransactionId);
        if (transaction is null || transaction.CompanyId != query.CallerCompanyId)
            throw new NotFoundException("Transaction not found");

        if (query.CallerUserId != transaction.SenderId && query.CallerUserId != transaction.ReceiverId)
            throw new ForbiddenException("You are not allowed to retrieve this transaction");

        return transaction.ToReadDto();
    }
}
