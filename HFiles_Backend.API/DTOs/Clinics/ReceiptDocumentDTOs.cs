using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.API.DTOs.Clinics
{
    public class ReceiptDocumentDTOs
    {
        public class ReceiptDocumentUploadRequest
        {
            [Required(ErrorMessage = "Receipt number is required")]
            public string ReceiptNumber { get; set; } = null!;

            [Required(ErrorMessage = "Clinic ID is required")]
            [Range(1, int.MaxValue, ErrorMessage = "Clinic ID must be positive")]
            public int ClinicId { get; set; }

            [Required(ErrorMessage = "Visit ID is required")]
            [Range(1, int.MaxValue, ErrorMessage = "Visit ID must be positive")]
            public int VisitId { get; set; }

            [Required(ErrorMessage = "Patient ID is required")]
            [Range(1, int.MaxValue, ErrorMessage = "Patient ID must be positive")]
            public int PatientId { get; set; }

            [Required(ErrorMessage = "At least one document is required")]
            [MinLength(1, ErrorMessage = "At least one document is required")]
            public List<IFormFile> Documents { get; set; } = new();
        }

        // Get Request
        public class ReceiptDocumentGetRequest
        {
            [Required(ErrorMessage = "Clinic ID is required")]
            [Range(1, int.MaxValue, ErrorMessage = "Clinic ID must be positive")]
            public int ClinicId { get; set; }

            [Required(ErrorMessage = "Visit ID is required")]
            [Range(1, int.MaxValue, ErrorMessage = "Visit ID must be positive")]
            public int VisitId { get; set; }

            [Required(ErrorMessage = "Patient ID is required")]
            [Range(1, int.MaxValue, ErrorMessage = "Patient ID must be positive")]
            public int PatientId { get; set; }
        }

        // Delete Request
        public class ReceiptDocumentDeleteRequest
        {
            [Required(ErrorMessage = "Receipt number is required")]
            public string ReceiptNumber { get; set; } = null!;

            [Required(ErrorMessage = "Clinic ID is required")]
            [Range(1, int.MaxValue, ErrorMessage = "Clinic ID must be positive")]
            public int ClinicId { get; set; }
        }

        // Response DTOs
        public class ReceiptDocumentUploadResponse
        {
            public string ReceiptNumber { get; set; } = null!;
            public int ClinicId { get; set; }
            public int PatientId { get; set; }
            public int VisitId { get; set; }
            public int TotalDocumentsUploaded { get; set; }
            public List<string> DocumentUrls { get; set; } = new();
            public string Message { get; set; } = null!;
        }

        public class ReceiptDocumentGetResponse
        {
            public int ClinicId { get; set; }
            public int PatientId { get; set; }
            public int VisitId { get; set; }
            public List<DocumentInfo> Documents { get; set; } = new();
        }

        public class DocumentInfo
        {
            public int RecordId { get; set; }
            public string ReceiptNumber { get; set; } = null!;
            public List<string> DocumentUrls { get; set; } = new();
            public long UploadedAt { get; set; }
        }

        public class ReceiptDocumentDeleteResponse
        {
            public string ReceiptNumber { get; set; } = null!;
            public int DeletedCount { get; set; }
            public string Message { get; set; } = null!;
        }
    }
}
