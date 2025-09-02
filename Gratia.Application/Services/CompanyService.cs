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
    public class CompanyService : ICompanyService
    {
        private readonly ICompanyRepository _companyRepository;

        public CompanyService(ICompanyRepository companyRepository)
        {
            _companyRepository = companyRepository;
        }

        public async Task<ReadCompanyDto> AddCompanyAsync(RegisterCompanyDto registerCompanyDto)
        {
            //turning from dto to model
            var company = new Company
            {
                Name = registerCompanyDto.Name,
                PrimaryColor = registerCompanyDto.PrimaryColor,
                SecondaryColor = registerCompanyDto.SecondaryColor,
                LogoUrl = registerCompanyDto.LogoUrl,
            };
            //adding
            var companyAdd = await _companyRepository.AddAsync(company);
            return MapToReadDto(companyAdd);
        }

        public async Task DeleteCompanyAsync(Guid id)
        {
            await _companyRepository.DeleteAsync(id);
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

        public async Task<ReadCompanyDto> UpdateCompanyAsync(UpdateCompanyDto updateCompanyDto)
        {
            if (updateCompanyDto == null)
                throw new ArgumentNullException(nameof(updateCompanyDto));

            var company = new Company();
            company.Update(
                updateCompanyDto.Name,
                updateCompanyDto.LogoUrl,
                updateCompanyDto.PrimaryColor,
                updateCompanyDto.SecondaryColor
            );

            var updatedCompany = await _companyRepository.UpdateAsync(company);
            return MapToReadDto(updatedCompany);
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
