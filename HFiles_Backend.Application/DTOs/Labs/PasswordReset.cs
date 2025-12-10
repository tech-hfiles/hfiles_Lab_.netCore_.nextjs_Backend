using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.Application.DTOs.Labs
{
    public class PasswordReset
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        [MaxLength(100, ErrorMessage = "Email must not exceed 100 characters.")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "New password is required.")]
        [MaxLength(100, ErrorMessage = "Password must not exceed 100 characters.")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&#^()_+=<>]).{6,}$",
            ErrorMessage = "Password must include at least one uppercase letter, one number, and one special character.")]
        public string NewPassword { get; set; } = null!;

        [Required(ErrorMessage = "Confirm password is required.")]
        [Compare("NewPassword", ErrorMessage = "Confirm password does not match.")]
        public string ConfirmPassword { get; set; } = null!;
    }
}
