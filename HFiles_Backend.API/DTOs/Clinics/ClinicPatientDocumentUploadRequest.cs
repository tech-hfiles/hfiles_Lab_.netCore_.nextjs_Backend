using HFiles_Backend.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.API.DTOs.Clinics
{
    public class ClinicPatientDocumentUploadRequest
    {
        [Required]
        public int ClinicId { get; set; }

        [Required]
        public int PatientId { get; set; }

        [Required]
        public int ClinicVisitId { get; set; }

        [Required]
        public PaymentMethod PaymentMethod { get; set; }

        [Required]
        public List<DocumentUploadItem> Documents { get; set; } = new();
    }

    public class DocumentUploadItem
    {
        [Required]
        public RecordType Type { get; set; }

        public bool SendToPatient { get; set; }

        public IFormFile? PdfFile { get; set; } 
    }
}
