using Gratia.Application.DTOs.Token;
using Gratia.Application.DTOs.UserDTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Application.Interfaces
{
    public interface ITokenService
    {
        TokenResponseDto GenerateTokens(ReadUserDto readUserDto);
        string GenerateRefreshToken();
        ClaimsPrincipal GetPrincipalFromExpiredToken(string token);
    }
}
