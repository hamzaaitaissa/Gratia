using Gratia.Application.DTOs.Invitation;
using Gratia.Application.DTOs.Token;
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

        // Bulk invitations (#54): the template to fill in, what importing an uploaded copy would do with each row, then
        // the import itself, which checks each row again. Admins only, into their own company.
        [HttpGet("import/template")]
        public async Task<IActionResult> ImportTemplate(CancellationToken cancellationToken)
        {
            var workbook = await _sender.Send(new GetInvitationImportTemplateQuery(), cancellationToken);
            return File(workbook, XlsxContentType, "gratia-invitations.xlsx");
        }

        // A multipart upload with the file as "file". The limits leave room for the multipart framing around 1 MB.
        [HttpPost("import/preview")]
        [RequestSizeLimit(2 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 2 * 1024 * 1024)]
        public async Task<ActionResult<ImportPreviewDto>> PreviewImport(IFormFile? file, CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
                return Problem(detail: "Choose a spreadsheet to upload.", statusCode: StatusCodes.Status400BadRequest);
            if (file.Length > PreviewInvitationImportQuery.MaxFileBytes)
                return Problem(detail: "The file is larger than 1 MB. Split the list and import each part.", statusCode: StatusCodes.Status400BadRequest);

            await using var stream = file.OpenReadStream();
            return Ok(await _sender.Send(new PreviewInvitationImportQuery(_currentUserService.CompanyId, stream), cancellationToken));
        }

        [HttpPost("import")]
        public async Task<ActionResult<ImportResultDto>> Import([FromBody] ImportInvitationsDto importDto, CancellationToken cancellationToken)
        {
            var command = new ImportInvitationsCommand(_currentUserService.CompanyId, _currentUserService.UserId, importDto.Rows!);
            return Ok(await _sender.Send(command, cancellationToken));
        }

        private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        // Creates the account and signs it in (#66): the same tokens as login
        [HttpPost("accept")]
        [AllowAnonymous]
        public async Task<ActionResult<TokenResponseDto>> Accept([FromBody] AcceptInvitationDto acceptInvitationDto, CancellationToken cancellationToken)
        {
            return Ok(await _sender.Send(new AcceptInvitationCommand(acceptInvitationDto), cancellationToken));
        }
    }
}
