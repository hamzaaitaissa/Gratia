using Gratia.Application.DTOs.CompanyDTO;
using Gratia.Application.Features.Companies.Commands;
using Gratia.Application.Features.Companies.Queries;
using Gratia.Application.Interfaces;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gratia.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CompanyController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly ICurrentUserService _currentUserService;

        public CompanyController(ISender sender, ICurrentUserService currentUserService)
        {
            _sender = sender;
            _currentUserService = currentUserService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ReadCompanyDto>>> GetAll(CancellationToken cancellationToken)
        {
            var companies = await _sender.Send(new GetAllCompaniesQuery(), cancellationToken);
            return Ok(companies);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ReadCompanyDto>> Get(Guid id, CancellationToken cancellationToken)
        {
            var company = await _sender.Send(new GetCompanyByIdQuery(id), cancellationToken);
            return Ok(company);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] RegisterCompanyDto registerCompanyDto, CancellationToken cancellationToken)
        {
            var company = await _sender.Send(new CreateCompanyCommand(registerCompanyDto), cancellationToken);
            return CreatedAtAction(
                actionName: nameof(Get),
                routeValues: new { id = company.Id },
                value: company
             );
        }

        [HttpPut("{id:guid}")]
        [Authorize("Admin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCompanyDto updateCompanyDto, CancellationToken cancellationToken)
        {
            if (id != updateCompanyDto.Id)
                return Problem(detail: "Id in route and body do not match.", statusCode: StatusCodes.Status400BadRequest);

            var company = await _sender.Send(new UpdateCompanyCommand(_currentUserService.CompanyId, updateCompanyDto), cancellationToken);
            return Ok(company);
        }
    }
}
