 using Gratia.Application.DTOs.UserDTO;
using Gratia.Application.Interfaces;
using Gratia.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Gratia.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<UserController> _logger;

        public UserController(IUserService userService, ILogger<UserController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] RegisterUserDto registerUserDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);
            //create the user
            var user = await _userService.AddUserAsync(registerUserDto);
            //return 201 created response
            return CreatedAtAction(
                //method
                actionName: nameof(Get),
                //parameters needed for this method
                routeValues: new { UserId = user.Id },
                //resource
                value: user
                );
        }

        [HttpGet("{userId:guid}")]
        public async Task<IActionResult> Get(Guid userId)
        {
            //get the user
            var user = await _userService.GetUserById(userId);
            //return 200 ok reponse
            return user is null ? NotFound() : Ok(user);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ReadUserDto>>> GetAll()
        {
            //get all the users
            var users = await _userService.GetAllUsersAsync();
            //return 200 response
            return users is null ? NotFound() : Ok(users);

        }
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var user = await _userService.GetUserById(id);
            if (user == null)
                return NotFound();

            await _userService.DeleteUserAsync(id);
            return NoContent();
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserDto updateUserDto)
        {
            if (id != updateUserDto.Id)
                return BadRequest("Id in route and body do not match.");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _userService.UpdateUserAsync(updateUserDto);
            if (user == null)
                return NotFound();

            return Ok(user);
        }
    }
}

//TODO: add authorization for super admin 