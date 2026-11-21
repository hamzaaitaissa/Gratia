using Gratia.Domain.Common;
using Gratia.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Domain.Entities
{
    public class CompanyInvitation : BaseEntity
    {
        public string Email { get; set; }
        public string InvitationToken { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; } = false;
        public UserRole InvitedRole { get; set; } = UserRole.Employee;
        public Guid CompanyId { get; set; }
        // Null for a founding invitation: the one that creates a new company's first admin at signup
        public Guid? InvitedByUserId { get; set; }

        // What the admin knew about the person, from an import or the invite form (#54). The accept form starts with them.
        public const int MaxNameLength = 200;
        public string? FullName { get; set; }
        public string? JobTitle { get; set; }

        // The relationships are configured in GratiaDbContext (#37)
        public Company Company { get; set; }
        public User? InvitedByUser { get; set; }

        // The email, sent in the background (#55, SendInvitationEmailsCommand). Pending until it goes out; after
        // MaxEmailAttempts failures it's Failed, and Resend queues it again.
        public InvitationEmailStatus EmailStatus { get; set; } = InvitationEmailStatus.Sent;
        public int EmailAttempts { get; set; }
        // When the sender may next try. While one sender has it, a little later (its lease), so another one leaves it.
        public DateTime? EmailNextAttemptAt { get; set; }
        public DateTime? EmailSentAt { get; set; }
        // A resend's new link, waiting for its email. It replaces InvitationToken only once that email has gone out,
        // so the link the invitee already has keeps working until then (#20).
        public string? PendingToken { get; set; }

        // Queues the email, to go out now
        public void QueueEmail(DateTime now)
        {
            EmailStatus = InvitationEmailStatus.Pending;
            EmailAttempts = 0;
            EmailNextAttemptAt = now;
        }

        // Queues an email with a new link
        public void QueueResend(string newToken, DateTime now)
        {
            PendingToken = newToken;
            QueueEmail(now);
        }

        public CompanyInvitation() { }

        public CompanyInvitation(string email, string invitationToken, DateTime expiresAt,
            UserRole invitedRole, Guid companyId, Guid? invitedByUserId)
        {
            Email = email;
            InvitationToken = invitationToken;
            ExpiresAt = expiresAt;
            InvitedRole = invitedRole;
            CompanyId = companyId;
            InvitedByUserId = invitedByUserId;
        }

        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
        public bool IsValid => !IsUsed && !IsExpired;
    }
}
