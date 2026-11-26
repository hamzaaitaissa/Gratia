using System.Security.Cryptography;
using Gratia.Application.DTOs.CompanyDTO;
using Gratia.Application.Interfaces;
using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Gratia.Application.Features.Companies;

// Domain verification (#61), shared by the signed-in check, the claim link and signup. Whoever can add a record to the
// company's domain manages the domain, so they speak for the company: finding their own record verifies the workspace
// (no more domain rule or daily cap) and makes them an admin, verified or not, so IT always has a way in. A record works
// once (DomainVerification): doing it again takes a new one, and so control of the domain now.
internal static class DomainOwnership
{
    // 128 random bits as lowercase hex: easy to copy into a DNS provider's form
    public static string NewRecordToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    public static DnsRecordDto Record(string domain, DomainVerification verification) =>
        new() { Name = domain, Value = verification.RecordValue };

    public static string NoDomainMessage =>
        "This workspace was set up with a personal email address, so it has no domain to verify.";

    public static string StartFirstMessage(string domain) =>
        $"Get your record first, add it to {domain}'s DNS settings, then check again.";

    public const string CanRemoveRecord = "You can remove the record from the DNS now.";

    // Null when the record was found
    public static DomainCheckResultDto? NotVerified(DnsLookupResult lookup, string domain) => lookup switch
    {
        DnsLookupResult.Found => null,
        DnsLookupResult.NotFound => new DomainCheckResultDto
        {
            Status = DomainCheckStatus.NotFound,
            Message = $"The record isn't on {domain} yet. DNS changes can take from a few minutes to a few hours to show up, so check again later."
        },
        _ => new DomainCheckResultDto
        {
            Status = DomainCheckStatus.LookupFailed,
            Message = $"{domain}'s DNS servers didn't answer. Check again in a few minutes."
        }
    };

    // After the commit: tells the company's other admins, so a takeover never goes unnoticed. A failed email is logged,
    // not an error: the verification is saved by now.
    public static async Task NotifyAdminsAsync(IUserRepository userRepository, IEmailService emailService, ILogger logger,
        Company company, string verifiedByEmail, DomainVerifiedBy by, CancellationToken cancellationToken)
    {
        foreach (var admin in await userRepository.GetActiveAdminsAsync(company.Id, cancellationToken))
        {
            if (admin.Email == verifiedByEmail)
                continue;
            try
            {
                await emailService.SendDomainVerifiedEmailAsync(admin.Email, company.Name, company.Domain!, verifiedByEmail, by, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Couldn't tell admin {UserId} that company {CompanyId}'s domain was verified", admin.Id, company.Id);
            }
        }
    }
}
