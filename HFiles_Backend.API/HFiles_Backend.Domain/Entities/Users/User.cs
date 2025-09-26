using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Users
{
    [Table("users")]
    public class User
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(50)]
        public string? FirstName { get; set; }

        [MaxLength(50)]
        public string? LastName { get; set; }

        [MaxLength(10)]
        public string? Gender { get; set; }

        [RegularExpression(@"^\d{2}-\d{2}-\d{4}$")]
        [MaxLength(10)]
        public string? DOB { get; set; }

        [MaxLength(10)]
        public string? CountryCallingCode { get; set; }

        [Required]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = default!;

        public bool IsPhoneVerified { get; set; }

        [Required]
        [EmailAddress]
        [MaxLength(100)]
        public string Email { get; set; } = default!;

        public bool IsEmailVerified { get; set; }

        [MaxLength(20)]
        public string? HfId { get; set; }

        [MaxLength(20)]
        public string? PatientId { get; set; }

        [MaxLength(10)]
        public string? Pincode { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(100)]
        public string? State { get; set; }

        [RegularExpression(@"^(A|B|AB|O)[+-]$")]
        [MaxLength(5)]
        public string? BloodGroup { get; set; }

        [Range(0, 9)]
        public int? HeightFeet { get; set; }

        [Range(0, 11)]
        public int? HeightInches { get; set; }

        [Range(0, 500)]
        public float? WeightKg { get; set; }

        [MaxLength(255)]
        public string? ProfilePhoto { get; set; }

        [MaxLength(255)]
        public string? Password { get; set; }

        public int UserReference { get; set; }

        public int InvitedByUserId { get; set; }

        [MaxLength(50)]
        public string? Relation { get; set; }

        [Required]
        public int DeletedBy { get; set; } = 0;

        [MaxLength(10)]
        public string? EmergencyContactCountryCode { get; set; }

        [MaxLength(10)]
        public string? EmergencyContactPhoneNumber { get; set; }

        public long CreatedEpoch { get; set; }
    }
}
