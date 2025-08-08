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
            //from model to dto
            var companyRead = new ReadCompanyDto
            {
                Name = companyAdd.Name,
                PrimaryColor = companyAdd.PrimaryColor,
                SecondaryColor = companyAdd.SecondaryColor,
                LogoUrl = companyAdd.LogoUrl,
            };
            return companyRead;
        }

        public async Task DeleteCompanyAsync(Guid id)
        {
            await _companyRepository.DeleteAsync(id);
        }

        public Task<IEnumerable<ReadCompanyDto>> GetAllCompaniesAsync()
        {
            throw new NotImplementedException();
        }

        public Task<ReadCompanyDto> GetCompanyAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        public Task<ReadCompanyDto> UpdateCompanyAsync(UpdateCompanyDto updateCompanyDto)
        {
            throw new NotImplementedException();
        }
    }
}
