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

        [MaxLength(10)]
        public string? DOB { get; set; }

        [MaxLength(10)]
        public string? CountryCallingCode { get; set; }

        [MaxLength(10)]
        public string? PhoneNumber { get; set; }

        [MaxLength(100)]
        public string? Email { get; set; }

        public bool IsEmailVerified { get; set; }

        [MaxLength(20)]
        public string? HfId { get; set; }

        [MaxLength(10)]
        public string? Pincode { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(100)]
        public string? State { get; set; }

        [MaxLength(255)]
        public string? Password { get; set; }

        public int? UserReference { get; set; }

        [MaxLength(50)]
        public string? Relation { get; set; }

        public int? DeletedBy { get; set; }

        [MaxLength(10)]
        public string? EmergencyContactCountryCode { get; set; }

        [MaxLength(10)]
        public string? EmergencyContactPhoneNumber { get; set; }

        public long CreatedEpoch { get; set; }

        [MaxLength(5)]
        public string? BloodGroup { get; set; }

        [MaxLength(255)]
        public string? ProfilePhoto { get; set; }

        public bool IsPhoneVerified { get; set; }

        public int? InvitedByUserId { get; set; }
    }
}
