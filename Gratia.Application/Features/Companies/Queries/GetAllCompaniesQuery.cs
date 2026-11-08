using Gratia.Application.DTOs.CompanyDTO;
using Gratia.Application.Mappings;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Companies.Queries;

public sealed record GetAllCompaniesQuery : IQuery<IReadOnlyList<ReadCompanyDto>>;

public sealed class GetAllCompaniesQueryHandler(ICompanyRepository companyRepository)
    : IQueryHandler<GetAllCompaniesQuery, IReadOnlyList<ReadCompanyDto>>
{
    public async ValueTask<IReadOnlyList<ReadCompanyDto>> Handle(GetAllCompaniesQuery query, CancellationToken cancellationToken)
    {
        var companies = await companyRepository.GetAllAsync();
        return companies.Select(c => c.ToReadDto()).ToList();
    }
}
