using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.API.DTOs.Clinics
{
    public class SendSymptomDiaryRequest
    {
        /// <summary>
        /// Health Files ID of the patient
        /// </summary>
        [Required(ErrorMessage = "HFID is required.")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "HFID must be between 3 and 50 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9]+$", ErrorMessage = "HFID must contain only alphanumeric characters.")]
        public string HFID { get; set; } = string.Empty;

        /// <summary>
        /// Clinic ID sending the symptom diary
        /// </summary>
        [Required(ErrorMessage = "Clinic ID is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Clinic ID must be a positive integer.")]
        public int ClinicId { get; set; }

        /// <summary>
        /// Symptom diary file to be sent as email attachment
        /// Allowed formats: PDF, DOC, DOCX, XLS, XLSX
        /// Max size: 10 MB
        /// </summary>
        [Required(ErrorMessage = "Symptom diary file is required.")]
        public IFormFile SymptomDiaryFile { get; set; } = null!;
    }
}
