using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.Application.DTOs.Clinics.Profile
{
    public class ClinicProfileUpdate
    {
        [Range(1, int.MaxValue, ErrorMessage = "Clinic ID must be greater than zero.")]
        public int ClinicId { get; set; }

        [MaxLength(250, ErrorMessage = "Address must not exceed 250 characters.")]
        public string? Address { get; set; }
    }
}
