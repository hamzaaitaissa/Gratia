using Gratia.Domain.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Application.DTOs.Invitation
{
    public class InviteUserDto
    {
        [Required]
        [EmailAddress]
        public string? Email { get; set; }
        [EnumDataType(typeof(UserRole))]
        public UserRole Role { get; set; } = UserRole.Employee;

        // Optional: the accept form starts with them (#54)
        [StringLength(200)]
        public string? FullName { get; set; }
        [StringLength(200)]
        public string? JobTitle { get; set; }
    }
}
