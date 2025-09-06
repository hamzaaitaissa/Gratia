﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Application.DTOs.Invitation
{
    public class AcceptInvitationDto
    {
        [Required]
        public string Token { get; set; }

        [Required]
        public string FullName { get; set; }

        [Required]
        [MinLength(6)]
        public string Password { get; set; }

        public string JobTitle { get; set; } = "";
    }
}
