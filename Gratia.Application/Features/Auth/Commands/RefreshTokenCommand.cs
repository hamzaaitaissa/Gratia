using Gratia.Application.DTOs.Token;
using Gratia.Application.Interfaces;
using Gratia.Application.Mappings;
using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Auth.Commands;

// Returns null when the refresh token is unknown, expired or already used; the controller turns that into a 401
public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<TokenResponseDto?>;

public sealed class RefreshTokenCommandHandler(
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork,
    ITokenService tokenService)
    : ICommandHandler<RefreshTokenCommand, TokenResponseDto?>
{
    public async ValueTask<TokenResponseDto?> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var stored = await refreshTokenRepository.GetByHashAsync(tokenService.HashRefreshToken(command.RefreshToken));
        // Deactivating deletes the user's tokens; this also covers a refresh that read the token just before that (#16)
        if (stored is null || stored.ExpiresAt <= now || stored.User.DeactivatedAt is not null)
            return null;

        // Rotation: the used token is replaced, so a copy of it stops working once its owner has refreshed. Deleting it
        // and storing the new one commit together, so a failure can't sign the device out.
        await using var dbTransaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        if (!await refreshTokenRepository.TryConsumeAsync(stored.Id, now, cancellationToken))
            return null; // another refresh with the same token got there first

        // From the user as they are now, so a changed role or a move takes effect at the next refresh
        var tokens = tokenService.GenerateTokens(stored.User.ToReadDto());
        refreshTokenRepository.Add(new RefreshToken(stored.UserId, tokenService.HashRefreshToken(tokens.RefreshToken), tokens.RefreshTokenExpiryTime));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);
        return tokens;
    }
}
