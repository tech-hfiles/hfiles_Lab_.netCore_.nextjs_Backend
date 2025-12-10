using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.Application.DTOs.Clinics.Login
{
    public class ClinicOtpLogin : IValidatableObject
    {
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        [MaxLength(100, ErrorMessage = "Email must not exceed 100 characters.")]
        public string? Email { get; set; }

        [RegularExpression(@"^\d{10}$", ErrorMessage = "Phone number must be exactly 10 digits.")]
        public string? PhoneNumber { get; set; }

        [Required(ErrorMessage = "OTP is required.")]
        [MaxLength(6, ErrorMessage = "OTP must not exceed 6 digits.")]
        [RegularExpression(@"^\d+$", ErrorMessage = "OTP must contain only digits.")]
        public string Otp { get; set; } = null!;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(Email) && string.IsNullOrWhiteSpace(PhoneNumber))
            {
                yield return new ValidationResult(
                    "Either email or phone number must be provided.",
                    new[] { nameof(Email), nameof(PhoneNumber) });
            }

            if (!string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(PhoneNumber))
            {
                yield return new ValidationResult(
                    "Provide either email or phone number, not both.",
                    new[] { nameof(Email), nameof(PhoneNumber) });
            }
        }
    }
}
