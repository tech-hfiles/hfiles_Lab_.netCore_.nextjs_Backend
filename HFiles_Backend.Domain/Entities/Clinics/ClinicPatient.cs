using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.Domain.Entities.Clinics
{
    public class ClinicPatient
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "HFID is required.")]
        [MaxLength(20, ErrorMessage = "HFID cannot exceed 20 characters.")]
        public string HFID { get; set; } = null!;

        [Required(ErrorMessage = "Patient name is required.")]
        [MaxLength(100, ErrorMessage = "Patient name cannot exceed 100 characters.")]
        public string PatientName { get; set; } = null!;

        public ICollection<ClinicVisit> Visits { get; set; } = new List<ClinicVisit>();
    }
}
