using Gratia.Application.Exceptions;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Invitations.Commands;

public sealed record CancelInvitationCommand(Guid InvitationId) : ICommand;

public sealed class CancelInvitationCommandHandler(IInvitationRepository invitationRepository, IUnitOfWork unitOfWork)
    : ICommandHandler<CancelInvitationCommand>
{
    public async ValueTask<Unit> Handle(CancelInvitationCommand command, CancellationToken cancellationToken)
    {
        var invitation = await invitationRepository.GetByIdAsync(command.InvitationId)
            ?? throw new NotFoundException("Invitation not found");

        invitationRepository.Remove(invitation);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
