using Gratia.Domain.Entities;

namespace Gratia.Domain.Repositories
{
    public interface IRefreshTokenRepository
    {
        // With its User, whose current role and company go into the new access token
        Task<RefreshToken?> GetByHashAsync(string tokenHash);

        // Saved by IUnitOfWork.SaveChangesAsync
        void Add(RefreshToken refreshToken);

        // The rest run immediately as single DELETEs, outside the unit of work.

        // Deletes the token only if it still exists and hasn't expired. Of two refreshes with the same token, only the
        // one that deletes it may issue new tokens, so a token can't be used twice.
        Task<bool> TryConsumeAsync(Guid id, DateTime now, CancellationToken cancellationToken = default);

        // Signs out one device, or every device of a user. Return the number of tokens deleted.
        Task<int> DeleteByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
        Task<int> DeleteForUserAsync(Guid userId, CancellationToken cancellationToken = default);

        // Housekeeping at sign-in
        Task<int> DeleteExpiredForUserAsync(Guid userId, DateTime now, CancellationToken cancellationToken = default);
    }
}
