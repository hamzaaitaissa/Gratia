using Gratia.Application.DTOs.Transaction;
using Gratia.Application.Mappings;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Transactions.Queries;

// Everything the user sent or received
public sealed record GetUserTransactionHistoryQuery(Guid UserId) : IQuery<IReadOnlyList<ReadTransactionDto>>;

public sealed class GetUserTransactionHistoryQueryHandler(ITransactionRepository transactionRepository)
    : IQueryHandler<GetUserTransactionHistoryQuery, IReadOnlyList<ReadTransactionDto>>
{
    public async ValueTask<IReadOnlyList<ReadTransactionDto>> Handle(GetUserTransactionHistoryQuery query, CancellationToken cancellationToken)
    {
        var transactions = await transactionRepository.GetUserTransactionHistoryAsync(query.UserId);
        return transactions.Select(t => t.ToReadDto()).ToList();
    }
}
