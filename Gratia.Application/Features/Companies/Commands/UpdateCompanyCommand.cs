using Gratia.Application.DTOs.CompanyDTO;
using Gratia.Application.Exceptions;
using Gratia.Application.Mappings;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Companies.Commands;

// CallerCompanyId is the company of the signed-in user: admins may only edit their own company
public sealed record UpdateCompanyCommand(Guid CallerCompanyId, UpdateCompanyDto Company) : ICommand<ReadCompanyDto>;

public sealed class UpdateCompanyCommandHandler(ICompanyRepository companyRepository, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateCompanyCommand, ReadCompanyDto>
{
    public async ValueTask<ReadCompanyDto> Handle(UpdateCompanyCommand command, CancellationToken cancellationToken)
    {
        var dto = command.Company;
        if (command.CallerCompanyId != dto.Id)
            throw new ForbiddenException("You are not authorized to edit this Company");

        var company = await companyRepository.GetByIdAsync(dto.Id)
            ?? throw new NotFoundException($"Company {dto.Id} was not found.");

        company.Update(dto.Name, dto.LogoUrl, dto.PrimaryColor, dto.SecondaryColor);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return company.ToReadDto();
    }
}
