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
        // No InvitationToken: it only goes out in the email, so nobody else (an admin included) can accept on the invitee's behalf
        public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; }
        public UserRole InvitedRole { get; set; }
        public string? CompanyName { get; set; }
        public string? InvitedByUserName { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsExpired { get; set; }
        public bool IsValid { get; set; }
    }
}
