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
        public string FullName { get; set; }
        [Required, EmailAddress]
        public string Email { get; set; }
        [Required, MinLength(8)]
        public string HashedPassword { get; set; }
        [Required]
        public string JobTitle { get; set; }
        [Required, AllowedValues("Employee", "Admin")]
        public string Role { get; set; }
    }
}
