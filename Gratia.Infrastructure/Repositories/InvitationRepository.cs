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

        // Queries compare ExpiresAt directly: IsExpired is a computed C# property, which EF can't translate to SQL

        public async Task<CompanyInvitation?> GetByEmailAndCompanyIdAsync(string email, Guid companyId)
        {
            var now = DateTime.UtcNow;
            return await _gratiaDbContext.CompanyInvitations.FirstOrDefaultAsync(i => i.Email.ToLower() == email.ToLower()
                    && i.CompanyId == companyId && !i.IsUsed && i.ExpiresAt > now);
        }

        public async Task<CompanyInvitation?> GetByTokenAsync(string token)
        {
            // The accept page shows the company and the inviter
            return await _gratiaDbContext.CompanyInvitations
                .Include(i => i.Company)
                .Include(i => i.InvitedByUser)
                .FirstOrDefaultAsync(i => i.InvitationToken == token);
        }

        public async Task<IEnumerable<CompanyInvitation>> GetPendingInvitationsByCompanyIdAsync(Guid companyId)
        {
            return await _gratiaDbContext.CompanyInvitations
                .AsNoTracking()
                .Where(i => i.CompanyId == companyId && !i.IsUsed && i.InvitedByUserId != null)
                // Only the latest invitation to each address: an expired one that was followed by a new one is history
                .Where(i => !_gratiaDbContext.CompanyInvitations.Any(newer =>
                    newer.CompanyId == i.CompanyId && newer.Email == i.Email && newer.CreatedDate > i.CreatedDate))
                .Include(i => i.Company)
                .Include(i => i.InvitedByUser)
                .OrderByDescending(i => i.CreatedDate)
                .ToListAsync();
        }

        public async Task<bool> HasActiveInvitationsAsync(Guid companyId)
        {
            var now = DateTime.UtcNow;
            return await _gratiaDbContext.CompanyInvitations.AnyAsync(i => i.CompanyId == companyId && !i.IsUsed && i.ExpiresAt > now);
        }
    }
}
