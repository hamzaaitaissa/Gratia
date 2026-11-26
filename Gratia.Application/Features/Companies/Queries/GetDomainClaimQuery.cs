using Gratia.Application.Common;
using Gratia.Application.DTOs.CompanyDTO;
using Gratia.Application.Exceptions;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Companies.Queries;

// The claim page (#61), before anyone is signed in: the link from signup's "already on Gratia" email shows the workspace
// and the record to add. 404 when the link is unknown or expired.
public sealed record GetDomainClaimQuery(string Token) : IQuery<DomainClaimDto>;

public sealed class GetDomainClaimQueryHandler(IDomainVerificationRepository domainVerificationRepository)
    : IQueryHandler<GetDomainClaimQuery, DomainClaimDto>
{
    public const string InvalidLinkMessage =
        "This link is invalid or has expired. Sign up again with your work email to get a new one.";

    public async ValueTask<DomainClaimDto> Handle(GetDomainClaimQuery query, CancellationToken cancellationToken)
    {
        var verification = await domainVerificationRepository.GetByLinkTokenHashAsync(SecretTokens.Hash(query.Token), cancellationToken);
        if (verification is null || !verification.HasLinkUntil(DateTime.UtcNow) || verification.Company.Domain is not { } domain)
            throw new NotFoundException(InvalidLinkMessage);

        return new DomainClaimDto
        {
            CompanyName = verification.Company.Name,
            Domain = domain,
            Email = verification.Email,
            IsVerified = verification.Company.IsDomainVerified,
            IsUsed = verification.IsSpent,
            Record = DomainOwnership.Record(domain, verification),
            ExpiresAt = verification.LinkExpiresAt!.Value
        };
    }
}
