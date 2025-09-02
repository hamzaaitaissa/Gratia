using Gratia.Application.DTOs.CompanyDTO;
using Gratia.Application.DTOs.UserDTO;
using Gratia.Application.Interfaces;
using Gratia.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Gratia.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CompanyController : ControllerBase
    {

        private readonly ICompanyService _companyService;
        private readonly IUserService _userService;

        public CompanyController(ICompanyService companyService,IUserService userService)
        {
            _companyService = companyService;
            _userService = userService;
        }

        [HttpGet]
        public async Task<ActionResult<ReadCompanyDto>> GetAll()
        {
            var companies = await _companyService.GetAllCompaniesAsync();
            return companies is null ? NotFound() : Ok(companies);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult> Get(Guid id)
        {
            var company = await _companyService.GetCompanyAsync(id);
            return company is null ? NotFound() : Ok(company);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody]RegisterCompanyDto registerCompanyDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);
            var company = await _companyService.AddCompanyAsync(registerCompanyDto);
            return CreatedAtAction(
                actionName: nameof(Get),
                routeValues: new { id = company.Id },
                value: company
             );
        }

        [HttpPut("{id:guid}")]
        [Authorize("Admin")]
        public async Task<IActionResult> Update(Guid id, [FromBody]UpdateCompanyDto updateCompanyDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);
            if (id != updateCompanyDto.Id)
                return BadRequest("Id in route and body do not match.");
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if(string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userIdGuid))
            {
                return Unauthorized("Invalid user identifier");
            }
            var user = await _userService.GetUserById(userIdGuid);
            if (user == null)
            {
                return NotFound("User not Found");
            }
            if(user.CompanyId != id)
            {
                return Forbid("You are not authorized to edit this Company");
            }
            var company = await _companyService.UpdateCompanyAsync(updateCompanyDto);
            return company is null ? NotFound("Company not found") : Ok(company);
        }

    }
}
