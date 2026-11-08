using Gratia.Application.DTOs.Token;
using Gratia.Application.Interfaces;
using Gratia.Application.Mappings;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Auth.Commands;

// Returns null when the refresh token is unknown or expired; the controller turns that into a 401
public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<TokenResponseDto?>;

public sealed class RefreshTokenCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ITokenService tokenService)
    : ICommandHandler<RefreshTokenCommand, TokenResponseDto?>
{
    public async ValueTask<TokenResponseDto?> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetUserByRefreshTokenAsync(command.RefreshToken);
        if (user is null || user.RefreshToken != command.RefreshToken || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            return null;

        var tokenResponse = tokenService.GenerateTokens(user.ToReadDto());
        user.RefreshToken = tokenResponse.RefreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return tokenResponse;
    }
}
