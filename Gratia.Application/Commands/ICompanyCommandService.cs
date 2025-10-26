using Gratia.Application.DTOs.CompanyDTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Application.Command
{
    public interface ICompanyCommandService
    {
        Task<ReadCompanyDto> AddCompanyAsync(RegisterCompanyDto registerCompanyDto);
        Task DeleteCompanyAsync(Guid id);
        Task<ReadCompanyDto> UpdateCompanyAsync(UpdateCompanyDto updateCompanyDto);
    }
}
