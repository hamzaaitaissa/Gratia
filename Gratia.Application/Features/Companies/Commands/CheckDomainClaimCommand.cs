using Gratia.Application.Common;
using Gratia.Application.DTOs.CompanyDTO;
using Gratia.Application.Exceptions;
using Gratia.Application.Features.Companies.Queries;
using Gratia.Application.Features.Invitations;
using Gratia.Application.Interfaces;
using Gratia.Domain.Entities;
using Gratia.Domain.Enum;
using Gratia.Domain.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Gratia.Application.Features.Companies.Commands;

// A claim (#61): someone who isn't an admin of their domain's workspace, typically IT after someone else set it up,
// proves they manage the domain. The link from their email proves the address; finding their record on the domain proves
// the rest, and spends it. The workspace is verified, and they get an admin invitation (its token comes back, for the
// accept page), or, with an account there already, their account becomes an admin. Nothing is merged or removed. The
// admins are told by email.
public sealed record CheckDomainClaimCommand(string Token) : ICommand<DomainCheckResultDto>;

public sealed class CheckDomainClaimCommandHandler(
    IDomainVerificationRepository domainVerificationRepository,
    IUserRepository userRepository,
    IInvitationRepository invitationRepository,
    IDomainVerifier domainVerifier,
    IUnitOfWork unitOfWork,
    IEmailService emailService,
    ILogger<CheckDomainClaimCommandHandler> logger)
    : ICommandHandler<CheckDomainClaimCommand, DomainCheckResultDto>
{
    public async ValueTask<DomainCheckResultDto> Handle(CheckDomainClaimCommand command, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var verification = await domainVerificationRepository.GetByLinkTokenHashAsync(SecretTokens.Hash(command.Token), cancellationToken);
        if (verification is null || !verification.HasLinkUntil(now) || verification.Company.Domain is not { } domain)
            throw new NotFoundException(GetDomainClaimQueryHandler.InvalidLinkMessage);
        var company = verification.Company;

        if (verification.IsSpent)
            return await AlreadyUsedAsync(verification, company, domain);

        var lookup = await domainVerifier.HasTxtRecordAsync(domain, verification.RecordValue, cancellationToken);
        if (DomainOwnership.NotVerified(lookup, domain) is { } notVerified)
            return notVerified;

        // The link proved the address is theirs, so they may hear what it has elsewhere. Read after the lookup: an
        // invitation may have been accepted meanwhile.
        var account = await userRepository.GetByEmailAsync(verification.Email);
        if (account is not null && account.CompanyId != company.Id)
            throw new BusinessRuleException("This address has an account in another Gratia workspace, and an address can belong to only one.");
        // Like every way in that doesn't use an access token (#16)
        if (account?.DeactivatedAt is not null)
            throw new BusinessRuleException($"Your account in {company.Name} was deactivated. Ask one of its admins to reactivate it.");

        DomainCheckResultDto result;
        DomainVerifiedBy by;
        await using (var dbTransaction = await unitOfWork.BeginTransactionAsync(cancellationToken))
        {
            // Another check of it (a double click) was faster: what it granted stands, and nothing is granted twice
            if (!await domainVerificationRepository.TrySpendAsync(verification.Id, verification.RecordToken, now, cancellationToken))
                return await AlreadyUsedAsync(verification, company, domain);

            if (account is not null)
            {
                if (!await userRepository.TryMakeAdminAsync(account.Id, company.Id, cancellationToken))
                    throw new BusinessRuleException($"Your account in {company.Name} was deactivated. Ask one of its admins to reactivate it.");
                by = DomainVerifiedBy.MemberMadeAdmin;
                result = new DomainCheckResultDto
                {
                    Status = DomainCheckStatus.Verified,
                    Message = $"{domain} is verified, and your account is now an admin of {company.Name}. Sign in to continue. {DomainOwnership.CanRemoveRecord}"
                };
            }
            else
            {
                var invitation = await AdminInvitationAsync(verification.Email, company, now);
                by = DomainVerifiedBy.InvitedAsAdmin;
                result = Invited(domain, company, invitation);
            }

            company.DomainVerifiedAt ??= now;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);
        }

        await DomainOwnership.NotifyAdminsAsync(userRepository, emailService, logger, company, verification.Email, by, cancellationToken);
        return result;
    }

    // An invitation they already have from an admin becomes an admin one. Otherwise it's a new one, like a founding
    // invitation: no inviter, and no email, since its token is in the response.
    private async Task<CompanyInvitation> AdminInvitationAsync(string email, Company company, DateTime now)
    {
        var expiresAt = now.Add(InvitationTokens.Lifetime);
        var invitation = await invitationRepository.GetByEmailAndCompanyIdAsync(email, company.Id);
        if (invitation is null)
        {
            invitation = new CompanyInvitation(email, InvitationTokens.Generate(), expiresAt, UserRole.Admin, company.Id, invitedByUserId: null);
            invitationRepository.Add(invitation);
            return invitation;
        }

        invitation.InvitedRole = UserRole.Admin;
        if (invitation.ExpiresAt < expiresAt)
            invitation.ExpiresAt = expiresAt;
        return invitation;
    }

    // The claim was finished before. Someone who lost the answer gets the invitation it gave them, while it's unused;
    // nothing new is granted, so an old link can't undo a demotion.
    private async Task<DomainCheckResultDto> AlreadyUsedAsync(DomainVerification verification, Company company, string domain)
    {
        if (await userRepository.GetByEmailAsync(verification.Email) is null
            && await invitationRepository.GetByEmailAndCompanyIdAsync(verification.Email, company.Id) is { InvitedRole: UserRole.Admin } invitation)
            return Invited(domain, company, invitation);

        throw new BusinessRuleException(
            "This claim has been used. If you have an account, sign in. Otherwise sign up again with your work email to get a new link and record.");
    }

    private static DomainCheckResultDto Invited(string domain, Company company, CompanyInvitation invitation) => new()
    {
        Status = DomainCheckStatus.Verified,
        Message = $"{domain} is verified. Choose a password to join {company.Name} as an admin. {DomainOwnership.CanRemoveRecord}",
        InvitationToken = invitation.InvitationToken
    };
}
