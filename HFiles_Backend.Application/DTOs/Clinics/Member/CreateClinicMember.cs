using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.Application.DTOs.Clinics.Member
{
    public class CreateClinicMember
    {
        [Required(ErrorMessage = "HFID is required.")]
        [MaxLength(20, ErrorMessage = "HFID must not exceed 20 characters.")]
        public string HFID { get; set; } = null!;

        [Required(ErrorMessage = "BranchId is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "BranchId must be greater than zero.")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
        [MaxLength(100, ErrorMessage = "Password must not exceed 100 characters.")]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&#^()_+=<>]).+$",
            ErrorMessage = "Password must include at least one uppercase letter, one number, and one special character.")]
        public string Password { get; set; } = null!;

        [Required(ErrorMessage = "Confirm Password is required.")]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = null!;
    }
}
