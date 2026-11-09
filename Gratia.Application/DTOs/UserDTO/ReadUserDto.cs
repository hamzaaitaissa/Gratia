using Gratia.Domain.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Application.DTOs.UserDTO
{
    public class ReadUserDto
    {
        [Required]
        public Guid Id { get; set; }
        [Required]
        public string? FullName { get; set; }
        [Required, EmailAddress]
        public string? Email { get; set; }
        [Required]
        public string? JobTitle { get; set; }
        [Required]
        public UserRole Role { get; set; }
        [Required]
        public int NumberOfPointsAcquired { get; set; }
        [Required]
        public int NumberOfPointsAvailable { get; set; }
        [Required]
        public Guid CompanyId { get; set; }
        // Null for everyone in the directory; set only in the admins' list of deactivated users (#16)
        public DateTime? DeactivatedAt { get; set; }
    }
}
