using Gratia.Application.DTOs.Transaction;
using Gratia.Application.Exceptions;
using Gratia.Application.Mappings;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Transactions.Queries;

public sealed record GetCompanyTransactionHistoryQuery(Guid CompanyId, int Page, int PageSize) : IQuery<ReadTransactionHistoryDto>;

public sealed class GetCompanyTransactionHistoryQueryHandler(ITransactionRepository transactionRepository)
    : IQueryHandler<GetCompanyTransactionHistoryQuery, ReadTransactionHistoryDto>
{
    public const int MaxPageSize = 100;

    public async ValueTask<ReadTransactionHistoryDto> Handle(GetCompanyTransactionHistoryQuery query, CancellationToken cancellationToken)
    {
        if (query.Page < 1)
            throw new BusinessRuleException("page must be 1 or more.");
        if (query.PageSize is < 1 or > MaxPageSize)
            throw new BusinessRuleException($"pageSize must be between 1 and {MaxPageSize}.");

        var transactions = (await transactionRepository.GetCompanyTransactionHistoryAsync(query.CompanyId, query.Page, query.PageSize))
            .Select(t => t.ToReadDto())
            .ToList();

        // The company's total, not just this page's, so the client knows how many pages there are
        var totalCount = await transactionRepository.GetTotalTransactionCountAsync(query.CompanyId);

        return new ReadTransactionHistoryDto
        {
            Transactions = transactions,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling((double)totalCount / query.PageSize)
        };
    }
}
