using HFiles_Backend.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Clinics
{
    public class ClinicTreatment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ClinicId { get; set; }

        [ForeignKey("ClinicId")]
        public ClinicSignup Clinic { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string TreatmentName { get; set; } = null!;

        [Required]
        [Range(0, int.MaxValue)]
        public int QuantityPerDay { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal Cost { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal Total { get; set; }

        [Required]
        public TreatmentStatus Status { get; set; }

        public int? Duration { get; set; }

        public int? Sessions { get; set; }
    }
}
