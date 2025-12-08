using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Clinics
{
    [Table("clinicpatientmedicalhistories")]
    public class ClinicPatientMedicalHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ClinicPatientId { get; set; }

        [Required]
        public int ClinicId { get; set; }

        [Column(TypeName = "longtext")]
        public string? Medical { get; set; }

        [Column(TypeName = "longtext")]
        public string? Surgical { get; set; }

        [Column(TypeName = "longtext")]
        public string? Drugs { get; set; }

        [Column(TypeName = "longtext")]
        public string? Allergies { get; set; }

        [Column(TypeName = "longtext")]
        public string? GeneralExamination { get; set; }

        [Column(TypeName = "longtext")]
        public string? Investigations { get; set; }

        [Column(TypeName = "longtext")]
        public string? Diagnoses { get; set; }

        [Column(TypeName = "longtext")]
        public string? ProvisionalDiagnosis { get; set; }

        [Column(TypeName = "longtext")]
        public string? Notes { get; set; }

        [Column(TypeName = "longtext")]
        public string? PresentComplaints { get; set; }

        [Column(TypeName = "longtext")]
        public string? PastHistory { get; set; }

        [Column(TypeName = "longtext")]
        public string? Intensity { get; set; }

        [Column(TypeName = "longtext")]
        public string? Frequency { get; set; }

        [Column(TypeName = "longtext")]
        public string? Duration { get; set; } 
        
        [Column(TypeName = "longtext")]
        public string? NatureofPain { get; set; }

        [Column(TypeName = "longtext")]
        public string? AggravatingFactors { get; set; }

        [Column(TypeName = "longtext")]
        public string? RelievingFacors { get; set; }


        [Required]
        public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public long? UpdatedAt { get; set; }

        [Required]
        public int CreatedBy { get; set; }

        public int? UpdatedBy { get; set; }

        [Required]
        public int DeletedBy { get; set; } = 0;

        // Navigation properties
        [ForeignKey("ClinicPatientId")]
        public virtual ClinicPatient ClinicPatient { get; set; } = null!;

        [ForeignKey("ClinicId")]
        public virtual ClinicSignup Clinic { get; set; } = null!;
    }
}