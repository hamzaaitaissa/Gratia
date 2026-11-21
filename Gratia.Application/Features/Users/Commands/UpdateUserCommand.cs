using Gratia.Application.Common;
using Gratia.Application.DTOs.UserDTO;
using Gratia.Application.Exceptions;
using Gratia.Application.Interfaces;
using Gratia.Application.Mappings;
using Gratia.Domain.Entities;
using Gratia.Domain.Enum;
using Gratia.Domain.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Gratia.Application.Features.Users.Commands;

// CallerCompanyId is the signed-in admin's company: admins can only edit users in their own company.
// The password isn't part of the edit (#17). A role change applies at once: the user's access token stops working and
// a refresh brings the new role (#73). A new email becomes the sign-in, and the old address is told (#50): with a forgotten-
// password link to the new one, a changed email is a way into the account.
public sealed record UpdateUserCommand(Guid CallerCompanyId, UpdateUserDto User) : ICommand<ReadUserDto>;

public sealed class UpdateUserCommandHandler(
    IUserRepository userRepository,
    ICompanyRepository companyRepository,
    IUnitOfWork unitOfWork,
    IEmailService emailService,
    ILogger<UpdateUserCommandHandler> logger)
    : ICommandHandler<UpdateUserCommand, ReadUserDto>
{
    public async ValueTask<ReadUserDto> Handle(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        var dto = command.User;
        var user = await userRepository.GetByIdInCompanyAsync(dto.Id, command.CallerCompanyId)
            ?? throw new NotFoundException($"User {dto.Id} was not found.");

        var email = EmailAddresses.Normalize(dto.Email);
        var previousEmail = user.Email;
        Company? company = null;
        if (email != previousEmail)
        {
            // The same rule as inviting: an unverified workspace only has addresses on its own domain (#1, #61)
            company = await companyRepository.GetByIdAsync(user.CompanyId)
                ?? throw new NotFoundException("Company not found");
            if (company.Domain is not null && !company.IsDomainVerified && !EmailAddresses.IsOnDomain(email, company.Domain))
                throw new BusinessRuleException($"Until your company's domain is verified, members can only have @{company.Domain} addresses");

            // Emails are unique across all companies, deactivated users included. This used to hit the unique index
            // and answer 500.
            if (await userRepository.GetByEmailAsync(email) is not null)
                throw new ConflictException("Another account already uses this email");
        }

        // Demoting an admin, themselves included, must leave the company another one (#63)
        await using var dbTransaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        if (user.Role == UserRole.Admin && dto.Role != UserRole.Admin)
            await LastAdmin.EnsureAnotherRemainsAsync(companyRepository, userRepository, user.CompanyId, user.Id, cancellationToken);

        user.Update(dto.FullName, email, dto.JobTitle ?? string.Empty, dto.Role);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        // The member hears of it at the address they had (#50). The change is saved by now, so a failed email isn't an error.
        if (company is not null)
        {
            try
            {
                await emailService.SendEmailChangedEmailAsync(previousEmail, email, company.Name, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Couldn't send the email-changed notice for user {UserId}", user.Id);
            }
        }

        return user.ToReadDto();
    }
}
