using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HFiles_Backend.Domain.Entities.Clinics
{
    public class High5FormImages
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ClinicId { get; set; }

        [ForeignKey("ClinicId")]
        public ClinicSignup Clinic { get; set; } = null!;

        [Required]
        public int PatientId { get; set; }

        [ForeignKey("PatientId")]
        public ClinicPatient Patient { get; set; } = null!;

        [Required]
        public int ClinicVisitId { get; set; }

        [ForeignKey("ClinicVisitId")]
        public ClinicVisit Visit { get; set; } = null!;

        public string? FileUrl { get; set; }
        public int? ConsentFormId { get; set; }
        [ForeignKey("ConsentFormId")]
        public ClinicConsentForm? ConsentForm { get; set; }

        [MaxLength(100)]
        public string? ConsentFormTitle { get; set; }


        [Required(ErrorMessage = "EpochTime is required.")]
        [Range(1, long.MaxValue, ErrorMessage = "EpochTime must be a valid timestamp.")]
        public long EpochTime { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
