using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Clinics
{
    [Table("clinicprescriptionnotes")]
    public class ClinicPrescriptionNotes
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ClinicId { get; set; }
        [ForeignKey("ClinicId")]
        public ClinicSignup Clinic { get; set; } = null!;

        [Required]
        [MaxLength(10000)]
        public string Notes { get; set; } = string.Empty;
    }
}
