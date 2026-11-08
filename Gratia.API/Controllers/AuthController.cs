using Gratia.Application.DTOs.UserDTO;
using Gratia.Application.Features.Auth.Commands;
using Gratia.Application.Interfaces;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gratia.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly ICurrentUserService _currentUserService;

        public AuthController(ISender sender, ICurrentUserService currentUserService)
        {
            _sender = sender;
            _currentUserService = currentUserService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto loginDto, CancellationToken cancellationToken)
        {
            var tokenResponse = await _sender.Send(new LoginCommand(loginDto), cancellationToken);
            if (tokenResponse is null)
                return Problem(detail: "Email or Password are incorrect", statusCode: StatusCodes.Status400BadRequest);

            return Ok(tokenResponse);
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] string refreshToken, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(refreshToken))
                return Problem(detail: "Invalid client request", statusCode: StatusCodes.Status400BadRequest);

            var tokenResponse = await _sender.Send(new RefreshTokenCommand(refreshToken), cancellationToken);
            if (tokenResponse is null)
                return Problem(detail: "Invalid refresh token", statusCode: StatusCodes.Status401Unauthorized);

            return Ok(tokenResponse);
        }

        [HttpPost("revoke-token")]
        [Authorize]
        public async Task<IActionResult> RevokeToken(CancellationToken cancellationToken)
        {
            await _sender.Send(new RevokeRefreshTokenCommand(_currentUserService.UserId), cancellationToken);
            return NoContent();
        }
    }
}
