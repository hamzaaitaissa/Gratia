using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;
using Gratia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Gratia.Infrastructure.Repositories
{
    public class InvitationRepository : IInvitationRepository
    {
        private readonly GratiaDbContext _gratiaDbContext;

        public InvitationRepository(GratiaDbContext gratiaDbContext)
        {
            _gratiaDbContext = gratiaDbContext;
        }

        public void Add(CompanyInvitation invitation)
        {
            _gratiaDbContext.CompanyInvitations.Add(invitation);
        }

        public void Remove(CompanyInvitation invitation)
        {
            _gratiaDbContext.CompanyInvitations.Remove(invitation);
        }

        public async Task<CompanyInvitation?> GetByIdAsync(Guid id)
        {
            return await _gratiaDbContext.CompanyInvitations.FindAsync(id);
        }

        public async Task<CompanyInvitation?> GetByEmailAndCompanyIdAsync(string email, Guid companyId)
        {
            return await _gratiaDbContext.CompanyInvitations.FirstOrDefaultAsync(i => i.Email.ToLower() == email.ToLower()
                    && i.CompanyId == companyId && !i.IsUsed && !i.IsExpired); ;
        }

        public async Task<CompanyInvitation?> GetByTokenAsync(string token)
        {
            return await _gratiaDbContext.CompanyInvitations.FirstOrDefaultAsync(i => i.InvitationToken == token);
        }

        public async Task<IEnumerable<CompanyInvitation>> GetPendingInvitationsByCompanyIdAsync(Guid companyId)
        {
            var invitations = await _gratiaDbContext.CompanyInvitations.Where(i=>i.CompanyId == companyId && !i.IsExpired).Include(i => i.InvitedByUser)
                .OrderByDescending(i => i.CreatedDate).ToListAsync();
            return invitations;
        }
    }
}
