using Gratia.Application.Common;
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
    ICompanyRepository companyRepository,
    IUnitOfWork unitOfWork,
    IEmailService emailService)
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

        var existing = await userRepository.GetByEmailAsync(email);
        if (existing is not null && existing.CompanyId == company.Id && existing.DeactivatedAt is not null)
            throw new ConflictException("This person's account was deactivated. Reactivate it instead of inviting them again.");
        if (existing is not null)
            throw new ConflictException("User with this email already exists");

        var existingInvitation = await invitationRepository.GetByEmailAndCompanyIdAsync(email, command.CompanyId);
        if (existingInvitation is not null && existingInvitation.IsValid)
            throw new ConflictException("An active invitation already exists for this email in the company");

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
            InvitedByUser = inviter
        };

        // Saved before sending, so the link works the moment it arrives
        invitationRepository.Add(invitation);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            await emailService.SendInvitationEmailAsync(email, token, company.Name, inviter.FullName, inviter.Email, cancellationToken);
        }
        catch
        {
            // #20: an invitation nobody received would block inviting the address again for 7 days. Undone, the
            // admin gets a 503 and can simply try again. Not cancellable: the request may be what was cancelled.
            invitationRepository.Remove(invitation);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        return invitation.ToDto();
    }
}
