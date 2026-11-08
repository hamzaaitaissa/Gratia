using Gratia.Domain.Entities;

namespace Gratia.Domain.Repositories
{
    public interface IInvitationRepository
    {
        Task<CompanyInvitation?> GetByIdAsync(Guid id);
        Task<CompanyInvitation?> GetByTokenAsync(string token);
        Task<CompanyInvitation?> GetByEmailAndCompanyIdAsync(string email, Guid companyId);
        Task<IEnumerable<CompanyInvitation>> GetPendingInvitationsByCompanyIdAsync(Guid companyId);

        // Saved by IUnitOfWork.SaveChangesAsync
        void Add(CompanyInvitation invitation);
        void Remove(CompanyInvitation invitation);
    }
}
