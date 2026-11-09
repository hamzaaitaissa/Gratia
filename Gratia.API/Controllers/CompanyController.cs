using Gratia.Application.DTOs.CompanyDTO;
using Gratia.Application.Features.Companies.Commands;
using Gratia.Application.Features.Companies.Queries;
using Gratia.Application.Interfaces;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gratia.API.Controllers
{
    // Callers only ever see their own company. There's deliberately no list of companies: no role may see
    // the others, and the list would reveal which domains have a workspace (TASKS.md #22).
    // Companies are created by signing up (POST /api/auth/register-company).
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CompanyController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly ICurrentUserService _currentUserService;

        public CompanyController(ISender sender, ICurrentUserService currentUserService)
        {
            _sender = sender;
            _currentUserService = currentUserService;
        }

        // The signed-in user's company, e.g. for the frontend's branding
        [HttpGet("me")]
        public async Task<ActionResult<ReadCompanyDto>> GetMine(CancellationToken cancellationToken)
        {
            var companyId = _currentUserService.CompanyId;
            var company = await _sender.Send(new GetCompanyByIdQuery(companyId, companyId), cancellationToken);
            return Ok(company);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ReadCompanyDto>> Get(Guid id, CancellationToken cancellationToken)
        {
            var company = await _sender.Send(new GetCompanyByIdQuery(id, _currentUserService.CompanyId), cancellationToken);
            return Ok(company);
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCompanyDto updateCompanyDto, CancellationToken cancellationToken)
        {
            if (id != updateCompanyDto.Id)
                return Problem(detail: "Id in route and body do not match.", statusCode: StatusCodes.Status400BadRequest);

            var company = await _sender.Send(new UpdateCompanyCommand(_currentUserService.CompanyId, updateCompanyDto), cancellationToken);
            return Ok(company);
        }
    }
}
