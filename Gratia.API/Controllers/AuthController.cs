using Gratia.Application.DTOs.CompanyDTO;
using Gratia.Application.DTOs.UserDTO;
using Gratia.Application.Features.Auth.Commands;
using Gratia.Application.Features.Companies.Commands;
using Gratia.Application.Interfaces;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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

        // Always 202 once the input is valid: what happened is told only to the mailbox owner, by email
        [HttpPost("register-company")]
        [EnableRateLimiting(RateLimitPolicies.Signup)]
        public async Task<IActionResult> RegisterCompany([FromBody] CompanySignupDto companySignupDto, CancellationToken cancellationToken)
        {
            await _sender.Send(new RegisterCompanyCommand(companySignupDto), cancellationToken);
            return Accepted();
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
