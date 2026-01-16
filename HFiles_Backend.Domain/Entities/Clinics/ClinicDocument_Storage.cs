using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HFiles_Backend.Domain.Entities.Clinics
{
    public class ClinicDocument_Storage
    {
        [Key]
        public int Id { get; set; }

        public int ClinicId { get; set; }

        [ForeignKey("ClinicId")]
        public ClinicSignup Clinics { get; set; } = null!;

        [Required]
        public int PatientId { get; set; }

        [ForeignKey("PatientId")]
        public ClinicPatient Patient { get; set; } = null!;

        public string? FileUrl { get; set; }

        public string? FileName { get; set; }

        public long? FileSizeInBytes { get; set; }

        public bool? IsDeleted { get; set; }
        public bool? SendToPatient { get; set; }

        [Required(ErrorMessage = "EpochTime is required.")]
        [Range(1, long.MaxValue, ErrorMessage = "EpochTime must be a valid timestamp.")]
        public long EpochTime { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}

