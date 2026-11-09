using Gratia.Application.DTOs.CompanyDTO;
using Gratia.Application.DTOs.UserDTO;
using Gratia.Application.Features.Auth.Commands;
using Gratia.Application.Features.Companies.Commands;
using Gratia.Application.Interfaces;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
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

        // 403 for a deactivated account, once the password is right (#16)
        [HttpPost("login")]
        [EnableRateLimiting(RateLimitPolicies.Login)]
        public async Task<IActionResult> Login(LoginDto loginDto, CancellationToken cancellationToken)
        {
            var tokenResponse = await _sender.Send(new LoginCommand(loginDto), cancellationToken);
            if (tokenResponse is null)
                return Problem(detail: "Email or Password are incorrect", statusCode: StatusCodes.Status400BadRequest);

            return Ok(tokenResponse);
        }

        [HttpPost("refresh-token")]
        [EnableRateLimiting(RateLimitPolicies.Refresh)]
        public async Task<IActionResult> RefreshToken([FromBody] string refreshToken, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(refreshToken))
                return Problem(detail: "Invalid client request", statusCode: StatusCodes.Status400BadRequest);

            var tokenResponse = await _sender.Send(new RefreshTokenCommand(refreshToken), cancellationToken);
            if (tokenResponse is null)
                return Problem(detail: "Invalid refresh token", statusCode: StatusCodes.Status401Unauthorized);

            return Ok(tokenResponse);
        }

        // Signing out (#15). With the refresh token as the body (a JSON string, like refresh-token), only that device is
        // signed out, and no access token is needed. With no body, every device of the signed-in caller is signed out.
        // Always 204, so it doesn't reveal whether a token existed.
        [HttpPost("revoke-token")]
        [AllowAnonymous]
        public async Task<IActionResult> RevokeToken([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] string? refreshToken,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(refreshToken) && _currentUserService.UserId == Guid.Empty)
                return Problem(detail: "Send the refresh token to sign out this device, or sign in to sign out everywhere.",
                    statusCode: StatusCodes.Status401Unauthorized);

            await _sender.Send(new RevokeRefreshTokenCommand(refreshToken, _currentUserService.UserId), cancellationToken);
            return NoContent();
        }
    }
}
