using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Clinics
{
    [Table("clinicotpentries")]
    public class ClinicOtpEntry
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        [MaxLength(100, ErrorMessage = "Email must not exceed 100 characters.")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "OTP code is required.")]
        [MaxLength(6, ErrorMessage = "OTP must not exceed 6 digits.")]
        [RegularExpression(@"^\d+$", ErrorMessage = "OTP must contain only digits.")]
        public string OtpCode { get; set; } = null!;

        [Required(ErrorMessage = "CreatedAt timestamp is required.")]
        public DateTime CreatedAt { get; set; }

        [Required(ErrorMessage = "ExpiryTime is required.")]
        public DateTime ExpiryTime { get; set; }
    }
}
