using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.Application.DTOs.Clinics.Member
{
    public class PermanentlyDeleteUser
    {
        [Required(ErrorMessage = "User ID is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "User ID must be greater than zero.")]
        public int Id { get; set; }

        [Required(ErrorMessage = "Clinic Id is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Clinic Id must be greater than zero.")]
        public int ClinicId { get; set; }
    }
}
