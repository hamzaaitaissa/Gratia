using Gratia.Application.DTOs.CompanyDTO;
using Gratia.Application.Exceptions;
using Gratia.Application.Mappings;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Companies.Queries;

public sealed record GetCompanyByIdQuery(Guid CompanyId) : IQuery<ReadCompanyDto>;

public sealed class GetCompanyByIdQueryHandler(ICompanyRepository companyRepository)
    : IQueryHandler<GetCompanyByIdQuery, ReadCompanyDto>
{
    public async ValueTask<ReadCompanyDto> Handle(GetCompanyByIdQuery query, CancellationToken cancellationToken)
    {
        var company = await companyRepository.GetByIdAsync(query.CompanyId)
            ?? throw new NotFoundException($"Company {query.CompanyId} was not found.");

        return company.ToReadDto();
    }
}
