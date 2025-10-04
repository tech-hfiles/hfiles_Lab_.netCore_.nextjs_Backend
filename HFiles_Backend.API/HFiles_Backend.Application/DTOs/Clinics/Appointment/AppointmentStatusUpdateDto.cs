using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.Application.DTOs.Clinics.Appointment
{
    public class AppointmentStatusUpdateDto : IValidatableObject
    {
        [RegularExpression("^(Scheduled|Canceled|Completed)$", ErrorMessage = "Status must be either 'Scheduled', 'Canceled' or 'Completed'.")]
        public string? Status { get; set; }

        [MaxLength(1000, ErrorMessage = "Treatment notes must not exceed 1000 characters.")]
        public string? Treatment { get; set; }

        [StringLength(50, ErrorMessage = "HFID must not exceed 50 characters.")]
        public string? HFID { get; set; }
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Status == "Completed" && string.IsNullOrWhiteSpace(Treatment))
            {
                yield return new ValidationResult("Treatment is required when status is Completed.", new[] { nameof(Treatment) });
            }
        }
    }
}
