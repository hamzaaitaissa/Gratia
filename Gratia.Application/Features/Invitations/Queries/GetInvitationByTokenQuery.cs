using Gratia.Application.DTOs.Invitation;
using Gratia.Application.Exceptions;
using Gratia.Application.Mappings;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Invitations.Queries;

public sealed record GetInvitationByTokenQuery(string Token) : IQuery<InvitationDto>;

public sealed class GetInvitationByTokenQueryHandler(IInvitationRepository invitationRepository)
    : IQueryHandler<GetInvitationByTokenQuery, InvitationDto>
{
    public async ValueTask<InvitationDto> Handle(GetInvitationByTokenQuery query, CancellationToken cancellationToken)
    {
        var invitation = await invitationRepository.GetByTokenAsync(query.Token)
            ?? throw new NotFoundException("Invitation not found");

        return invitation.ToDto();
    }
}
