using Gratia.Domain.Entities;

namespace Gratia.Domain.Repositories
{
    public interface IUserRepository
    {
        Task<IEnumerable<User>> GetAllAsync();
        Task<User?> GetByIdAsync(Guid id);
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetUserByRefreshTokenAsync(string refreshToken);
        Task<bool> AnyInCompanyAsync(Guid companyId);

        // Saved by IUnitOfWork.SaveChangesAsync
        void Add(User user);
        void Remove(User user);

        // Runs immediately as a single UPDATE, outside the unit of work. Returns the number of users updated.
        Task<int> ResetAvailablePointsAsync(int points);
    }
}
