using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Clinics
{
    [Table("clinicsignups")] 
    public class ClinicSignup
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Clinic name is required.")]
        [MaxLength(100, ErrorMessage = "Clinic name must not exceed 100 characters.")]
        public string ClinicName { get; set; } = null!;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        [MaxLength(100, ErrorMessage = "Email must not exceed 100 characters.")]
        public string Email { get; set; } = null!;

        [MaxLength(50)]
        public string HFID { get; set; } = null!;

        [Required(ErrorMessage = "Phone number is required.")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Phone number must be exactly 10 digits.")]
        public string PhoneNumber { get; set; } = null!;

        [MaxLength(250)]
        public string? Address { get; set; }

        [Required(ErrorMessage = "Pincode is required.")]
        [RegularExpression(@"^\d{5,6}$", ErrorMessage = "Pincode must be 5 or 6 digits.")]
        public string Pincode { get; set; } = null!;

        [MaxLength(500)]
        public string? ProfilePhoto { get; set; }

        [Required(ErrorMessage = "Password hash is required.")]
        [MaxLength(100)]
        public string PasswordHash { get; set; } = null!;

        public bool IsSuperAdmin { get; set; } = false;

        [Range(0, int.MaxValue)]
        public int ClinicReference { get; set; } = 0;

        [Range(0, int.MaxValue)]
        public int DeletedBy { get; set; } = 0;

        [Required(ErrorMessage = "CreatedAtEpoch is required.")]
        [Range(1, long.MaxValue, ErrorMessage = "CreatedAtEpoch must be a positive number.")]
        public long CreatedAtEpoch { get; set; }

        [MaxLength(255)]
        public string? GoogleCalendarId { get; set; }

        [MaxLength(255)]
        public string? GoogleServiceAccountEmail { get; set; }

        [MaxLength(500)]
        public string? GoogleCredentialsPath { get; set; }
        public ICollection<ClinicVisit> Visits { get; set; } = new List<ClinicVisit>();
    }
}
