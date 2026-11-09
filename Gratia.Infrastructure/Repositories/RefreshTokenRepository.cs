using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;
using Gratia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Gratia.Infrastructure.Repositories
{
    public class RefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly GratiaDbContext _gratiaDbContext;

        public RefreshTokenRepository(GratiaDbContext gratiaDbContext)
        {
            _gratiaDbContext = gratiaDbContext;
        }

        public async Task<RefreshToken?> GetByHashAsync(string tokenHash)
        {
            return await _gratiaDbContext.RefreshTokens
                .AsNoTracking()
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
        }

        public void Add(RefreshToken refreshToken)
        {
            _gratiaDbContext.RefreshTokens.Add(refreshToken);
        }

        public async Task<bool> TryConsumeAsync(Guid id, DateTime now, CancellationToken cancellationToken = default)
        {
            // The row lock makes a concurrent refresh with the same token wait, then find nothing left to delete
            var deleted = await _gratiaDbContext.RefreshTokens
                .Where(t => t.Id == id && t.ExpiresAt > now)
                .ExecuteDeleteAsync(cancellationToken);
            return deleted == 1;
        }

        public async Task<int> DeleteByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        {
            return await _gratiaDbContext.RefreshTokens.Where(t => t.TokenHash == tokenHash).ExecuteDeleteAsync(cancellationToken);
        }

        public async Task<int> DeleteForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _gratiaDbContext.RefreshTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        }

        public async Task<int> DeleteExpiredForUserAsync(Guid userId, DateTime now, CancellationToken cancellationToken = default)
        {
            return await _gratiaDbContext.RefreshTokens
                .Where(t => t.UserId == userId && t.ExpiresAt <= now)
                .ExecuteDeleteAsync(cancellationToken);
        }
    }
}
