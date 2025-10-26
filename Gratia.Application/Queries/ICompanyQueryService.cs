using Gratia.Application.DTOs.CompanyDTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Application.Interfaces
{
    public interface ICompanyQueryService
    {
        Task<ReadCompanyDto> GetCompanyAsync(Guid id);
        Task<IEnumerable<ReadCompanyDto>> GetAllCompaniesAsync();
    }
}
