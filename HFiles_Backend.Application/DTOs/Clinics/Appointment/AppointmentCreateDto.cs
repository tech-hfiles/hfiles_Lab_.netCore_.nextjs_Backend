using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.Application.DTOs.Clinics.Appointment
{
    public class AppointmentCreateDto
    {
        [Required(ErrorMessage = "VisitorUsername is required.")]
        [StringLength(100, ErrorMessage = "VisitorUsername cannot exceed 100 characters.")]
        public string VisitorUsername { get; set; } = string.Empty;

        [Required(ErrorMessage = "VisitorPhoneNumber is required.")]
        [Phone(ErrorMessage = "VisitorPhoneNumber must be a valid phone number.")]
        [StringLength(15, ErrorMessage = "VisitorPhoneNumber cannot exceed 15 characters.")]
        public string VisitorPhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "AppointmentDate is required.")]
        [RegularExpression(@"^\d{2}-\d{2}-\d{4}$", ErrorMessage = "Date must be in dd-MM-yyyy format.")]
        public string AppointmentDate { get; set; } = string.Empty;

        [Required(ErrorMessage = "AppointmentTime is required.")]
        [RegularExpression(@"^\d{1,2}:\d{2}$", ErrorMessage = "Time must be in HH:mm format.")]
        public string AppointmentTime { get; set; } = string.Empty;

        [Required(ErrorMessage = "ClinicId is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "ClinicId must be a positive integer.")]
        public int ClinicId { get; set; }
    }
}
