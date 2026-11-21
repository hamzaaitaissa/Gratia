using Microsoft.EntityFrameworkCore;

namespace Gratia.Infrastructure.Data
{
    internal static class TenantFilterExtensions
    {
        // Takes one query out of the tenant filter (#78), for a read that must cross companies or runs before there is a
        // caller: login, refresh, signup, the invitation link, the global email check, the access token check and the
        // refill. Every use is a place to review: the query must limit itself to the right company, or not need to.
        public static IQueryable<T> AcrossCompanies<T>(this IQueryable<T> query) where T : class =>
            query.IgnoreQueryFilters([GratiaDbContext.TenantFilter]);
    }
}
