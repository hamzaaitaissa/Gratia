using Gratia.Application.Command;
using Gratia.Application.DTOs.CompanyDTO;
using Gratia.Application.Interfaces;
using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Application.Services
{
    public class CompanyQueryService : ICompanyQueryService
    {
        private readonly ICompanyRepository _companyRepository;

        public CompanyQueryService(ICompanyRepository companyRepository)
        {
            _companyRepository = companyRepository;
        }

        public async Task<IEnumerable<ReadCompanyDto>> GetAllCompaniesAsync()
        {
            var companies = await _companyRepository.GetAllAsync();
            var companyRead = new List<ReadCompanyDto>();
            foreach(var company in companies)
            {
                companyRead.Add(MapToReadDto(company));
            }
            return companyRead;
           
        }

        public async Task<ReadCompanyDto> GetCompanyAsync(Guid id)
        {
            var company = await _companyRepository.GetAsync(id);
            return MapToReadDto(company);
        }

        private static ReadCompanyDto MapToReadDto(Company company)
        {
            return new ReadCompanyDto
            {
                Id = company.Id,
                Name = company.Name,
                PrimaryColor = company.PrimaryColor,
                SecondaryColor = company.SecondaryColor,
                LogoUrl = company.LogoUrl,
            };
        }
    }
}
