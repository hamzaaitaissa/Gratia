using Gratia.Application.DTOs.Transaction;
using Gratia.Application.Mappings;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Transactions.Queries;

public sealed record GetCompanyTransactionHistoryQuery(Guid CompanyId, int Page, int PageSize) : IQuery<ReadTransactionHistoryDto>;

public sealed class GetCompanyTransactionHistoryQueryHandler(ITransactionRepository transactionRepository)
    : IQueryHandler<GetCompanyTransactionHistoryQuery, ReadTransactionHistoryDto>
{
    public async ValueTask<ReadTransactionHistoryDto> Handle(GetCompanyTransactionHistoryQuery query, CancellationToken cancellationToken)
    {
        var transactions = (await transactionRepository.GetCompanyTransactionHistoryAsync(query.CompanyId, query.Page, query.PageSize))
            .Select(t => t.ToReadDto())
            .ToList();

        // Known bug (TASKS.md #13): this counts the current page, not the company's total
        var transactionCount = transactions.Count;

        return new ReadTransactionHistoryDto
        {
            Transactions = transactions,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = transactionCount,
            TotalPages = (int)Math.Ceiling((double)transactionCount / query.PageSize)
        };
    }
}
