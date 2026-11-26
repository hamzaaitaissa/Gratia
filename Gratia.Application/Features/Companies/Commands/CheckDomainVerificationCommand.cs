using Gratia.Application.DTOs.CompanyDTO;
using Gratia.Application.Exceptions;
using Gratia.Application.Interfaces;
using Gratia.Domain.Enum;
using Gratia.Domain.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Gratia.Application.Features.Companies.Commands;

// Looks for the signed-in member's record on the workspace's domain (#61). Found: the record is spent, the workspace is
// verified, and a member who isn't an admin becomes one. Their access token then gets a 401 like any role change (#73),
// and a refresh brings the admin role. The other admins are told by email.
public sealed record CheckDomainVerificationCommand(Guid CallerUserId, Guid CallerCompanyId) : ICommand<DomainCheckResultDto>;

public sealed class CheckDomainVerificationCommandHandler(
    ICompanyRepository companyRepository,
    IUserRepository userRepository,
    IDomainVerificationRepository domainVerificationRepository,
    IDomainVerifier domainVerifier,
    IUnitOfWork unitOfWork,
    IEmailService emailService,
    ILogger<CheckDomainVerificationCommandHandler> logger)
    : ICommandHandler<CheckDomainVerificationCommand, DomainCheckResultDto>
{
    public async ValueTask<DomainCheckResultDto> Handle(CheckDomainVerificationCommand command, CancellationToken cancellationToken)
    {
        var company = await companyRepository.GetByIdAsync(command.CallerCompanyId)
            ?? throw new NotFoundException("Company not found");
        var user = await userRepository.GetByIdInCompanyAsync(command.CallerUserId, command.CallerCompanyId)
            ?? throw new NotFoundException("User not found");
        if (company.Domain is not { } domain)
            throw new BusinessRuleException(DomainOwnership.NoDomainMessage);

        var wasAdmin = user.Role == UserRole.Admin;
        if (wasAdmin && company.IsDomainVerified)
            return new DomainCheckResultDto { Status = DomainCheckStatus.Verified, Message = $"{domain} is verified." };

        var verification = await domainVerificationRepository.GetForMemberAsync(company.Id, user.Id, cancellationToken);
        if (verification is null || verification.IsSpent)
            throw new BusinessRuleException(DomainOwnership.StartFirstMessage(domain));

        var lookup = await domainVerifier.HasTxtRecordAsync(domain, verification.RecordValue, cancellationToken);
        if (DomainOwnership.NotVerified(lookup, domain) is { } notVerified)
            return notVerified;

        var verified = new DomainCheckResultDto
        {
            Status = DomainCheckStatus.Verified,
            Message = (wasAdmin
                ? $"{domain} is verified. Admins can now invite people on any domain, with no daily limit. "
                : $"{domain} is verified, and you're now an admin of {company.Name}. ") + DomainOwnership.CanRemoveRecord
        };

        // The lookup can take seconds, so the record and the member are checked again here, in the database
        var now = DateTime.UtcNow;
        await using (var dbTransaction = await unitOfWork.BeginTransactionAsync(cancellationToken))
        {
            // Another check of it (a double click) was faster: what that one granted stands, and nothing is granted twice
            if (!await domainVerificationRepository.TrySpendAsync(verification.Id, verification.RecordToken, now, cancellationToken))
                return verified;
            if (!wasAdmin && !await userRepository.TryMakeAdminAsync(user.Id, company.Id, cancellationToken))
                throw new NotFoundException("User not found"); // deactivated meanwhile

            company.DomainVerifiedAt ??= now;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);
        }

        await DomainOwnership.NotifyAdminsAsync(userRepository, emailService, logger, company, user.Email,
            wasAdmin ? DomainVerifiedBy.Admin : DomainVerifiedBy.MemberMadeAdmin, cancellationToken);
        return verified;
    }
}
