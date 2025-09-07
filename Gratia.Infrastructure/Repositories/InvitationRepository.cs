using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;
using Gratia.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Infrastructure.Repositories
{
    public class InvitationRepository : IInvitationRepository
    {
        private readonly GratiaDbContext _gratiaDbContext;

        public InvitationRepository(GratiaDbContext gratiaDbContext)
        {
            _gratiaDbContext = gratiaDbContext;
        }

        public async Task<CompanyInvitation> CreateAsync(CompanyInvitation invitation)
        {
            throw new NotImplementedException();
        }

        public Task<bool> DeleteAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        public Task<CompanyInvitation> GetByEmailAndCompanyIdAsync(string email, Guid companyId)
        {
            throw new NotImplementedException();
        }

        public Task<CompanyInvitation> GetByTokenAsync(string token)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<CompanyInvitation>> GetPendingInvitationsByCompanyIdAsync(Guid companyId)
        {
            throw new NotImplementedException();
        }

        public Task<CompanyInvitation> UpdateAsync(CompanyInvitation invitation)
        {
            throw new NotImplementedException();
        }
    }
}
