using System.ComponentModel.DataAnnotations;

namespace Gratia.Application.DTOs.CompanyDTO
{
    // Kept to three fields; branding is set later by the admin
    public class CompanySignupDto
    {
        [Required, StringLength(100)]
        public string CompanyName { get; set; } = string.Empty;

        // The founder's name, used to greet them in the setup email
        [Required, StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        // A work email: its domain becomes the company's domain
        [Required, EmailAddress, StringLength(254)]
        public string Email { get; set; } = string.Empty;
    }
}
