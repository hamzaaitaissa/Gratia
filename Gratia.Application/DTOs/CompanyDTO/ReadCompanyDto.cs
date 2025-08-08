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
        public string Name { get; set; }
        [Required]
        public string LogoUrl { get; set; }
        [Required]
        public string PrimaryColor { get; set; }
        public string SecondaryColor { get; set; }
    }
}
