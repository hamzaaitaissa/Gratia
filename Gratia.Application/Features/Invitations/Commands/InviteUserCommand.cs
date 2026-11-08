using System.Security.Cryptography;
using Gratia.Application.DTOs.Invitation;
using Gratia.Application.Exceptions;
using Gratia.Application.Interfaces;
using Gratia.Application.Mappings;
using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Invitations.Commands;

public sealed record InviteUserCommand(Guid CompanyId, Guid InvitedByUserId, InviteUserDto Invitation) : ICommand<InvitationDto>;

public sealed class InviteUserCommandHandler(
    IInvitationRepository invitationRepository,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IEmailService emailService)
    : ICommandHandler<InviteUserCommand, InvitationDto>
{
    public async ValueTask<InvitationDto> Handle(InviteUserCommand command, CancellationToken cancellationToken)
    {
        var dto = command.Invitation;

        if (await userRepository.GetByEmailAsync(dto.Email) is not null)
            throw new ConflictException("User with this email already exists");

        var existingInvitation = await invitationRepository.GetByEmailAndCompanyIdAsync(dto.Email, command.CompanyId);
        if (existingInvitation is not null && existingInvitation.IsValid)
            throw new ConflictException("An active invitation already exists for this email in the company");

        var token = GenerateInvitationToken();
        var invitation = new CompanyInvitation(
            dto.Email,
            token,
            DateTime.UtcNow.AddDays(7),
            dto.Role,
            command.CompanyId,
            command.InvitedByUserId);

        invitationRepository.Add(invitation);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Known issue (TASKS.md #20 / #55): if sending fails, the invitation is already saved
        await emailService.SendInvitationEmailAsync(dto.Email, token, command.CompanyId);

        return invitation.ToDto();
    }

    // URL-safe, so it can go straight into the /accept-invitation/{token} link
    private static string GenerateInvitationToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "");
    }
}
