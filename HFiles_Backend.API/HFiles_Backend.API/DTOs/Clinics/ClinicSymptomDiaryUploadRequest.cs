using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.API.DTOs.Clinics
{
    public class ClinicSymptomDiaryUploadRequest
    {
        [Required(ErrorMessage = "Clinic ID is required.")]
        public int ClinicId { get; set; }

        [Required(ErrorMessage = "Patient ID is required.")]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "Clinic Visit ID is required.")]
        public int ClinicVisitId { get; set; }

        [Required(ErrorMessage = "Symptom diary file is required.")]
        public IFormFile File { get; set; } = null!;
    }
}
