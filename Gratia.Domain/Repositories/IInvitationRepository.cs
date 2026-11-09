using Gratia.Domain.Entities;

namespace Gratia.Domain.Repositories
{
    public interface IInvitationRepository
    {
        Task<CompanyInvitation?> GetByIdAsync(Guid id);
        Task<CompanyInvitation?> GetByTokenAsync(string token);
        Task<CompanyInvitation?> GetByEmailAndCompanyIdAsync(string email, Guid companyId);
        // The admins' list: invitations they sent that haven't been accepted, expired ones included so they can be
        // resent (#75), newest first. Only the latest per address, and no founding setup links (those aren't resent here).
        Task<IEnumerable<CompanyInvitation>> GetPendingInvitationsByCompanyIdAsync(Guid companyId);
        Task<bool> HasActiveInvitationsAsync(Guid companyId);

        // Saved by IUnitOfWork.SaveChangesAsync
        void Add(CompanyInvitation invitation);
        void Remove(CompanyInvitation invitation);
    }
}
