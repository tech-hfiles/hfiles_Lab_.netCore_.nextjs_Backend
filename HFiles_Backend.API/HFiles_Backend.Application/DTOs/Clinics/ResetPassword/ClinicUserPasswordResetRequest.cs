using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.Application.DTOs.Clinics.ResetPassword
{
    public class ClinicUserPasswordResetRequest
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        [MaxLength(100, ErrorMessage = "Email must not exceed 100 characters.")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "ClinicId is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "ClinicId must be greater than zero.")]
        public int ClinicId { get; set; }
    }
}
