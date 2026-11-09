using Gratia.Application.Interfaces;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Auth.Commands;

/// <summary>
/// Signing out. With a refresh token, only that device is signed out; holding the token is proof enough, so it needs
/// no access token. Without one, every device of the signed-in caller is signed out. The controller makes sure there
/// is one or the other.
/// </summary>
public sealed record RevokeRefreshTokenCommand(string? RefreshToken, Guid CallerUserId) : ICommand;

public sealed class RevokeRefreshTokenCommandHandler(IRefreshTokenRepository refreshTokenRepository, ITokenService tokenService)
    : ICommandHandler<RevokeRefreshTokenCommand>
{
    public async ValueTask<Unit> Handle(RevokeRefreshTokenCommand command, CancellationToken cancellationToken)
    {
        // Single DELETEs that run immediately, so there's nothing for the unit of work to save
        if (!string.IsNullOrEmpty(command.RefreshToken))
            await refreshTokenRepository.DeleteByHashAsync(tokenService.HashRefreshToken(command.RefreshToken), cancellationToken);
        else
            await refreshTokenRepository.DeleteForUserAsync(command.CallerUserId, cancellationToken);

        return Unit.Value;
    }
}
