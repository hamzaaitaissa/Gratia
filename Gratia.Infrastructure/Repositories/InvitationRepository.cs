using Gratia.Domain.Entities;
using Gratia.Domain.Enum;
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

        // Across companies (#78): signup asks it too, which has no caller. Admins ask it about their own company.
        public async Task<CompanyInvitation?> GetByEmailAndCompanyIdAsync(string email, Guid companyId)
        {
            var now = DateTime.UtcNow;
            return await _gratiaDbContext.CompanyInvitations.AcrossCompanies().FirstOrDefaultAsync(i => i.Email.ToLower() == email.ToLower()
                    && i.CompanyId == companyId && !i.IsUsed && i.ExpiresAt > now);
        }

        // The caller's company only, like the rest of an admin's reads (the tenant filter applies)
        public async Task<IReadOnlyList<CompanyInvitation>> GetActiveByEmailsAsync(Guid companyId, IReadOnlyCollection<string> emails,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            return await _gratiaDbContext.CompanyInvitations
                .AsNoTracking()
                .Where(i => i.CompanyId == companyId && emails.Contains(i.Email) && !i.IsUsed && i.ExpiresAt > now)
                .ToListAsync(cancellationToken);
        }

        public async Task<CompanyInvitation?> GetByTokenAsync(string token)
        {
            // The accept page shows the company and the inviter. Across companies (#78): the invitee isn't signed in,
            // and the token from the email is what proves the invitation is theirs.
            return await _gratiaDbContext.CompanyInvitations
                .AcrossCompanies()
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

        // The background sender (#55). Across companies (#78): it has no caller, and sends every company's emails.

        public async Task<IReadOnlyList<Guid>> GetDueEmailIdsAsync(DateTime now, int max, CancellationToken cancellationToken = default)
        {
            return await _gratiaDbContext.CompanyInvitations
                .AcrossCompanies()
                .Where(i => i.EmailStatus == InvitationEmailStatus.Pending && i.EmailNextAttemptAt <= now && !i.IsUsed)
                .OrderBy(i => i.EmailNextAttemptAt)
                .Select(i => i.Id)
                .Take(max)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> TryClaimEmailAsync(Guid id, DateTime now, DateTime leaseUntil, CancellationToken cancellationToken = default)
        {
            var claimed = await _gratiaDbContext.CompanyInvitations
                .AcrossCompanies()
                .Where(i => i.Id == id && i.EmailStatus == InvitationEmailStatus.Pending && i.EmailNextAttemptAt <= now)
                .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.EmailNextAttemptAt, leaseUntil), cancellationToken);
            return claimed == 1;
        }

        public async Task<CompanyInvitation?> GetForEmailAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _gratiaDbContext.CompanyInvitations
                .AcrossCompanies()
                .AsNoTracking()
                .Include(i => i.Company)
                .Include(i => i.InvitedByUser)
                .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        }

        public async Task<bool> MarkEmailSentAsync(Guid id, string sentToken, DateTime now, DateTime expiresAt,
            CancellationToken cancellationToken = default)
        {
            var updated = await StillWaitingFor(id, sentToken)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.EmailStatus, InvitationEmailStatus.Sent)
                    .SetProperty(i => i.EmailSentAt, now)
                    .SetProperty(i => i.EmailNextAttemptAt, (DateTime?)null)
                    .SetProperty(i => i.EmailAttempts, 0)
                    .SetProperty(i => i.InvitationToken, sentToken)
                    .SetProperty(i => i.PendingToken, (string?)null)
                    .SetProperty(i => i.ExpiresAt, expiresAt),
                    cancellationToken);
            return updated == 1;
        }

        public async Task RecordEmailFailureAsync(Guid id, string sentToken, DateTime? nextAttemptAt, CancellationToken cancellationToken = default)
        {
            var query = StillWaitingFor(id, sentToken);
            if (nextAttemptAt is not null)
                await query.ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.EmailAttempts, i => i.EmailAttempts + 1)
                    .SetProperty(i => i.EmailNextAttemptAt, nextAttemptAt),
                    cancellationToken);
            else
                await query.ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.EmailAttempts, i => i.EmailAttempts + 1)
                    .SetProperty(i => i.EmailStatus, InvitationEmailStatus.Failed)
                    .SetProperty(i => i.EmailNextAttemptAt, (DateTime?)null)
                    .SetProperty(i => i.PendingToken, (string?)null),
                    cancellationToken);
        }

        // Still waiting for the email this sender sent: a resend's pending link, or the invitation's own when there's none
        private IQueryable<CompanyInvitation> StillWaitingFor(Guid id, string sentToken) =>
            _gratiaDbContext.CompanyInvitations
                .AcrossCompanies()
                .Where(i => i.Id == id && i.EmailStatus == InvitationEmailStatus.Pending
                    && (i.PendingToken == sentToken || (i.PendingToken == null && i.InvitationToken == sentToken)));

        // Across companies (#78): signup asks it, which has no caller
        public async Task<bool> HasActiveInvitationsAsync(Guid companyId)
        {
            var now = DateTime.UtcNow;
            return await _gratiaDbContext.CompanyInvitations.AcrossCompanies().AnyAsync(i => i.CompanyId == companyId && !i.IsUsed && i.ExpiresAt > now);
        }
    }
}
