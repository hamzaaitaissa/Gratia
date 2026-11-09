using Gratia.Domain.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Application.DTOs.UserDTO
{
    public class UpdateUserDto
    {
        public Guid Id { get; set; }
        [Required]
        public string? FullName { get; set; }
        [Required, EmailAddress]
        public string? Email { get; set; }
        // No password (#17): an admin's edit used to force a new one. Changing it is the user's own job (#50).
        // Optional, as when accepting an invitation (#74): null or empty means none.
        public string? JobTitle { get; set; }
        // Sent by name ("Employee", "Admin"). AllowedValues("Employee", "Admin") compared the enum to strings, so it always failed (#5).
        [Required, EnumDataType(typeof(UserRole))]
        public UserRole Role { get; set; }
    }
}
