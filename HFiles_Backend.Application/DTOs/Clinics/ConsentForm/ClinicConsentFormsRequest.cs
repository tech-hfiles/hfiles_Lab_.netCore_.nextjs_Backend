using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.Application.DTOs.Clinics.ConsentForm
{
    public class ClinicConsentFormsRequest
    {
        [Required]
        [MaxLength(20)]
        public string HFID { get; set; } = null!;

        [Required]
        [DataType(DataType.Date)]
        public DateTime AppointmentDate { get; set; }
    }
}
