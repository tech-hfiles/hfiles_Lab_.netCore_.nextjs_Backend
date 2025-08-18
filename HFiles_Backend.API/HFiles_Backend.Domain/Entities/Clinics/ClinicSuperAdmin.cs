using HFiles_Backend.Domain.Entities.Users;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Clinics
{
    [Table("clinicsuperadmins")]
    public class ClinicSuperAdmin
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

        [Required(ErrorMessage = "Password hash is required.")]
        [MinLength(60)]
        [MaxLength(100)]
        public string PasswordHash { get; set; } = null!;

        [Required(ErrorMessage = "Epoch time is required.")]
        [Range(1, long.MaxValue)]
        public long EpochTime { get; set; }

        [Required(ErrorMessage = "IsMain flag is required.")]
        [Range(0, 1)]
        public int IsMain { get; set; } = 1;
    }
}
