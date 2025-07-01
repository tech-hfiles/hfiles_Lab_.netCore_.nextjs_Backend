using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.Application.DTOs.Labs
{
    public class LoginOtpRequest : IValidatableObject
    {
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        [MaxLength(100)]
        public string? Email { get; set; }

        [RegularExpression(@"^\d{10}$", ErrorMessage = "Phone number must be exactly 10 digits.")]
        public string? PhoneNumber { get; set; }

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



    public class OtpLogin : IValidatableObject
    {
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        [MaxLength(100, ErrorMessage = "Email must not exceed 100 characters.")]
        public string? Email { get; set; }

        [Phone(ErrorMessage = "Invalid phone number.")]
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
        }
    }


    public class PasswordLogin
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        [MaxLength(100, ErrorMessage = "Email must not exceed 100 characters.")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Password is required.")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
        [MaxLength(100, ErrorMessage = "Password must not exceed 100 characters.")]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&#^()_+=<>]).{6,}$",
            ErrorMessage = "Password must include at least one uppercase letter, one number, and one special character.")]
        public string Password { get; set; } = null!;
    }
}
