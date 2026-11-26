using Gratia.Domain.Common;

namespace Gratia.Domain.Entities
{
    // One person's proof that they manage their company's email domain (TASKS.md #61): a TXT record of their own to add
    // to the domain's DNS. Whoever can add it speaks for the company, so finding it verifies the workspace and makes them
    // an admin of it, even when someone else set the workspace up.
    //
    // Two things keep a record from working for anyone else, or later on:
    // - It belongs to one person. A member's is tied to their account (UserId), never found by email, because admins
    //   change members' emails; changing one also deletes it. Someone without an account starts from signup's "already on
    //   Gratia" email (a claim, UserId null), whose link proves the address. Only the link's SHA-256 is stored.
    // - It works once. Records tend to stay in the DNS, and an old one only shows that someone managed the domain then:
    //   with it, a demoted admin could make themselves one again.
    public class DomainVerification : BaseEntity
    {
        public const string RecordPrefix = "gratia-verify=";
        public static readonly TimeSpan LinkLifetime = TimeSpan.FromDays(7);

        public Guid CompanyId { get; set; }
        public Company Company { get; set; } = null!;

        // The member it belongs to. Null for a claim, which belongs to whoever gets the mail at Email.
        public Guid? UserId { get; set; }

        // Normalized. A claim's is who it's for; a member's is only their address when they started.
        public string Email { get; set; } = "";

        // Random. The same until it's spent, so a record already added keeps working when the person comes back later.
        public string RecordToken { get; set; } = "";

        // A claim's emailed link
        public string? LinkTokenHash { get; set; }
        public DateTime? LinkExpiresAt { get; set; }

        // When the record was found, which spends it. Set only by IDomainVerificationRepository.TrySpendAsync.
        public DateTime? VerifiedAt { get; set; }

        public string RecordValue => RecordPrefix + RecordToken;

        public bool IsSpent => VerifiedAt is not null;

        public DomainVerification() { }

        public DomainVerification(Guid companyId, Guid? userId, string email, string recordToken)
        {
            CompanyId = companyId;
            UserId = userId;
            Email = email;
            RecordToken = recordToken;
        }

        // Replaces a spent record
        public void Renew(string recordToken)
        {
            RecordToken = recordToken;
            VerifiedAt = null;
        }

        // A new emailed link replaces the last one
        public void StartLink(string linkTokenHash, DateTime now)
        {
            LinkTokenHash = linkTokenHash;
            LinkExpiresAt = now + LinkLifetime;
        }

        public bool HasLinkUntil(DateTime now) => LinkTokenHash is not null && LinkExpiresAt > now;
    }
}
