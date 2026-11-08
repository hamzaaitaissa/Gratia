using Gratia.Application.DTOs.CompanyDTO;
using Gratia.Application.Mappings;
using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Companies.Commands;

public sealed record CreateCompanyCommand(RegisterCompanyDto Company) : ICommand<ReadCompanyDto>;

public sealed class CreateCompanyCommandHandler(ICompanyRepository companyRepository, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateCompanyCommand, ReadCompanyDto>
{
    public async ValueTask<ReadCompanyDto> Handle(CreateCompanyCommand command, CancellationToken cancellationToken)
    {
        var dto = command.Company;
        var company = new Company(dto.Name, dto.LogoUrl, dto.PrimaryColor, dto.SecondaryColor);

        companyRepository.Add(company);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return company.ToReadDto();
    }
}
