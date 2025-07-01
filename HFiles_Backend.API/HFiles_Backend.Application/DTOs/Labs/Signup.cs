using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.Application.DTOs.Labs
{
    public class Signup
    {
        [Required(ErrorMessage = "Lab name is required.")]
        [MaxLength(100, ErrorMessage = "Lab name must not exceed 100 characters.")]
        public string LabName { get; set; } = null!;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        [MaxLength(100, ErrorMessage = "Email must not exceed 100 characters.")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Phone number is required.")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Phone number must be exactly 10 digits.")]
        public string PhoneNumber { get; set; } = null!;

        [Required(ErrorMessage = "Pincode is required.")]
        [RegularExpression(@"^\d{5,6}$", ErrorMessage = "Pincode must be 5 or 6 digits.")]
        public string Pincode { get; set; } = null!;

        [Required(ErrorMessage = "Password is required.")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
        [MaxLength(100, ErrorMessage = "Password must not exceed 100 characters.")]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&#^()_+=<>]).{6,}$",
            ErrorMessage = "Password must include at least one uppercase letter, one number, and one special character.")]
        public string Password { get; set; } = null!;

        [Required(ErrorMessage = "Confirm password is required.")]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = null!;

        [Required(ErrorMessage = "OTP is required.")]
        [MaxLength(6, ErrorMessage = "OTP must not exceed 6 digits.")]
        [RegularExpression(@"^\d+$", ErrorMessage = "OTP must contain only digits.")]
        public string Otp { get; set; } = null!;
    }

}
