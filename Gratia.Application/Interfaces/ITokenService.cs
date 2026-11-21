using Gratia.Application.DTOs.Token;
using Gratia.Application.DTOs.UserDTO;
using System.Security.Claims;

namespace Gratia.Application.Interfaces
{
    public interface ITokenService
    {
        // A new access token and a new random refresh token, with the refresh token's expiry. The access token carries the
        // user's session stamp, so changing the stamp refuses it at once (#50).
        TokenResponseDto GenerateTokens(ReadUserDto readUserDto, Guid sessionStamp);

        // What's stored and looked up instead of the refresh token itself (#27)
        string HashRefreshToken(string refreshToken);

        ClaimsPrincipal GetPrincipalFromExpiredToken(string token);
    }
}
