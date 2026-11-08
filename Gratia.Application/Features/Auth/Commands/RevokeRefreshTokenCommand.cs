using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Auth.Commands;

public sealed record RevokeRefreshTokenCommand(Guid UserId) : ICommand;

public sealed class RevokeRefreshTokenCommandHandler(IUserRepository userRepository, IUnitOfWork unitOfWork)
    : ICommandHandler<RevokeRefreshTokenCommand>
{
    public async ValueTask<Unit> Handle(RevokeRefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(command.UserId);
        if (user is null)
            return Unit.Value;

        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
