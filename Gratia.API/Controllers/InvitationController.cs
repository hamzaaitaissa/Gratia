using Gratia.Application.DTOs.Invitation;
using Gratia.Application.Features.Invitations.Commands;
using Gratia.Application.Features.Invitations.Queries;
using Gratia.Application.Interfaces;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gratia.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")] // managing invitations is for admins; the invitee's two actions below are anonymous
    public class InvitationController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly ICurrentUserService _currentUserService;

        public InvitationController(ISender sender, ICurrentUserService currentUserService)
        {
            _sender = sender;
            _currentUserService = currentUserService;
        }

        [HttpPost]
        public async Task<ActionResult<InvitationDto>> Invite([FromBody] InviteUserDto inviteUserDto, CancellationToken cancellationToken)
        {
            var invitation = await _sender.Send(
                new InviteUserCommand(_currentUserService.CompanyId, _currentUserService.UserId, inviteUserDto), cancellationToken);
            return StatusCode(StatusCodes.Status201Created, invitation);
        }

        [HttpGet("pending")]
        public async Task<ActionResult<IEnumerable<InvitationDto>>> GetPending(CancellationToken cancellationToken)
        {
            var invitations = await _sender.Send(new GetPendingInvitationsQuery(_currentUserService.CompanyId), cancellationToken);
            return Ok(invitations);
        }

        [HttpPost("{id:guid}/resend")]
        public async Task<ActionResult<InvitationDto>> Resend(Guid id, CancellationToken cancellationToken)
        {
            var invitation = await _sender.Send(new ResendInvitationCommand(id, _currentUserService.CompanyId), cancellationToken);
            return Ok(invitation);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
        {
            await _sender.Send(new CancelInvitationCommand(id, _currentUserService.CompanyId), cancellationToken);
            return NoContent();
        }

        // For the accept page: shows which company the link is for and whether it's still valid
        [HttpGet("token/{token}")]
        [AllowAnonymous]
        public async Task<ActionResult<InvitationDto>> GetByToken(string token, CancellationToken cancellationToken)
        {
            var invitation = await _sender.Send(new GetInvitationByTokenQuery(token), cancellationToken);
            return Ok(invitation);
        }

        [HttpPost("accept")]
        [AllowAnonymous]
        public async Task<IActionResult> Accept([FromBody] AcceptInvitationDto acceptInvitationDto, CancellationToken cancellationToken)
        {
            await _sender.Send(new AcceptInvitationCommand(acceptInvitationDto), cancellationToken);
            return NoContent();
        }
    }
}
