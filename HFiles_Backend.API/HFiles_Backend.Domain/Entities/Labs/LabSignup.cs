using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Labs
{
    [Table("labsignups")]
    public class LabSignup
    {
        public int Id { get; set; }

        [Required]  
        public string? LabName { get; set; }

        [Required]
        public string? Email { get; set; }

        public string? HFID { get; set; }

        [Required]
        public string? PhoneNumber { get; set; }

        public string? Address { get; set; }

        [Required]
        public string? Pincode { get; set; }

        public string? ProfilePhoto { get; set; }

        [Required]
        public string? PasswordHash { get; set; }

        public bool IsSuperAdmin { get; set; } = false;

        public int LabReference { get; set; } = 0;

        public int DeletedBy { get; set; } = 0;

        [Required]
        public long CreatedAtEpoch { get; set; }
    }
}
