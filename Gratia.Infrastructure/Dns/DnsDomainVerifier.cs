using DnsClient;
using Gratia.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Gratia.Infrastructure.Dns
{
    // Domain verification's DNS lookups (#61), with DnsClient (Apache-2.0). A TXT record at the domain itself, as most
    // services ask for, so it works with every DNS provider.
    public sealed class DnsDomainVerifier(IDnsQuery dns, ILogger<DnsDomainVerifier> logger) : IDomainVerifier
    {
        public async Task<DnsLookupResult> HasTxtRecordAsync(string domain, string value, CancellationToken cancellationToken = default)
        {
            // The domain comes from a signup's email address: only a real host name is asked about
            if (Uri.CheckHostName(domain) != UriHostNameType.Dns || !domain.Contains('.'))
                return DnsLookupResult.NotFound;

            IDnsQueryResponse response;
            try
            {
                response = await dns.QueryAsync(domain, QueryType.TXT, QueryClass.IN, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // A timeout, an unreachable server, a malformed answer
                logger.LogWarning(ex, "The TXT lookup of {Domain} failed", domain);
                return DnsLookupResult.Failed;
            }

            if (response.HasError)
            {
                if (response.Header.ResponseCode == DnsHeaderResponseCode.NotExistentDomain)
                    return DnsLookupResult.NotFound;
                logger.LogWarning("The TXT lookup of {Domain} failed: {Error}", domain, response.ErrorMessage);
                return DnsLookupResult.Failed;
            }

            // A long TXT value can be split into several strings, which make one value together
            var found = response.Answers.TxtRecords()
                .Any(record => string.Concat(record.Text).Trim().Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));
            return found ? DnsLookupResult.Found : DnsLookupResult.NotFound;
        }
    }
}
