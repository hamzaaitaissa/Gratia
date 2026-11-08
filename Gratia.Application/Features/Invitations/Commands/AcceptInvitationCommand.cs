using Gratia.Application.DTOs.Invitation;
using Gratia.Application.Exceptions;
using Gratia.Application.Interfaces;
using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Invitations.Commands;

public sealed record AcceptInvitationCommand(AcceptInvitationDto Invitation) : ICommand;

public sealed class AcceptInvitationCommandHandler(
    IInvitationRepository invitationRepository,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IPasswordHashingService passwordHashingService)
    : ICommandHandler<AcceptInvitationCommand>
{
    public async ValueTask<Unit> Handle(AcceptInvitationCommand command, CancellationToken cancellationToken)
    {
        var dto = command.Invitation;

        var invitation = await invitationRepository.GetByTokenAsync(dto.Token);
        if (invitation is null || !invitation.IsValid)
            throw new BusinessRuleException("Invalid or expired invitation");

        if (await userRepository.GetByEmailAsync(invitation.Email) is not null)
            throw new ConflictException("User with this email already exists");

        var user = new User(
            dto.FullName,
            invitation.Email,
            passwordHashingService.HashPassword(dto.Password),
            dto.JobTitle,
            invitation.InvitedRole,
            25, // Known issue (TASKS.md #10): received and to-give look swapped
            0,
            invitation.CompanyId);

        userRepository.Add(user);
        invitation.IsUsed = true;

        // Saved together: the account is created and the invitation used up, or neither
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
