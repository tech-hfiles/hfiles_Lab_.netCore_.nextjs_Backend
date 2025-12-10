using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.Application.DTOs.Labs
{
    public class PasswordResetRequest
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        [MaxLength(100, ErrorMessage = "Email must not exceed 100 characters.")]
        public string Email { get; set; } = null!;
    }

}
