using Gratia.Domain.Entities;

namespace Gratia.Domain.Repositories
{
    public interface ICompanyRepository
    {
        Task<IEnumerable<Company>> GetAllAsync();
        Task<Company?> GetByIdAsync(Guid id);

        // Saved by IUnitOfWork.SaveChangesAsync
        void Add(Company company);
        void Remove(Company company);
    }
}
