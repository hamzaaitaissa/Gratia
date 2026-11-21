using Gratia.Application.DTOs.Token;
using Gratia.Application.Interfaces;
using Gratia.Application.Mappings;
using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;

namespace Gratia.Application.Features.Auth;

internal static class Sessions
{
    // A new signed-in device (#15): tokens for the user as they are now, and the refresh token's row, staged for the
    // caller's SaveChangesAsync. Only the token's hash is stored.
    public static TokenResponseDto Start(User user, ITokenService tokenService, IRefreshTokenRepository refreshTokenRepository)
    {
        var tokens = tokenService.GenerateTokens(user.ToReadDto(), user.SessionStamp);
        refreshTokenRepository.Add(new RefreshToken(user.Id, tokenService.HashRefreshToken(tokens.RefreshToken), tokens.RefreshTokenExpiryTime));
        return tokens;
    }
}
