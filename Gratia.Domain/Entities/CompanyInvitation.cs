using Gratia.Domain.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Domain.Entities
{
    public class CompanyInvitation
    {
        public string Email { get; set; }
        public string InvitationToken { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; } = false;
        public UserRole InvitedRole { get; set; } = UserRole.Employee;
        public Guid CompanyId { get; set; }
        public Guid InvitedByUserId { get; set; }

        [ForeignKey(nameof(CompanyId))]
        public Company Company { get; set; }

        [ForeignKey(nameof(InvitedByUserId))]
        public User InvitedByUser { get; set; }

        public CompanyInvitation() { }

        public CompanyInvitation(string email, string invitationToken, DateTime expiresAt,
            UserRole invitedRole, Guid companyId, Guid invitedByUserId)
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
