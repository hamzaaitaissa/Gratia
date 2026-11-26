using Gratia.Application.Common;
using Gratia.Application.DTOs.Invitation;
using Gratia.Application.Exceptions;
using Gratia.Application.Interfaces;
using Gratia.Application.Mappings;
using Gratia.Application.Settings;
using Gratia.Domain.Entities;
using Gratia.Domain.Enum;
using Gratia.Domain.Repositories;
using Mediator;
using Microsoft.Extensions.Options;

namespace Gratia.Application.Features.Invitations.Commands;

public sealed record InviteUserCommand(Guid CompanyId, Guid InvitedByUserId, InviteUserDto Invitation) : ICommand<InvitationDto>;

public sealed class InviteUserCommandHandler(
    IInvitationRepository invitationRepository,
    IUserRepository userRepository,
    ICompanyRepository companyRepository,
    IUnitOfWork unitOfWork,
    IInvitationEmailQueue emailQueue,
    IOptions<OnboardingOptions> onboardingOptions)
    : ICommandHandler<InviteUserCommand, InvitationDto>
{
    public async ValueTask<InvitationDto> Handle(InviteUserCommand command, CancellationToken cancellationToken)
    {
        var email = EmailAddresses.Normalize(command.Invitation.Email);

        var company = await companyRepository.GetByIdAsync(command.CompanyId)
            ?? throw new NotFoundException("Company not found");
        var inviter = await userRepository.GetByIdAsync(command.InvitedByUserId)
            ?? throw new NotFoundException("User not found");

        // Until a company proves it controls its domain (TASKS.md #61), its admins can only invite people on that
        // domain, so a provisional workspace can't be used to send invitations to strangers
        if (company.Domain is not null && !company.IsDomainVerified && !EmailAddresses.IsOnDomain(email, company.Domain))
            throw new BusinessRuleException($"Until your company's domain is verified, you can only invite @{company.Domain} addresses");

        // An account in another company is invited like any address, so inviting can't be used to find out who has a
        // Gratia account (#61): its email says the address is taken instead of carrying a link (SendInvitationEmailsCommand)
        var existing = await userRepository.GetByEmailAsync(email);
        if (existing is not null && existing.CompanyId == company.Id)
            throw new ConflictException(existing.DeactivatedAt is not null
                ? "This person's account was deactivated. Reactivate it instead of inviting them again."
                : "This person is already a member of your company.");

        var existingInvitation = await invitationRepository.GetByEmailAndCompanyIdAsync(email, command.CompanyId);
        if (existingInvitation is not null && existingInvitation.IsValid)
            throw new ConflictException(existingInvitation.EmailStatus == InvitationEmailStatus.Failed
                ? "An invitation to this email couldn't be sent. Resend it from the list of pending invitations."
                : "An active invitation already exists for this email in the company");

        var token = InvitationTokens.Generate();
        var invitation = new CompanyInvitation(
            email,
            token,
            DateTime.UtcNow.Add(InvitationTokens.Lifetime),
            command.Invitation.Role,
            company.Id,
            inviter.Id)
        {
            // So the response carries the company and inviter names
            Company = company,
            InvitedByUser = inviter,
            FullName = string.IsNullOrWhiteSpace(command.Invitation.FullName) ? null : command.Invitation.FullName.Trim(),
            JobTitle = string.IsNullOrWhiteSpace(command.Invitation.JobTitle) ? null : command.Invitation.JobTitle.Trim()
        };

        // The email goes out in the background (#55), so this answers at once and a slow or failing mail server doesn't
        // fail the invite: the email is retried, and the list shows it as Failed if it never goes out, for Resend.
        // Counted for the daily cap in the same commit (#65).
        var now = DateTime.UtcNow;
        invitation.QueueEmail(now);
        await using (var dbTransaction = await unitOfWork.BeginTransactionAsync(cancellationToken))
        {
            await InvitationQuota.CountAsync(company, companyRepository, onboardingOptions.Value, now, cancellationToken);
            invitationRepository.Add(invitation);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);
        }

        emailQueue.Notify();
        return invitation.ToDto();
    }
}
