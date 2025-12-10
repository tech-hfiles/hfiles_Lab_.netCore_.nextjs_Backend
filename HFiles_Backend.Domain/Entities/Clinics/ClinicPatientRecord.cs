using HFiles_Backend.Domain.Entities.Users;
using HFiles_Backend.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Clinics
{
    public class ClinicPatientRecord
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

        [Required]
        public RecordType Type { get; set; }

        [MaxLength(20)]
        public string? UniqueRecordId { get; set; }

        [Required]
        public string JsonData { get; set; } = null!;
        public bool SendToPatient { get; set; } 
    }
}
