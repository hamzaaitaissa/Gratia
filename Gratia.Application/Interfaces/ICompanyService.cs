using Gratia.Application.DTOs.CompanyDTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Application.Interfaces
{
    public interface ICompanyService
    {
        Task<ReadCompanyDto> GetCompanyAsync(Guid id);
        Task<IEnumerable<ReadCompanyDto>> GetAllCompaniesAsync();
        Task<ReadCompanyDto> AddCompanyAsync(RegisterCompanyDto registerCompanyDto);
        Task DeleteCompanyAsync(Guid id);
        Task<ReadCompanyDto> UpdateCompanyAsync(UpdateCompanyDto updateCompanyDto);
    }
}
