using Gratia.Application.Exceptions;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Invitations.Commands;

public sealed record CancelInvitationCommand(Guid InvitationId, Guid CallerCompanyId) : ICommand;

public sealed class CancelInvitationCommandHandler(IInvitationRepository invitationRepository, IUnitOfWork unitOfWork)
    : ICommandHandler<CancelInvitationCommand>
{
    public async ValueTask<Unit> Handle(CancelInvitationCommand command, CancellationToken cancellationToken)
    {
        // Another company's invitation is reported as missing, so its existence isn't revealed
        var invitation = await invitationRepository.GetByIdAsync(command.InvitationId);
        if (invitation is null || invitation.CompanyId != command.CallerCompanyId)
            throw new NotFoundException("Invitation not found");

        // An accepted invitation is the record of how the account was created
        if (invitation.IsUsed)
            throw new BusinessRuleException("This invitation has already been accepted");

        invitationRepository.Remove(invitation);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
