using Gratia.Application.DTOs.Invitation;
using Gratia.Application.Mappings;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Invitations.Queries;

public sealed record GetPendingInvitationsQuery(Guid CompanyId) : IQuery<IReadOnlyList<InvitationDto>>;

public sealed class GetPendingInvitationsQueryHandler(IInvitationRepository invitationRepository)
    : IQueryHandler<GetPendingInvitationsQuery, IReadOnlyList<InvitationDto>>
{
    public async ValueTask<IReadOnlyList<InvitationDto>> Handle(GetPendingInvitationsQuery query, CancellationToken cancellationToken)
    {
        var invitations = await invitationRepository.GetPendingInvitationsByCompanyIdAsync(query.CompanyId);
        return invitations.Select(i => i.ToDto()).ToList();
    }
}
