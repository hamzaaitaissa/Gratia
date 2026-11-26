using Gratia.Application.DTOs.CompanyDTO;
using Gratia.Application.Exceptions;
using Gratia.Domain.Enum;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Companies.Queries;

// The signed-in member's workspace: whether its domain is verified, and the member's own DNS record if they've started (#61)
public sealed record GetDomainVerificationQuery(Guid CallerUserId, Guid CallerCompanyId) : IQuery<DomainVerificationDto>;

public sealed class GetDomainVerificationQueryHandler(
    ICompanyRepository companyRepository,
    IUserRepository userRepository,
    IDomainVerificationRepository domainVerificationRepository)
    : IQueryHandler<GetDomainVerificationQuery, DomainVerificationDto>
{
    public async ValueTask<DomainVerificationDto> Handle(GetDomainVerificationQuery query, CancellationToken cancellationToken)
    {
        var company = await companyRepository.GetByIdAsync(query.CallerCompanyId)
            ?? throw new NotFoundException("Company not found");
        var user = await userRepository.GetByIdInCompanyAsync(query.CallerUserId, query.CallerCompanyId)
            ?? throw new NotFoundException("User not found");

        var verification = company.Domain is null
            ? null
            : await domainVerificationRepository.GetForMemberAsync(company.Id, user.Id, cancellationToken);

        return new DomainVerificationDto
        {
            Domain = company.Domain,
            IsVerified = company.IsDomainVerified,
            VerifiedAt = company.DomainVerifiedAt,
            IsAdmin = user.Role == UserRole.Admin,
            // A spent record doesn't work any more: starting again gives a new one
            Record = verification is null || verification.IsSpent ? null : DomainOwnership.Record(company.Domain!, verification)
        };
    }
}
