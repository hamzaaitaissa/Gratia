using Gratia.Application.DTOs.Token;
using Gratia.Application.DTOs.UserDTO;
using Gratia.Application.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Gratia.Infrastructure.Authentication
{
    public class TokenService : ITokenService
    {
        private readonly JwtOptions _jwtOptions;

        public TokenService(IOptions<JwtOptions> jwtOptions)
        {
            _jwtOptions = jwtOptions.Value;
        }

        public string GenerateRefreshToken()
        {
            var randomNumber = new Byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        public TokenResponseDto GenerateTokens(ReadUserDto readUserDto)
        {
            var accessToken = CreateToken(readUserDto);
            var refreshToken = GenerateRefreshToken();
            return new TokenResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                RefreshTokenExpiryTime = DateTime.UtcNow.AddHours(1)
            };
        }

        public ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = false, // We don't care about expiration here
                ValidateIssuerSigningKey = true,
                ValidIssuer = _jwtOptions.Issuer,
                ValidAudience = _jwtOptions.Audience,
                IssuerSigningKey = _jwtOptions.CreateSigningKey()
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);

            if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha512,
                StringComparison.InvariantCultureIgnoreCase))
            {
                throw new SecurityTokenException("Invalid token");
            }

            return principal;
        }

        private string CreateToken(ReadUserDto user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("CompanyId", user.CompanyId.ToString())
            };

            var creds = new SigningCredentials(_jwtOptions.CreateSigningKey(), SecurityAlgorithms.HmacSha512);

            var jwtDescriptor = new JwtSecurityToken(
                issuer: _jwtOptions.Issuer,
                audience: _jwtOptions.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds
                );

            return new JwtSecurityTokenHandler().WriteToken(jwtDescriptor);
        }
    }
}
