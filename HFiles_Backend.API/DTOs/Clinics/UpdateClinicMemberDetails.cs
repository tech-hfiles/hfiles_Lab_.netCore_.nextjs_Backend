using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.API.DTOs.Clinics
{
    public class UpdateClinicMemberDetails
    {
        [Required(ErrorMessage = "Member ID is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Member ID must be greater than zero.")]
        public int MemberId { get; set; }

        [MaxLength(50, ErrorMessage = "First name must not exceed 50 characters.")]
        public string? FirstName { get; set; }

        [MaxLength(50, ErrorMessage = "Last name must not exceed 50 characters.")]
        public string? LastName { get; set; }

        [EmailAddress(ErrorMessage = "Invalid email address format.")]
        [MaxLength(100, ErrorMessage = "Email must not exceed 100 characters.")]
        public string? Email { get; set; }

        [MaxLength(20, ErrorMessage = "HFID must not exceed 20 characters.")]
        public string? HFID { get; set; }

        [MaxLength(100, ErrorMessage = "Coach name must not exceed 100 characters.")]
        public string? Coach { get; set; }

        [MaxLength(50, ErrorMessage = "Color must not exceed 50 characters.")]
        [RegularExpression(@"^rgba?\(\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*\d{1,3}\s*(,\s*(0|1|0?\.\d+))?\s*\)$",
            ErrorMessage = "Color must be in valid RGB or RGBA format (e.g., 'rgba(255, 0, 0, 1)').")]
        public string? Color { get; set; }

        [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
        [MaxLength(100, ErrorMessage = "Password must not exceed 100 characters.")]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&#^()_+=<>]).+$",
            ErrorMessage = "Password must include at least one uppercase letter, one number, and one special character.")]
        public string? Password { get; set; }

        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string? ConfirmPassword { get; set; }
    }
}
