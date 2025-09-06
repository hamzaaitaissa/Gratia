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
        public string Email { get; set; }
        public UserRole Role { get; set; } = UserRole.Employee;
    }
}
