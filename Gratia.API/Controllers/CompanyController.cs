using Gratia.Application.DTOs.CompanyDTO;
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
    public class CompanyController : ControllerBase
    {

        private readonly ICompanyService _companyService;

        public CompanyController(ICompanyService companyService)
        {
            _companyService = companyService;
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
                actionName : nameof(Get),
                routeValues: new {CompanyId=company.Id},
                value: company
                );
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody]UpdateCompanyDto updateCompanyDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);
            if (id != updateCompanyDto.Id)
                return BadRequest("Id in route and body do not match.");
            var company = await _companyService.UpdateCompanyAsync(updateCompanyDto);
            return company is null ? NotFound() : Ok(company);
        }

    }
}
