using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Gratia.Domain.Entities;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Application.DTOs.CompanyDTO
{
    public class UpdateCompanyDto
    {
        private const string HexColor = "^#[0-9A-Fa-f]{6}$";

        [Required, Key]
        public Guid Id { get; set; }
        [Required(ErrorMessage = "Company name is required.")]
        [StringLength(100, ErrorMessage = "Company name cannot exceed 100 characters.")]
        public string? Name { get; set; }
        // Optional: companies start without a logo, and null or empty removes it (#67). Only http(s), so no javascript: links.
        // The pattern is published in the OpenAPI document, so it sticks to syntax JavaScript regexes understand too.
        [RegularExpression(@"^https?://\S+$", ErrorMessage = "LogoUrl must be an http(s) URL.")]
        [StringLength(200, ErrorMessage = "Logo URL cannot exceed 200 characters.")]
        public string? LogoUrl { get; set; }
        [Required(ErrorMessage = "Primary color is required.")]
        [RegularExpression(HexColor, ErrorMessage = "Primary color must be a hex code like #4F46E5.")]
        public string? PrimaryColor { get; set; }
        [Required(ErrorMessage = "Secondary color is required.")]
        [RegularExpression(HexColor, ErrorMessage = "Secondary color must be a hex code like #F59E0B.")]
        public string? SecondaryColor { get; set; }

        // The points cycle (#19). Left out (null), each keeps its current value, so a branding-only form needn't send
        // them. A new allowance applies from the next refill. A new day doesn't refill anyone: the current period lasts
        // until the new day comes round.
        [Range(1, Company.MaxMonthlyPointsAllowance, ErrorMessage = "The monthly allowance must be between 1 and 1000 points.")]
        public int? MonthlyPointsAllowance { get; set; }
        [Range(1, Company.MaxPointsResetDay, ErrorMessage = "The reset day must be between 1 and 28, so it exists in every month.")]
        public int? PointsResetDay { get; set; }
    }
}
