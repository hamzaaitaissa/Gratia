namespace Gratia.Application.Interfaces;

public enum DnsLookupResult
{
    Found,
    // The domain has no such TXT record, or doesn't exist
    NotFound,
    // The DNS servers didn't answer, or answered with an error: try again later
    Failed
}

// Looks up a domain's DNS for domain verification (#61). Infrastructure's DnsDomainVerifier asks the DNS servers; the tests fake it.
public interface IDomainVerifier
{
    // Whether one of domain's TXT records is exactly value (ignoring case and surrounding spaces). Never throws for a DNS
    // failure, only for cancellation.
    Task<DnsLookupResult> HasTxtRecordAsync(string domain, string value, CancellationToken cancellationToken = default);
}
