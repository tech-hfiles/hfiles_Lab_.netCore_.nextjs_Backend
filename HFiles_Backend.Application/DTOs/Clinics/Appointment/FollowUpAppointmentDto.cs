using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.Application.DTOs.Clinics.Appointment
{
    /// <summary>
    /// DTO for creating a follow-up appointment with support for both existing and new patients
    /// </summary>
    public class FollowUpAppointmentDto
    {
        // Optional: For existing patients
        [MaxLength(20, ErrorMessage = "HFID cannot exceed 20 characters.")]
        public string? HFID { get; set; }

        // Required for new patients (when HFID is not provided)
        [MaxLength(50, ErrorMessage = "First name cannot exceed 50 characters.")]
        public string? FirstName { get; set; }

        [MaxLength(50, ErrorMessage = "Last name cannot exceed 50 characters.")]
        public string? LastName { get; set; }

        [RegularExpression(@"^\d{2}-\d{2}-\d{4}$", ErrorMessage = "DOB must be in format dd-MM-yyyy.")]
        [MaxLength(10, ErrorMessage = "DOB cannot exceed 10 characters.")]
        public string? DOB { get; set; }

        [RegularExpression(@"^\d{6,10}$", ErrorMessage = "Phone number must be between 6 to 10 digits.")]
        [MaxLength(10, ErrorMessage = "Phone number cannot exceed 10 digits.")]
        public string? PhoneNumber { get; set; }

        [EmailAddress(ErrorMessage = "Invalid email format.")]
        [MaxLength(100, ErrorMessage = "Email cannot exceed 100 characters.")]
        public string? Email { get; set; }

        [RegularExpression(@"^[A-Z]{3} \+\d{1,4}$", ErrorMessage = "Country code must be in format ISO3 +<digits> (e.g., IND +91).")]
        [MaxLength(10, ErrorMessage = "Country code cannot exceed 10 characters.")]
        public string? CountryCode { get; set; }

        public List<string>? ConsentFormTitles { get; set; } = new();

        [Required(ErrorMessage = "Appointment date is required.")]
        [RegularExpression(@"^\d{2}-\d{2}-\d{4}$", ErrorMessage = "Date must be in dd-MM-yyyy format.")]
        public string AppointmentDate { get; set; } = null!;

        [Required(ErrorMessage = "Appointment time is required.")]
        [RegularExpression(@"^\d{2}:\d{2}$", ErrorMessage = "Time must be in HH:mm format.")]
        public string AppointmentTime { get; set; } = null!;

        /// <summary>
        /// Validates that either HFID is provided OR all new patient fields are provided
        /// </summary>
        public bool IsValid(out List<string> errors)
        {
            errors = new List<string>();

            // If HFID is provided, it's an existing patient
            if (!string.IsNullOrWhiteSpace(HFID))
            {
                return true;
            }

            // If HFID is not provided, validate new patient fields
            if (string.IsNullOrWhiteSpace(FirstName))
                errors.Add("First name is required when HFID is not provided.");

            if (string.IsNullOrWhiteSpace(LastName))
                errors.Add("Last name is required when HFID is not provided.");

            if (string.IsNullOrWhiteSpace(DOB))
                errors.Add("Date of birth is required when HFID is not provided.");

            if (string.IsNullOrWhiteSpace(PhoneNumber))
                errors.Add("Phone number is required when HFID is not provided.");

            if (string.IsNullOrWhiteSpace(CountryCode))
                errors.Add("Country code is required when HFID is not provided.");

            return errors.Count == 0;
        }

        /// <summary>
        /// Indicates whether this is a new patient registration
        /// </summary>
        public bool IsNewPatient => string.IsNullOrWhiteSpace(HFID);
    }
}
