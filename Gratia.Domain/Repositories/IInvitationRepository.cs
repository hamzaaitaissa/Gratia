using Gratia.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Domain.Repositories
{
    public interface IInvitationRepository
    {
        Task<CompanyInvitation> CreateAsync(CompanyInvitation invitation);
        Task<CompanyInvitation> GetByTokenAsync(string token);
        Task<CompanyInvitation> GetByEmailAndCompanyIdAsync(string email, Guid companyId);
        Task<IEnumerable<CompanyInvitation>> GetPendingInvitationsByCompanyIdAsync(Guid companyId);
        Task<CompanyInvitation> UpdateAsync(CompanyInvitation invitation);
        Task<bool> DeleteAsync(Guid id);

    }
}
