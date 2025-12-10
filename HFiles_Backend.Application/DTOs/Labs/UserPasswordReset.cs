using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.Application.DTOs.Labs
{
    public class UserPasswordReset
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        [MaxLength(100, ErrorMessage = "Email must not exceed 100 characters.")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "LabId is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "LabId must be greater than zero.")]
        public int LabId { get; set; }

        [Required(ErrorMessage = "New password is required.")]
        [MinLength(6, ErrorMessage = "New Password must be at least 6 characters.")]
        [MaxLength(100, ErrorMessage = "New Password must not exceed 100 characters.")]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&#^()_+=<>]).{6,}$",
            ErrorMessage = "New Password must include at least one uppercase letter, one number, and one special character.")]
        public string NewPassword { get; set; } = null!;

        [Required(ErrorMessage = "Confirm password is required.")]
        [Compare("NewPassword", ErrorMessage = "Confirm password does not match.")]
        public string ConfirmPassword { get; set; } = null!;
    }

}
