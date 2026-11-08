using Gratia.Application.DTOs.Transaction;
using Gratia.Application.Mappings;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Transactions.Queries;

public sealed record GetReceivedTransactionsQuery(Guid UserId) : IQuery<IReadOnlyList<ReadTransactionDto>>;

public sealed class GetReceivedTransactionsQueryHandler(ITransactionRepository transactionRepository)
    : IQueryHandler<GetReceivedTransactionsQuery, IReadOnlyList<ReadTransactionDto>>
{
    public async ValueTask<IReadOnlyList<ReadTransactionDto>> Handle(GetReceivedTransactionsQuery query, CancellationToken cancellationToken)
    {
        var transactions = await transactionRepository.GetByReceiverIdAsync(query.UserId);
        return transactions.Select(t => t.ToReadDto()).ToList();
    }
}
