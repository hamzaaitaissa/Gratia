using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;
using Gratia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Gratia.Infrastructure.Repositories
{
    public class LoginAttemptRepository : ILoginAttemptRepository
    {
        private readonly GratiaDbContext _gratiaDbContext;

        public LoginAttemptRepository(GratiaDbContext gratiaDbContext)
        {
            _gratiaDbContext = gratiaDbContext;
        }

        public void Add(LoginAttempt attempt)
        {
            _gratiaDbContext.LoginAttempts.Add(attempt);
        }

        public async Task<IReadOnlyList<DateTime>> GetTimesSinceAsync(string email, DateTime since, Guid exceptId,
            CancellationToken cancellationToken = default)
        {
            return await _gratiaDbContext.LoginAttempts
                .Where(a => a.Email == email && a.CreatedDate > since && a.Id != exceptId)
                .OrderBy(a => a.CreatedDate)
                .Select(a => a.CreatedDate)
                .ToListAsync(cancellationToken);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            await _gratiaDbContext.LoginAttempts.Where(a => a.Id == id).ExecuteDeleteAsync(cancellationToken);
        }

        public async Task DeleteForEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            await _gratiaDbContext.LoginAttempts.Where(a => a.Email == email).ExecuteDeleteAsync(cancellationToken);
        }

        public async Task DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken = default)
        {
            await _gratiaDbContext.LoginAttempts.Where(a => a.CreatedDate <= cutoff).ExecuteDeleteAsync(cancellationToken);
        }
    }
}
