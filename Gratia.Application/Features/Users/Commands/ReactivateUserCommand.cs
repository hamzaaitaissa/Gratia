using Gratia.Application.Exceptions;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Users.Commands;

// Undoes DeactivateUserCommand (#16): the user can sign in again with their old password, and keeps their history and
// balances. Admins can only reactivate users in their own company. Doing it twice is fine.
public sealed record ReactivateUserCommand(Guid UserId, Guid CallerCompanyId) : ICommand;

public sealed class ReactivateUserCommandHandler(IUserRepository userRepository, IUnitOfWork unitOfWork)
    : ICommandHandler<ReactivateUserCommand>
{
    public async ValueTask<Unit> Handle(ReactivateUserCommand command, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdInCompanyIncludingDeactivatedAsync(command.UserId, command.CallerCompanyId)
            ?? throw new NotFoundException($"User {command.UserId} was not found.");

        user.Reactivate();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
