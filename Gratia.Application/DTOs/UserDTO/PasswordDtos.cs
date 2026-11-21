using System.ComponentModel.DataAnnotations;

namespace Gratia.Application.DTOs.UserDTO
{
    // The signed-in user's own password (#50). The new one has the same minimum as LoginDto, or it could never sign in.
    public class ChangePasswordDto
    {
        [Required]
        public string? CurrentPassword { get; set; }

        [Required, MinLength(8)]
        public string? NewPassword { get; set; }
    }

    // Asking for a forgotten-password link (#50)
    public class ForgotPasswordDto
    {
        [Required, EmailAddress, StringLength(254)]
        public string? Email { get; set; }
    }

    // Choosing a new password with the token from that link (#50)
    public class ResetPasswordDto
    {
        [Required]
        public string? Token { get; set; }

        [Required, MinLength(8)]
        public string? NewPassword { get; set; }
    }
}
