using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;
using Gratia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Gratia.Infrastructure.Repositories
{
    public class DomainVerificationRepository(GratiaDbContext gratiaDbContext) : IDomainVerificationRepository
    {
        public void Add(DomainVerification verification) => gratiaDbContext.DomainVerifications.Add(verification);

        public Task<DomainVerification?> GetForMemberAsync(Guid companyId, Guid userId, CancellationToken cancellationToken = default) =>
            gratiaDbContext.DomainVerifications
                .FirstOrDefaultAsync(v => v.CompanyId == companyId && v.UserId == userId, cancellationToken);

        // Across companies (#78): signup starts one for someone who isn't a member, and has no caller. Only the given company's.
        public Task<DomainVerification?> GetClaimAsync(Guid companyId, string email, CancellationToken cancellationToken = default) =>
            gratiaDbContext.DomainVerifications
                .AcrossCompanies()
                .FirstOrDefaultAsync(v => v.CompanyId == companyId && v.UserId == null && v.Email == email, cancellationToken);

        // Across companies (#78): a claim's link is opened by someone without an account; the token from the email is the
        // proof. The company too, which the tenant filter would hide from an anonymous request.
        public Task<DomainVerification?> GetByLinkTokenHashAsync(string linkTokenHash, CancellationToken cancellationToken = default) =>
            gratiaDbContext.DomainVerifications
                .AcrossCompanies()
                .Include(v => v.Company)
                .FirstOrDefaultAsync(v => v.LinkTokenHash == linkTokenHash, cancellationToken);

        // Across companies (#78): a claim has no caller. Only the one row, by its id.
        public async Task<bool> TrySpendAsync(Guid id, string recordToken, DateTime now, CancellationToken cancellationToken = default)
        {
            var updated = await gratiaDbContext.DomainVerifications
                .AcrossCompanies()
                .Where(v => v.Id == id && v.RecordToken == recordToken && v.VerifiedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(v => v.VerifiedAt, (DateTime?)now), cancellationToken);
            return updated == 1;
        }

        public Task DeleteForMemberAsync(Guid companyId, Guid userId, CancellationToken cancellationToken = default) =>
            gratiaDbContext.DomainVerifications
                .Where(v => v.CompanyId == companyId && v.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);
    }
}
