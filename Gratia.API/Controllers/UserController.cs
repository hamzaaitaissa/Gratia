using Gratia.Application.DTOs.UserDTO;
using Gratia.Application.Features.Users.Commands;
using Gratia.Application.Features.Users.Queries;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gratia.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly ISender _sender;

        public UserController(ISender sender)
        {
            _sender = sender;
        }

        // Accounts are created by accepting an invitation (POST /api/invitation/accept)

        [HttpGet("{userId:guid}")]
        public async Task<ActionResult<ReadUserDto>> Get(Guid userId, CancellationToken cancellationToken)
        {
            var user = await _sender.Send(new GetUserByIdQuery(userId), cancellationToken);
            return Ok(user);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ReadUserDto>>> GetAll(CancellationToken cancellationToken)
        {
            var users = await _sender.Send(new GetAllUsersQuery(), cancellationToken);
            return Ok(users);
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            await _sender.Send(new DeleteUserCommand(id), cancellationToken);
            return NoContent();
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserDto updateUserDto, CancellationToken cancellationToken)
        {
            if (id != updateUserDto.Id)
                return Problem(detail: "Id in route and body do not match.", statusCode: StatusCodes.Status400BadRequest);

            var user = await _sender.Send(new UpdateUserCommand(updateUserDto), cancellationToken);
            return Ok(user);
        }
    }
}

