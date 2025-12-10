using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.Application.DTOs.Clinics.ConsentForm
{
    // Request DTO
    public class SendConsentFormsRequest
    {
        [Required(ErrorMessage = "ConsentForms are required.")]
        [MinLength(1, ErrorMessage = "At least one consent form must be provided.")]
        public List<ConsentFormDto> ConsentForms { get; set; } = new List<ConsentFormDto>();
    }

    public class ConsentFormDto
    {
        [Required(ErrorMessage = "ConsentFormId is required.")]
        public int ConsentFormId { get; set; }

        [Required(ErrorMessage = "ConsentFormName is required.")]
        [MaxLength(100, ErrorMessage = "ConsentFormName cannot exceed 100 characters.")]
        public string ConsentFormName { get; set; } = null!;
    }
}
