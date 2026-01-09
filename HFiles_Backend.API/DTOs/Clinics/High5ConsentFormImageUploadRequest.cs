using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.API.DTOs.Clinics
{
    public class High5ConsentFormImageUploadRequest
    {
        [Required(ErrorMessage = "At least one file is required")]
        public List<IFormFile> Files { get; set; } = new();

        /// <summary>
        /// Optional: Reference to the consent form in ClinicConsentForm table
        /// </summary>
        public int? ConsentFormId { get; set; }

        /// <summary>
        /// Optional: Title/name of the consent form
        /// </summary>
        [MaxLength(100, ErrorMessage = "Consent form title cannot exceed 100 characters")]
        public string? ConsentFormTitle { get; set; }
    }
}
