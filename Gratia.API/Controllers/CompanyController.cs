using Gratia.Application.DTOs.CompanyDTO;
using Gratia.Application.Features.Companies.Commands;
using Gratia.Application.Features.Companies.Queries;
using Gratia.Application.Interfaces;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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

        // Domain verification (#61), for any member: an admin verifies the workspace's domain, anyone else becomes an
        // admin by it. Each member has their own DNS record.

        // Whether the domain is verified, and the caller's record if they've started. Domain is null for a workspace with none.
        [HttpGet("domain-verification")]
        public async Task<ActionResult<DomainVerificationDto>> GetDomainVerification(CancellationToken cancellationToken)
        {
            return Ok(await _sender.Send(new GetDomainVerificationQuery(_currentUserService.UserId, _currentUserService.CompanyId),
                cancellationToken));
        }

        // The caller's record to add to the domain's DNS: the same one each time. 409 for an admin of a verified workspace.
        [HttpPost("domain-verification")]
        public async Task<ActionResult<DomainVerificationDto>> StartDomainVerification(CancellationToken cancellationToken)
        {
            return Ok(await _sender.Send(new StartDomainVerificationCommand(_currentUserService.UserId, _currentUserService.CompanyId),
                cancellationToken));
        }

        // Looks for the caller's record. 200 whether it's found or not: the status says which, and the message what to do.
        [HttpPost("domain-verification/check")]
        [EnableRateLimiting(RateLimitPolicies.DomainCheck)]
        public async Task<ActionResult<DomainCheckResultDto>> CheckDomainVerification(CancellationToken cancellationToken)
        {
            return Ok(await _sender.Send(new CheckDomainVerificationCommand(_currentUserService.UserId, _currentUserService.CompanyId),
                cancellationToken));
        }
    }
}
