using HFiles_Backend.Domain.Entities.Labs;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Clinics
{
    public class ClinicAppointment
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Visitor name is required.")]
        [MaxLength(100, ErrorMessage = "Visitor name must not exceed 100 characters.")]
        public string VisitorUsername { get; set; } = null!;

        [Required(ErrorMessage = "Visitor phone number is required.")]
        [Phone(ErrorMessage = "Invalid phone number format.")]
        [MaxLength(15, ErrorMessage = "Phone number must not exceed 15 digits.")]
        public string VisitorPhoneNumber { get; set; } = null!;

        [Required(ErrorMessage = "Appointment date is required.")]
        [DataType(DataType.Date)]
        public DateTime AppointmentDate { get; set; }

        [Required(ErrorMessage = "Appointment time is required.")]
        [DataType(DataType.Time)]
        public TimeSpan AppointmentTime { get; set; }

        [Required(ErrorMessage = "ClinicId is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "ClinicId must be a positive number.")]

        [MaxLength(1000, ErrorMessage = "Treatment notes must not exceed 1000 characters.")]
        public string? Treatment { get; set; }

        public int ClinicId { get; set; }

        [ForeignKey("ClinicId")]
        public ClinicSignup Clinics { get; set; } = null!;

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Scheduled";

        [MaxLength(255)]
        public string? GoogleCalendarEventId { get; set; }
    }
}