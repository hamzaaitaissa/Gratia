using Gratia.Application.DTOs.CompanyDTO;
using Gratia.Application.Exceptions;
using Gratia.Application.Mappings;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Companies.Queries;

// CallerCompanyId is the company of the signed-in user, who may only read their own company
public sealed record GetCompanyByIdQuery(Guid CompanyId, Guid CallerCompanyId) : IQuery<ReadCompanyDto>;

public sealed class GetCompanyByIdQueryHandler(ICompanyRepository companyRepository)
    : IQueryHandler<GetCompanyByIdQuery, ReadCompanyDto>
{
    public async ValueTask<ReadCompanyDto> Handle(GetCompanyByIdQuery query, CancellationToken cancellationToken)
    {
        // Another company gets the same 404 as one that doesn't exist, so ids can't be probed
        var company = query.CompanyId == query.CallerCompanyId
            ? await companyRepository.GetByIdAsync(query.CompanyId)
            : null;

        return company?.ToReadDto() ?? throw new NotFoundException($"Company {query.CompanyId} was not found.");
    }
}
