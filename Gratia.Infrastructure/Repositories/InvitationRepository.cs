using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;
using Gratia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
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
            _gratiaDbContext.CompanyInvitations.Add(invitation);
            await _gratiaDbContext.SaveChangesAsync();
            return invitation;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var invitation = await _gratiaDbContext.CompanyInvitations.FindAsync(id);
            if (invitation == null) return false;

            _gratiaDbContext.CompanyInvitations.Remove(invitation);
            await _gratiaDbContext.SaveChangesAsync();
            return true;
        }

        public async Task<CompanyInvitation> GetByEmailAndCompanyIdAsync(string email, Guid companyId)
        {
            return await _gratiaDbContext.CompanyInvitations.FirstOrDefaultAsync(i => i.Email.ToLower() == email.ToLower()
                    && i.CompanyId == companyId && !i.IsUsed && !i.IsExpired); ;
        }

        public async Task<CompanyInvitation> GetByTokenAsync(string token)
        {
            return await _gratiaDbContext.CompanyInvitations.FirstOrDefaultAsync(i => i.InvitationToken == token);
        }

        public async Task<IEnumerable<CompanyInvitation>> GetPendingInvitationsByCompanyIdAsync(Guid companyId)
        {
            var invitations = await _gratiaDbContext.CompanyInvitations.Where(i=>i.CompanyId == companyId && !i.IsExpired).Include(i => i.InvitedByUser)
                .OrderByDescending(i => i.CreatedDate).ToListAsync();
            return invitations;
        }

        public async Task<CompanyInvitation> UpdateAsync(CompanyInvitation invitation)
        {
            _gratiaDbContext.CompanyInvitations.Update(invitation);
            await _gratiaDbContext.SaveChangesAsync();
            return invitation;
        }
    }
}
