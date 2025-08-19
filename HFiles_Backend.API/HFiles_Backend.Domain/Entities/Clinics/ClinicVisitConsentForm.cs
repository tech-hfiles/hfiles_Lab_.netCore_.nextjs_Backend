using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Clinics
{
    public class ClinicVisitConsentForm
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "ClinicVisitId is required.")]
        public int ClinicVisitId { get; set; }

        [Required(ErrorMessage = "ConsentFormId is required.")]
        public int ConsentFormId { get; set; }

        [ForeignKey("ClinicVisitId")]
        public ClinicVisit Visit { get; set; } = null!;

        [ForeignKey("ConsentFormId")]
        public ClinicConsentForm ConsentForm { get; set; } = null!;
    }
}
