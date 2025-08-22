using HFiles_Backend.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Clinics
{
    public class ClinicVisit
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "ClinicPatientId is required.")]
        public int ClinicPatientId { get; set; }

        [ForeignKey("ClinicPatientId")]
        public ClinicPatient Patient { get; set; } = null!;

        [Required]
        public int ClinicId { get; set; }

        [ForeignKey("ClinicId")]
        public ClinicSignup Clinic { get; set; } = null!;

        [Required(ErrorMessage = "Appointment date is required.")]
        [DataType(DataType.Date)]
        public DateTime AppointmentDate { get; set; }

        [Required(ErrorMessage = "Appointment time is required.")]
        [DataType(DataType.Time)]
        public TimeSpan AppointmentTime { get; set; }

        public PaymentMethod? PaymentMethod { get; set; }

        public ICollection<ClinicVisitConsentForm> ConsentFormsSent { get; set; } = new List<ClinicVisitConsentForm>();
    }
}
