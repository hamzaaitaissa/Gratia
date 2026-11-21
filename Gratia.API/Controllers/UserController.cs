using Gratia.Application.DTOs.Token;
using Gratia.Application.DTOs.UserDTO;
using Gratia.Application.Features.Auth.Commands;
using Gratia.Application.Features.Users.Commands;
using Gratia.Application.Features.Users.Queries;
using Gratia.Application.Interfaces;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Gratia.API.Controllers
{
    // Everything is scoped to the caller's company: users in other companies look like they don't exist (TASKS.md #22)
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly ICurrentUserService _currentUserService;

        public UserController(ISender sender, ICurrentUserService currentUserService)
        {
            _sender = sender;
            _currentUserService = currentUserService;
        }

        // Accounts are created by accepting an invitation (POST /api/invitation/accept)

        // The caller's own account (#50)
        [HttpGet("me")]
        public async Task<ActionResult<ReadUserDto>> GetMe(CancellationToken cancellationToken)
        {
            var user = await _sender.Send(new GetUserByIdQuery(_currentUserService.UserId, _currentUserService.CompanyId), cancellationToken);
            return Ok(user);
        }

        // The caller's own password (#50). Every session ends, this one's included; the response is this device's new
        // tokens, as login's. 400 when the current password is wrong; limited like login against guessing it.
        [HttpPost("me/password")]
        [EnableRateLimiting(RateLimitPolicies.Login)]
        public async Task<ActionResult<TokenResponseDto>> ChangePassword([FromBody] ChangePasswordDto changePasswordDto,
            CancellationToken cancellationToken)
        {
            var tokens = await _sender.Send(
                new ChangePasswordCommand(_currentUserService.UserId, _currentUserService.CompanyId, changePasswordDto), cancellationToken);
            return Ok(tokens);
        }

        [HttpGet("{userId:guid}")]
        public async Task<ActionResult<ReadUserDto>> Get(Guid userId, CancellationToken cancellationToken)
        {
            var user = await _sender.Send(new GetUserByIdQuery(userId, _currentUserService.CompanyId), cancellationToken);
            return Ok(user);
        }

        // The caller's active colleagues, by name
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ReadUserDto>>> GetAll(CancellationToken cancellationToken)
        {
            var users = await _sender.Send(new GetCompanyUsersQuery(_currentUserService.CompanyId), cancellationToken);
            return Ok(users);
        }

        // Users are deactivated instead of deleted, so their recognitions stay (#16)
        [HttpGet("deactivated")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<ReadUserDto>>> GetDeactivated(CancellationToken cancellationToken)
        {
            var users = await _sender.Send(new GetDeactivatedUsersQuery(_currentUserService.CompanyId), cancellationToken);
            return Ok(users);
        }

        [HttpPost("{id:guid}/deactivate")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
        {
            await _sender.Send(new DeactivateUserCommand(id, _currentUserService.UserId, _currentUserService.CompanyId), cancellationToken);
            return NoContent();
        }

        [HttpPost("{id:guid}/reactivate")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Reactivate(Guid id, CancellationToken cancellationToken)
        {
            await _sender.Send(new ReactivateUserCommand(id, _currentUserService.CompanyId), cancellationToken);
            return NoContent();
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserDto updateUserDto, CancellationToken cancellationToken)
        {
            if (id != updateUserDto.Id)
                return Problem(detail: "Id in route and body do not match.", statusCode: StatusCodes.Status400BadRequest);

            var user = await _sender.Send(new UpdateUserCommand(_currentUserService.CompanyId, updateUserDto), cancellationToken);
            return Ok(user);
        }
    }
}
