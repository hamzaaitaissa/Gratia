using System.Collections.Concurrent;
using Gratia.Application.Interfaces;

namespace GratiaTests.Integration;

// The domains' TXT records, so a test can "add" a verification record the way a domain's managers would (#61)
public sealed class FakeDns : IDomainVerifier
{
    private readonly ConcurrentDictionary<string, ConcurrentBag<string>> _txtRecords = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, bool> _failing = new(StringComparer.OrdinalIgnoreCase);

    public void AddTxt(string domain, string value) => _txtRecords.GetOrAdd(domain, _ => []).Add(value);

    // Makes lookups of this domain fail, as when its DNS servers don't answer
    public void FailFor(string domain) => _failing[domain] = true;

    // Runs once, during the next lookup of this domain: something else happening while a check waits for DNS
    private readonly ConcurrentDictionary<string, Func<Task>> _during = new(StringComparer.OrdinalIgnoreCase);
    public void DuringNextLookup(string domain, Func<Task> action) => _during[domain] = action;

    public async Task<DnsLookupResult> HasTxtRecordAsync(string domain, string value, CancellationToken cancellationToken = default)
    {
        if (_during.TryRemove(domain, out var action))
            await action();
        if (_failing.ContainsKey(domain))
            return DnsLookupResult.Failed;
        var found = _txtRecords.TryGetValue(domain, out var values) && values.Any(v => v.Equals(value, StringComparison.OrdinalIgnoreCase));
        return found ? DnsLookupResult.Found : DnsLookupResult.NotFound;
    }
}
