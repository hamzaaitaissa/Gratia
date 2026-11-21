using Gratia.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Application.DTOs.Invitation
{
    public class InvitationDto
    {
        public Guid Id { get; set; }
        public string? Email { get; set; }
        // What the admin entered or imported, if anything (#54): the accept form starts with them
        public string? FullName { get; set; }
        public string? JobTitle { get; set; }
        // No InvitationToken: it only goes out in the email, so nobody else (an admin included) can accept on the invitee's behalf
        public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; }
        public UserRole InvitedRole { get; set; }
        public string? CompanyName { get; set; }
        public string? InvitedByUserName { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsExpired { get; set; }
        public bool IsValid { get; set; }

        // The email (#55): Pending while it waits to go out (or to be tried again), Sent, or Failed after every try,
        // until it's resent. EmailSentAt is null for invitations sent before the queue existed.
        public InvitationEmailStatus EmailStatus { get; set; }
        public DateTime? EmailSentAt { get; set; }
    }
}
