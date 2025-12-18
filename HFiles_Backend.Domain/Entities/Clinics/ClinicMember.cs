using HFiles_Backend.Domain.Entities.Users;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Clinics
{
    [Table("clinicmembers")]
    public class ClinicMember
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "User ID is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "User ID must be greater than zero.")]
        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        [Required(ErrorMessage = "Clinic ID is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Clinic ID must be greater than zero.")]
        public int ClinicId { get; set; }

        [ForeignKey(nameof(ClinicId))]
        public ClinicSignup Clinic { get; set; } = null!;

        [Required(ErrorMessage = "Role is required.")]
        [MaxLength(20, ErrorMessage = "Role must not exceed 20 characters.")]
        public string Role { get; set; } = "Member";

        [Required(ErrorMessage = "Password hash is required.")]
        [MaxLength(100, ErrorMessage = "Password hash must not exceed 100 characters.")]
        [MinLength(60, ErrorMessage = "Password hash must be at least 60 characters.")]
        public string PasswordHash { get; set; } = null!;

        [Required(ErrorMessage = "CreatedBy is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "CreatedBy must be a valid user ID.")]
        public int CreatedBy { get; set; }

        public string? Coach { get; set; }

        [ForeignKey(nameof(CreatedBy))]
        public User CreatedByUser { get; set; } = null!;

        [Range(0, int.MaxValue, ErrorMessage = "PromotedBy must be a valid user ID or zero.")]
        public int? PromotedBy { get; set; }

        [ForeignKey(nameof(PromotedBy))]
        public User? PromotedByUser { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "DeletedBy must be a valid user ID or zero.")]
        public int DeletedBy { get; set; } = 0;

        //[ForeignKey(nameof(DeletedBy))]
        //public User? DeletedByUser { get; set; }

        [Required(ErrorMessage = "EpochTime is required.")]
        [Range(1, long.MaxValue, ErrorMessage = "EpochTime must be a valid timestamp.")]
        public long EpochTime { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
