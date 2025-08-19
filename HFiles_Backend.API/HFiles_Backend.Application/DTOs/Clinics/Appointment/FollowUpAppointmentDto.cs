using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.Application.DTOs.Clinics.Appointment
{
    public class FollowUpAppointmentDto
    {
        [Required(ErrorMessage = "HFID is required.")]
        [MaxLength(20, ErrorMessage = "HFID cannot exceed 20 characters.")]
        public string HFID { get; set; } = null!;

        [Required(ErrorMessage = "Consent form names are required.")]
        [MinLength(1, ErrorMessage = "At least one consent form must be selected.")]
        public List<string> ConsentFormTitles { get; set; } = new();

        [Required(ErrorMessage = "Appointment date is required.")]
        [RegularExpression(@"^\d{2}-\d{2}-\d{4}$", ErrorMessage = "Date must be in dd-MM-yyyy format.")]
        public string AppointmentDate { get; set; } = null!;

        [Required(ErrorMessage = "Appointment time is required.")]
        [RegularExpression(@"^\d{2}:\d{2}$", ErrorMessage = "Time must be in HH:mm format.")]
        public string AppointmentTime { get; set; } = null!;
    }
}
