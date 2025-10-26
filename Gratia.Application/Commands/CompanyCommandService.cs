using Gratia.Application.DTOs.CompanyDTO;
using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Application.Command
{
    public class CompanyCommandService : ICompanyCommandService
    {
        private readonly ICompanyRepository _companyRepository;

        public CompanyCommandService(ICompanyRepository companyRepository)
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
