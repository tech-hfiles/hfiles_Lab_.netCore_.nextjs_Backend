using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.Application.DTOs.Clinics.HFID
{
    public class ClinicHFIDDto
    {
        [Required(ErrorMessage = "Email is required. Please enter a valid clinic email address.")]
        [EmailAddress(ErrorMessage = "Invalid email format. Please enter a valid email.")]
        [MaxLength(100, ErrorMessage = "Email must not exceed 100 characters.")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Clinic name is required.")]
        [MaxLength(100, ErrorMessage = "Clinic name must not exceed 100 characters.")]
        public string ClinicName { get; set; } = null!;

        [Required(ErrorMessage = "HFID is required.")]
        [MaxLength(20, ErrorMessage = "HFID must not exceed 20 characters.")]
        public string HFID { get; set; } = null!;

        public string? ProfilePhoto { get; set; } 
    }
}
