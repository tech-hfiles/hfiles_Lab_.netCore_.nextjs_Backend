using HFiles_Backend.Domain.Entities.Clinics;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Users
{
    [Table("userreports")]
    public class UserReport
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [MaxLength(255)]
        public string? ReportName { get; set; }

        public int ReportCategory { get; set; }

        [Required]
        [MaxLength(255)]
        public string? ReportUrl { get; set; }

        [Required]
        public long EpochTime { get; set; }

        [Column(TypeName = "decimal(65,30)")]
        public decimal FileSize { get; set; }

        [MaxLength(50)]
        public string? UserType { get; set; }

        [MaxLength(10)]
        public string? UploadedBy { get; set; }

        public int? LabId { get; set; }

        public int? ClinicId { get; set; }
        [ForeignKey("ClinicId")]
        public ClinicSignup? Clinics { get; set; }

        public int? LabUserReportId { get; set; }

        public int DeletedBy { get; set; }
    }
}
