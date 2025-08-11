using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.Application.DTOs.Clinics.SuperAdmin
{
    public class CreateClinicSuperAdmin
    {
        [Required(ErrorMessage = "User ID is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "User ID must be greater than zero.")]
        public int UserId { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address format.")]
        [MaxLength(100)]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "HFID is required.")]
        [MaxLength(20)]
        public string HFID { get; set; } = null!;

        [Required(ErrorMessage = "Role is required.")]
        [MaxLength(20)]
        public string Role { get; set; } = null!;

        [Required(ErrorMessage = "Password is required.")]
        [MinLength(6)]
        [MaxLength(100)]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&#^()_+=<>]).{6,}$", ErrorMessage = "Password must include one uppercase letter, one number, and one special character.")]
        public string Password { get; set; } = null!;

        [Required(ErrorMessage = "Confirm Password is required.")]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = null!;
    }
}
