using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.Application.DTOs.Clinics.Member
{
    public class RevertClinicUser
    {
        [Required(ErrorMessage = "Member ID is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Member ID must be greater than zero.")]
        public int Id { get; set; }

        [Required(ErrorMessage = "Clinic ID is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Clinic ID must be greater than zero.")]
        public int ClinicId { get; set; }

        [Required(ErrorMessage = "Role is required.")]
        [MaxLength(20, ErrorMessage = "Role must not exceed 20 characters.")]
        public string? Role { get; set; }
    }
}
