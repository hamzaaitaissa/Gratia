using Gratia.Application.DTOs.CompanyDTO;
using Gratia.Application.DTOs.Token;
using Gratia.Application.DTOs.UserDTO;
using Gratia.Application.Features.Auth.Commands;
using Gratia.Application.Features.Companies.Commands;
using Gratia.Application.Features.Companies.Queries;
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

        // A forgotten-password link (#50). Always 202 for valid input: what happened is told only by email, so this can't
        // be used to find out who has an account.
        [HttpPost("forgot-password")]
        [EnableRateLimiting(RateLimitPolicies.PasswordReset)]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto, CancellationToken cancellationToken)
        {
            await _sender.Send(new ForgotPasswordCommand(forgotPasswordDto), cancellationToken);
            return Accepted();
        }

        // Sets the new password with the link's token, ends every other session and signs in here, with the same tokens
        // as login (#50). 400 when the link is unknown, used or expired.
        [HttpPost("reset-password")]
        [EnableRateLimiting(RateLimitPolicies.Login)]
        public async Task<ActionResult<TokenResponseDto>> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto, CancellationToken cancellationToken)
        {
            return Ok(await _sender.Send(new ResetPasswordCommand(resetPasswordDto), cancellationToken));
        }

        // Claiming a workspace through DNS (#61), with the link from signup's "already on Gratia" email: the workspace and
        // the record to add. 404 when the link is unknown or expired.
        [HttpGet("domain-claim/{token}")]
        public async Task<ActionResult<DomainClaimDto>> GetDomainClaim(string token, CancellationToken cancellationToken)
        {
            return Ok(await _sender.Send(new GetDomainClaimQuery(token), cancellationToken));
        }

        // Looks for the claimant's record. Found: the workspace is verified, and the answer carries an admin invitation's
        // token for the accept page (or, with an account there already, the account is now an admin). 200 either way.
        [HttpPost("domain-claim/check")]
        [EnableRateLimiting(RateLimitPolicies.DomainCheck)]
        public async Task<ActionResult<DomainCheckResultDto>> CheckDomainClaim([FromBody] CheckDomainClaimDto checkDomainClaimDto,
            CancellationToken cancellationToken)
        {
            return Ok(await _sender.Send(new CheckDomainClaimCommand(checkDomainClaimDto.Token!), cancellationToken));
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
