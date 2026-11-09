using Gratia.Application.Common;
using Gratia.Application.DTOs.UserDTO;
using Gratia.Application.Exceptions;
using Gratia.Application.Mappings;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Users.Commands;

// CallerCompanyId is the signed-in admin's company: admins can only edit users in their own company.
// The password isn't part of the edit (#17). A role change applies at the user's next token refresh (#15).
public sealed record UpdateUserCommand(Guid CallerCompanyId, UpdateUserDto User) : ICommand<ReadUserDto>;

public sealed class UpdateUserCommandHandler(
    IUserRepository userRepository,
    ICompanyRepository companyRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateUserCommand, ReadUserDto>
{
    public async ValueTask<ReadUserDto> Handle(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        var dto = command.User;
        var user = await userRepository.GetByIdInCompanyAsync(dto.Id, command.CallerCompanyId)
            ?? throw new NotFoundException($"User {dto.Id} was not found.");

        var email = EmailAddresses.Normalize(dto.Email);
        if (email != user.Email)
        {
            // The same rule as inviting: an unverified workspace only has addresses on its own domain (#1, #61)
            var company = await companyRepository.GetByIdAsync(user.CompanyId)
                ?? throw new NotFoundException("Company not found");
            if (company.Domain is not null && !company.IsDomainVerified && !EmailAddresses.IsOnDomain(email, company.Domain))
                throw new BusinessRuleException($"Until your company's domain is verified, members can only have @{company.Domain} addresses");

            // Emails are unique across all companies, deactivated users included. This used to hit the unique index
            // and answer 500.
            if (await userRepository.GetByEmailAsync(email) is not null)
                throw new ConflictException("Another account already uses this email");
        }

        user.Update(dto.FullName, email, dto.JobTitle ?? string.Empty, dto.Role);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return user.ToReadDto();
    }
}
