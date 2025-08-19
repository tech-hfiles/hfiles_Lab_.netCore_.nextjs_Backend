using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.Domain.Entities.Clinics
{
    public class ClinicConsentForm
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Title { get; set; } = null!;
    }
}
