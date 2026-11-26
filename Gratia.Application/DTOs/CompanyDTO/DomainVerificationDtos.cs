using System.ComponentModel.DataAnnotations;

namespace Gratia.Application.DTOs.CompanyDTO
{
    // The DNS record a person adds to prove they manage the domain (#61)
    public sealed class DnsRecordDto
    {
        public string Type { get; set; } = "TXT";
        // The domain itself: at most DNS providers, the "@" or root entry
        public string Name { get; set; } = "";
        public string Value { get; set; } = "";
    }

    // The signed-in member's view of their workspace's domain verification
    public sealed class DomainVerificationDto
    {
        // Null when the workspace isn't tied to a domain (it was founded with a personal address): nothing to verify
        public string? Domain { get; set; }
        public bool IsVerified { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public bool IsAdmin { get; set; }
        // The caller's own record, once they've started (POST /api/company/domain-verification). Null before, and once
        // it has been found: it works once.
        public DnsRecordDto? Record { get; set; }
    }

    public enum DomainCheckStatus
    {
        // The record was found: the domain is verified, and the person is an admin (or invited as one)
        Verified,
        // Not there yet. DNS changes can take a while to show up.
        NotFound,
        // The domain's DNS servers didn't answer: check again later
        LookupFailed
    }

    public sealed class DomainCheckResultDto
    {
        public DomainCheckStatus Status { get; set; }
        public string Message { get; set; } = "";
        // A claim by someone without an account: the token of their admin invitation, for the accept page
        // (/accept-invitation/{token}). It's theirs: they opened the claim link from their email.
        public string? InvitationToken { get; set; }
    }

    // What a claim link (from signup's "already on Gratia" email) shows, before anyone is signed in
    public sealed class DomainClaimDto
    {
        public string CompanyName { get; set; } = "";
        public string Domain { get; set; } = "";
        public string Email { get; set; } = "";
        public bool IsVerified { get; set; }
        // The claim was finished with this record, which works once. Checking again only gives back its unused invitation.
        public bool IsUsed { get; set; }
        public DnsRecordDto Record { get; set; } = new();
        public DateTime ExpiresAt { get; set; }
    }

    public sealed class CheckDomainClaimDto
    {
        [Required]
        public string? Token { get; set; }
    }
}
