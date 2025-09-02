using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Application.DTOs.CompanyDTO
{
    public class RegisterCompanyDto
    {
        [Required(ErrorMessage = "Company name is required.")]
        [StringLength(100, ErrorMessage = "Company name cannot exceed 100 characters.")]
        public string Name { get; set; }
        //[Url(ErrorMessage = "LogoUrl must be a valid URL.")]
        [StringLength(200, ErrorMessage = "Logo URL cannot exceed 200 characters.")]
        public string LogoUrl { get; set; }
        [Required(ErrorMessage = "Primary color is required.")]
        [StringLength(7, ErrorMessage = "Primary color must be a valid hex code.")]
        public string PrimaryColor { get; set; }
        [StringLength(7, ErrorMessage = "Secondary color must be a valid hex code.")]
        public string SecondaryColor { get; set; }
    }
}
