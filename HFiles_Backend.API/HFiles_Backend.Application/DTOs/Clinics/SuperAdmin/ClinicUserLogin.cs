using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.Application.DTOs.Clinics.SuperAdmin
{
    public class ClinicUserLogin
    {
        [Required(ErrorMessage = "HFID is required. Please provide your unique Health Facility ID.")]
        [MaxLength(20, ErrorMessage = "HFID must not exceed 20 characters.")]
        public string HFID { get; set; } = null!;

        [Required(ErrorMessage = "Role is required. Please specify whether you are a Super Admin, Admin, or Member.")]
        [MaxLength(20, ErrorMessage = "Role must not exceed 20 characters.")]
        public string Role { get; set; } = null!;

        [Required(ErrorMessage = "Password is required. Please enter your account password.")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters long.")]
        [MaxLength(100, ErrorMessage = "Password must not exceed 100 characters.")]
        public string Password { get; set; } = null!;

        [Required(ErrorMessage = "Clinic ID is required. Please provide a valid Clinic ID.")]
        [Range(1, int.MaxValue, ErrorMessage = "Clinic ID must be a positive number greater than zero.")]
        public int UserId { get; set; }

        [Required(ErrorMessage = "Email is required. Please enter your registered email address.")]
        [EmailAddress(ErrorMessage = "Invalid email format. Please enter a valid email address.")]
        [MaxLength(100, ErrorMessage = "Email must not exceed 100 characters.")]
        public string Email { get; set; } = null!;
    }
}
