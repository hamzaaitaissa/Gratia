using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Application.DTOs.UserDTO
{
    public class LoginDto
    {
        // 254 is the longest deliverable address, so no invited account has a longer one; a longer one wouldn't fit
        // LoginAttempts (#72)
        [Required, EmailAddress, StringLength(254)]
        public string? Email { get; set; }
        [Required, MinLength(8)]
        public string? HashedPassword { get; set; }
    }
}
