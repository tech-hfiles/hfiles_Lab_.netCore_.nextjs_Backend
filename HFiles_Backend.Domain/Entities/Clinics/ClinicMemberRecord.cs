using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HFiles_Backend.Domain.Entities.Users;

namespace HFiles_Backend.Domain.Entities.Clinics
{
    [Table("clinicMemberRecords")]
    public class ClinicMemberRecord
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Clinic ID is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Clinic ID must be greater than zero.")]
        public int ClinicId { get; set; }

        [ForeignKey(nameof(ClinicId))]
        public ClinicSignup Clinic { get; set; } = null!;

        [Required(ErrorMessage = "User ID is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "User ID must be greater than zero.")]
        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        [Required(ErrorMessage = "Report name is required.")]
        [MaxLength(255, ErrorMessage = "Report name must not exceed 255 characters.")]
        public string ReportName { get; set; } = null!;

        [Required(ErrorMessage = "Report URL is required.")]
        public string ReportUrl { get; set; } = null!;

        [Required(ErrorMessage = "Report type is required.")]
        [MaxLength(50, ErrorMessage = "Report type must not exceed 50 characters.")]
        public string ReportType { get; set; } = null!;

        [Required(ErrorMessage = "File size is required.")]
        public long FileSize { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "DeletedBy must be a valid user ID or zero.")]
        public int DeletedBy { get; set; } = 0;

        [Required(ErrorMessage = "EpochTime is required.")]
        [Range(1, long.MaxValue, ErrorMessage = "EpochTime must be a valid timestamp.")]
        public long EpochTime { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
