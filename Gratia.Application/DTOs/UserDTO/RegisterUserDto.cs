using Gratia.Domain.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Application.DTOs.UserDTO
{
    public class RegisterUserDto
    {
        [Required]
        public string FullName { get; set; }
        [Required, EmailAddress]
        public string Email { get; set; }
        [Required, MinLength(8)]
        public string HashedPassword { get; set; }
        [Required]
        public string JobTitle { get; set; }
        [Required, AllowedValues("Employee", "Admin")]
        public UserRole Role { get; set; }
        [Required]
        public int NumberOfPointsAcquired { get; set; }
        [Required]
        public int NumberOfPointsAvailable { get; set; }
        [Required]
        public Guid CompanyId { get; set; }
    }
}
