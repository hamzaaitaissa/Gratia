using Gratia.Application.DTOs.CompanyDTO;
using Gratia.Application.Exceptions;
using Gratia.Domain.Entities;
using Gratia.Domain.Enum;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Companies.Commands;

// Gives the signed-in member their own DNS record for verifying the workspace's domain (#61). Any member may: an admin
// to verify it, anyone else to become an admin, which is how IT gets in when someone else set the workspace up, and
// stays the way back in if every admin leaves. Asking again gives the same record, or a new one once it's spent.
public sealed record StartDomainVerificationCommand(Guid CallerUserId, Guid CallerCompanyId) : ICommand<DomainVerificationDto>;

public sealed class StartDomainVerificationCommandHandler(
    ICompanyRepository companyRepository,
    IUserRepository userRepository,
    IDomainVerificationRepository domainVerificationRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<StartDomainVerificationCommand, DomainVerificationDto>
{
    public async ValueTask<DomainVerificationDto> Handle(StartDomainVerificationCommand command, CancellationToken cancellationToken)
    {
        var company = await companyRepository.GetByIdAsync(command.CallerCompanyId)
            ?? throw new NotFoundException("Company not found");
        var user = await userRepository.GetByIdInCompanyAsync(command.CallerUserId, command.CallerCompanyId)
            ?? throw new NotFoundException("User not found");
        if (company.Domain is not { } domain)
            throw new BusinessRuleException(DomainOwnership.NoDomainMessage);

        var isAdmin = user.Role == UserRole.Admin;
        if (isAdmin && company.IsDomainVerified)
            throw new ConflictException($"{domain} is already verified.");

        // Two starts at the same moment (a double click) take turns, so the second finds the first one's record
        await using var dbTransaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await companyRepository.LockAsync(company.Id, cancellationToken);
        var verification = await domainVerificationRepository.GetForMemberAsync(company.Id, user.Id, cancellationToken);
        if (verification is null)
        {
            verification = new DomainVerification(company.Id, user.Id, user.Email, DomainOwnership.NewRecordToken());
            domainVerificationRepository.Add(verification);
        }
        else if (verification.IsSpent)
        {
            verification.Renew(DomainOwnership.NewRecordToken());
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        return new DomainVerificationDto
        {
            Domain = domain,
            IsVerified = company.IsDomainVerified,
            VerifiedAt = company.DomainVerifiedAt,
            IsAdmin = isAdmin,
            Record = DomainOwnership.Record(domain, verification)
        };
    }
}
