using Gratia.Application.DTOs.Invitation;
using Gratia.Application.Exceptions;
using Gratia.Application.Interfaces;
using Gratia.Application.Mappings;
using Gratia.Application.Settings;
using Gratia.Domain.Repositories;
using Mediator;
using Microsoft.Extensions.Options;

namespace Gratia.Application.Features.Invitations.Commands;

public sealed record ResendInvitationCommand(Guid InvitationId, Guid CallerCompanyId) : ICommand<InvitationDto>;

public sealed class ResendInvitationCommandHandler(
    IInvitationRepository invitationRepository,
    IUserRepository userRepository,
    ICompanyRepository companyRepository,
    IUnitOfWork unitOfWork,
    IInvitationEmailQueue emailQueue,
    IOptions<OnboardingOptions> onboardingOptions)
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

        // Only a member of this company is reported, as when inviting (#61)
        if (await userRepository.GetByEmailAsync(invitation.Email) is { } account && account.CompanyId == invitation.CompanyId)
            throw new ConflictException("This person is already a member of your company.");

        // After an invitation expires the admin can invite the same email again, so an older one may be resent
        var active = await invitationRepository.GetByEmailAndCompanyIdAsync(invitation.Email, invitation.CompanyId);
        if (active is not null && active.Id != invitation.Id)
            throw new ConflictException("A newer active invitation exists for this email");

        // Loaded into the same context, so they also fill the invitation's navigations for the response
        var company = await companyRepository.GetByIdAsync(invitation.CompanyId)
            ?? throw new NotFoundException("Company not found");
        var inviter = await userRepository.GetByIdAsync(inviterId)
            ?? throw new NotFoundException("User not found");

        // A new link, in the background like an invite (#55). It replaces the old one, which then stops working, once its
        // email has gone out, with 7 days from then; until that happens the link the invitee has keeps working (#20).
        // It's another email, so it counts for the daily cap like an invite (#65), in the same commit.
        var now = DateTime.UtcNow;
        await using (var dbTransaction = await unitOfWork.BeginTransactionAsync(cancellationToken))
        {
            await InvitationQuota.CountAsync(company, companyRepository, onboardingOptions.Value, now, cancellationToken);
            invitation.QueueResend(InvitationTokens.Generate(), now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);
        }

        emailQueue.Notify();
        return invitation.ToDto();
    }
}
