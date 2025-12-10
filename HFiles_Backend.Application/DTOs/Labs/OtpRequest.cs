using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.Application.DTOs.Labs
{
    public class OtpRequest
    {
        [Required(ErrorMessage = "Lab name is required.")]
        [MaxLength(100, ErrorMessage = "Lab name must not exceed 100 characters.")]
        public string LabName { get; set; } = null!;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        [MaxLength(100, ErrorMessage = "Email must not exceed 100 characters.")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Phone number is required.")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Phone number must be exactly 10 digits.")]
        public string PhoneNumber { get; set; } = null!;
    }
}
