using Gratia.Domain.Entities;
using Gratia.Domain.Enum;
using Gratia.Domain.Repositories;
using Gratia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Gratia.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly GratiaDbContext _gratiaDbContext;

        public UserRepository(GratiaDbContext gratiaDbContext)
        {
            _gratiaDbContext = gratiaDbContext;
        }

        public void Add(User user)
        {
            _gratiaDbContext.Users.Add(user);
        }

        public async Task<IEnumerable<User>> GetActiveInCompanyAsync(Guid companyId)
        {
            return await _gratiaDbContext.Users
                .AsNoTracking()
                .Where(u => u.CompanyId == companyId && u.DeactivatedAt == null)
                .OrderBy(u => u.FullName)
                .ToListAsync();
        }

        public async Task<IEnumerable<User>> GetDeactivatedInCompanyAsync(Guid companyId)
        {
            return await _gratiaDbContext.Users
                .AsNoTracking()
                .Where(u => u.CompanyId == companyId && u.DeactivatedAt != null)
                .OrderBy(u => u.FullName)
                .ToListAsync();
        }

        // Single-entity reads are tracked so services can change them and save through the unit of work

        // Across companies (#78): an email is unique across all of them, and login and signup have no caller yet
        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _gratiaDbContext.Users.AcrossCompanies().FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<IReadOnlyList<User>> GetInCompanyByEmailsAsync(Guid companyId, IReadOnlyCollection<string> emails,
            CancellationToken cancellationToken = default)
        {
            return await _gratiaDbContext.Users
                .AsNoTracking()
                .Where(u => u.CompanyId == companyId && emails.Contains(u.Email))
                .ToListAsync(cancellationToken);
        }

        // Across companies (#78): a domain claim (#61) has no caller. Only the given company's admins.
        public async Task<IReadOnlyList<User>> GetActiveAdminsAsync(Guid companyId, CancellationToken cancellationToken = default)
        {
            return await _gratiaDbContext.Users
                .AcrossCompanies()
                .AsNoTracking()
                .Where(u => u.CompanyId == companyId && u.Role == UserRole.Admin && u.DeactivatedAt == null)
                .ToListAsync(cancellationToken);
        }

        public async Task<User?> GetByIdAsync(Guid id)
        {
            return await _gratiaDbContext.Users.FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<User?> GetByIdInCompanyAsync(Guid id, Guid companyId)
        {
            return await _gratiaDbContext.Users.FirstOrDefaultAsync(u => u.Id == id && u.CompanyId == companyId && u.DeactivatedAt == null);
        }

        public async Task<User?> GetByIdInCompanyIncludingDeactivatedAsync(Guid id, Guid companyId)
        {
            return await _gratiaDbContext.Users.FirstOrDefaultAsync(u => u.Id == id && u.CompanyId == companyId);
        }

        // Across companies (#78): it runs while the access token is checked, before the request has a caller. The company
        // comes from the token.
        public async Task<bool> IsAccessTokenCurrentAsync(Guid id, Guid companyId, UserRole role, Guid sessionStamp,
            CancellationToken cancellationToken = default)
        {
            return await _gratiaDbContext.Users.AcrossCompanies().AnyAsync(
                u => u.Id == id && u.CompanyId == companyId && u.Role == role && u.SessionStamp == sessionStamp && u.DeactivatedAt == null,
                cancellationToken);
        }

        // Across companies (#78): resetting a forgotten password has no caller; the token from the email is the proof
        public async Task<User?> GetByPasswordResetTokenHashAsync(string tokenHash)
        {
            return await _gratiaDbContext.Users.AcrossCompanies().FirstOrDefaultAsync(u => u.PasswordResetTokenHash == tokenHash);
        }

        public async Task<bool> HasOtherActiveAdminAsync(Guid companyId, Guid exceptUserId, CancellationToken cancellationToken = default)
        {
            return await _gratiaDbContext.Users.AnyAsync(
                u => u.CompanyId == companyId && u.Role == UserRole.Admin && u.DeactivatedAt == null && u.Id != exceptUserId, cancellationToken);
        }

        // Across companies (#78): a domain claim (#61) has no caller. Only the one member, of the given company.
        public async Task<bool> TryMakeAdminAsync(Guid userId, Guid companyId, CancellationToken cancellationToken = default)
        {
            var updated = await _gratiaDbContext.Users
                .AcrossCompanies()
                .Where(u => u.Id == userId && u.CompanyId == companyId && u.DeactivatedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.Role, UserRole.Admin), cancellationToken);
            return updated == 1;
        }

        // Across companies (#78): signup asks it, which has no caller
        public async Task<bool> AnyInCompanyAsync(Guid companyId)
        {
            return await _gratiaDbContext.Users.AcrossCompanies().AnyAsync(u => u.CompanyId == companyId);
        }

        // Deactivated users too, so they have this month's allowance if they're reactivated. Across companies (#78): the
        // background refill has no caller.
        public async Task<int> ResetAvailablePointsAsync(Guid companyId, int points, CancellationToken cancellationToken = default)
        {
            return await _gratiaDbContext.Users
                .AcrossCompanies()
                .Where(u => u.CompanyId == companyId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.NumberOfPointsAvailable, points), cancellationToken);
        }

        public async Task<bool> TrySpendAvailablePointsAsync(Guid userId, int amount, CancellationToken cancellationToken = default)
        {
            // The balance check is in the WHERE clause, so it sees the committed balance, and the row lock makes a
            // concurrent send wait for this one and then check again
            var updated = await _gratiaDbContext.Users
                .Where(u => u.Id == userId && u.NumberOfPointsAvailable >= amount)
                .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.NumberOfPointsAvailable, u => u.NumberOfPointsAvailable - amount),
                    cancellationToken);
            return updated == 1;
        }

        public async Task AddAcquiredPointsAsync(Guid userId, int amount, CancellationToken cancellationToken = default)
        {
            await _gratiaDbContext.Users
                .Where(u => u.Id == userId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.NumberOfPointsAcquired, u => u.NumberOfPointsAcquired + amount),
                    cancellationToken);
        }
    }
}
