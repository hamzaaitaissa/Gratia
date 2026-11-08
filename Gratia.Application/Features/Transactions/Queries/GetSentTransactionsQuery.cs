using Gratia.Application.DTOs.Transaction;
using Gratia.Application.Mappings;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Transactions.Queries;

public sealed record GetSentTransactionsQuery(Guid UserId) : IQuery<IReadOnlyList<ReadTransactionDto>>;

public sealed class GetSentTransactionsQueryHandler(ITransactionRepository transactionRepository)
    : IQueryHandler<GetSentTransactionsQuery, IReadOnlyList<ReadTransactionDto>>
{
    public async ValueTask<IReadOnlyList<ReadTransactionDto>> Handle(GetSentTransactionsQuery query, CancellationToken cancellationToken)
    {
        var transactions = await transactionRepository.GetBySenderIdAsync(query.UserId);
        return transactions.Select(t => t.ToReadDto()).ToList();
    }
}
