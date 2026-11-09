using Gratia.Domain.Entities;

namespace Gratia.Domain.Repositories
{
    public interface ICompanyRepository
    {
        Task<IEnumerable<Company>> GetAllAsync();
        Task<Company?> GetByIdAsync(Guid id);
        Task<Company?> GetByDomainAsync(string domain);

        // Saved by IUnitOfWork.SaveChangesAsync
        void Add(Company company);
        void Remove(Company company);
    }
}
