using HFiles_Backend.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Clinics
{
    public class ClinicPrescription
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ClinicId { get; set; }
        [ForeignKey("ClinicId")]
        public ClinicSignup Clinic { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string MedicationName { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string MedicationDosage { get; set; } = null!;

        [Required]
        public MedicationFrequency Frequency { get; set; }

        [Required]
        public MedicationTiming Timing { get; set; }

        [MaxLength(50)]
        public string? Duration { get; set; } 

        [MaxLength(1000)]
        public string? Instructions { get; set; }
    }
}
