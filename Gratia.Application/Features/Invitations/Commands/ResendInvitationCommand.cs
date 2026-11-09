using Gratia.Application.DTOs.Invitation;
using Gratia.Application.Exceptions;
using Gratia.Application.Interfaces;
using Gratia.Application.Mappings;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Invitations.Commands;

public sealed record ResendInvitationCommand(Guid InvitationId, Guid CallerCompanyId) : ICommand<InvitationDto>;

public sealed class ResendInvitationCommandHandler(
    IInvitationRepository invitationRepository,
    IUserRepository userRepository,
    ICompanyRepository companyRepository,
    IUnitOfWork unitOfWork,
    IEmailService emailService)
    : ICommandHandler<ResendInvitationCommand, InvitationDto>
{
    public async ValueTask<InvitationDto> Handle(ResendInvitationCommand command, CancellationToken cancellationToken)
    {
        // Another company's invitation is reported as missing, so its existence isn't revealed
        var invitation = await invitationRepository.GetByIdAsync(command.InvitationId);
        if (invitation is null || invitation.CompanyId != command.CallerCompanyId)
            throw new NotFoundException("Invitation not found");

        if (invitation.IsUsed)
            throw new BusinessRuleException("This invitation has already been accepted");

        // A founder gets a new setup link by signing up again; admins only resend the invitations they sent
        if (invitation.InvitedByUserId is not Guid inviterId)
            throw new BusinessRuleException("This is a company setup link; sign up again to get a new one");

        if (await userRepository.GetByEmailAsync(invitation.Email) is not null)
            throw new ConflictException("User with this email already exists");

        // After an invitation expires the admin can invite the same email again, so an older one may be resent
        var active = await invitationRepository.GetByEmailAndCompanyIdAsync(invitation.Email, invitation.CompanyId);
        if (active is not null && active.Id != invitation.Id)
            throw new ConflictException("A newer active invitation exists for this email");

        // Loaded into the same context, so they also fill the invitation's navigations for the response
        var company = await companyRepository.GetByIdAsync(invitation.CompanyId)
            ?? throw new NotFoundException("Company not found");
        var inviter = await userRepository.GetByIdAsync(inviterId)
            ?? throw new NotFoundException("User not found");

        // A new token, so a link from an earlier email stops working; the 7 days start again
        invitation.InvitationToken = InvitationTokens.Generate();
        invitation.ExpiresAt = DateTime.UtcNow.Add(InvitationTokens.Lifetime);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Known issue (TASKS.md #20 / #55): if sending fails, the new token is already saved
        await emailService.SendInvitationEmailAsync(invitation.Email, invitation.InvitationToken, company.Name, inviter.FullName, inviter.Email,
            cancellationToken);

        return invitation.ToDto();
    }
}
