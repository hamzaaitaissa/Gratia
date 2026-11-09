using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Application.DTOs.CompanyDTO
{
    public class ReadCompanyDto
    {
        [Required, Key]
        public Guid Id { get; set; }
        [Required, MinLength(2)]
        public string? Name { get; set; }
        [Required]
        public string? LogoUrl { get; set; }
        [Required]
        public string? PrimaryColor { get; set; }
        public string? SecondaryColor { get; set; }
        public string? Domain { get; set; }
        // False while the workspace is provisional: invitations are limited to Domain
        public bool IsDomainVerified { get; set; }

        // The points cycle (#19, #53): points to give are refilled to the allowance on the reset day at 00:00 UTC.
        // LastPointsResetAt and NextPointsResetAt are that schedule's dates (UTC) around the time of the request.
        public int MonthlyPointsAllowance { get; set; }
        public int PointsResetDay { get; set; }
        public DateTime LastPointsResetAt { get; set; }
        public DateTime NextPointsResetAt { get; set; }
    }
}
