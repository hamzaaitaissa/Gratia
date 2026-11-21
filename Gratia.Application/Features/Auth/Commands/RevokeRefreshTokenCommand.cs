using Gratia.Application.Interfaces;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Auth.Commands;

/// <summary>
/// Signing out. With a refresh token, only that device is signed out; holding the token is proof enough, so it needs
/// no access token. Without one, every device of the signed-in caller is signed out, and the access tokens they hold
/// stop working at once too (#50). The controller makes sure there is one or the other.
/// </summary>
public sealed record RevokeRefreshTokenCommand(string? RefreshToken, Guid CallerUserId) : ICommand;

public sealed class RevokeRefreshTokenCommandHandler(
    IRefreshTokenRepository refreshTokenRepository,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ITokenService tokenService)
    : ICommandHandler<RevokeRefreshTokenCommand>
{
    public async ValueTask<Unit> Handle(RevokeRefreshTokenCommand command, CancellationToken cancellationToken)
    {
        // One device: a single DELETE that runs immediately, so there's nothing for the unit of work to save
        if (!string.IsNullOrEmpty(command.RefreshToken))
        {
            await refreshTokenRepository.DeleteByHashAsync(tokenService.HashRefreshToken(command.RefreshToken), cancellationToken);
            return Unit.Value;
        }

        // Every device: no refresh token renews them, and a new session stamp refuses the access tokens they hold (#50)
        await using var dbTransaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        if (await userRepository.GetByIdAsync(command.CallerUserId) is { } user)
        {
            user.EndSessions();
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        await refreshTokenRepository.DeleteForUserAsync(command.CallerUserId, cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        return Unit.Value;
    }
}
