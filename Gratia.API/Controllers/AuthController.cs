using Gratia.Application.DTOs.UserDTO;
using Gratia.Application.Interfaces;
using Gratia.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Data.SqlTypes;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IConfiguration _configuration;
        private readonly ITokenService _tokenService;

        public AuthController(IUserService userService, IConfiguration configuration, ITokenService tokenService)
        {
            _userService = userService;
            _configuration = configuration;
            _tokenService = tokenService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto loginDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if(!await _userService.LoginUser(loginDto))
            {
                return BadRequest("Email or Password are incorrect");
            }
            var user = await _userService.GetUserByEmail(loginDto.Email);

            var tokenResponse = _tokenService.GenerateTokens(user);
            //savin refresh token to user
            await _userService.SaveRefreshTokenAsync(user.Email, tokenResponse.RefreshToken);

            return Ok(tokenResponse);
        }
        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] string refreshToken)
        {
            if (string.IsNullOrEmpty(refreshToken))
                return BadRequest("Invalid client request");

            var tokenResponse = await _userService.RefreshTokenAsync(refreshToken);

            if (tokenResponse == null)
                return Unauthorized("Invalid refresh token");

            return Ok(tokenResponse);
        }

        [HttpPost("revoke-token")]
        [Authorize]
        public async Task<IActionResult> RevokeToken()
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            await _userService.RevokeRefreshTokenAsync(email);
            return NoContent();
        }
    }

    
}
