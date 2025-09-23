using HFiles_Backend.Domain.Entities.Users;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Clinics
{
    [Table("blacklisted_tokens")]
    public class BlacklistedToken
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string SessionId { get; set; } = null!;

        [Required]
        [ForeignKey(nameof(User))]
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        [Required]
        [ForeignKey(nameof(Clinic))]
        public int ClinicId { get; set; }
        public ClinicSignup Clinic { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string Reason { get; set; } = null!;

        [Required]
        public DateTime BlacklistedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public DateTime ExpiresAt { get; set; }
    }
}
