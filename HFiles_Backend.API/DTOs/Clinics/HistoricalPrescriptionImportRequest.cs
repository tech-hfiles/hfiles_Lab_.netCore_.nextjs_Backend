using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.API.DTOs.Clinics
{
    public class HistoricalPrescriptionImportRequest
    {
        [Required(ErrorMessage = "Excel file is required")]
        public IFormFile ExcelFile { get; set; } = null!;
    }

    public class HistoricalPrescriptionImportResponse
    {
        public int TotalProcessed { get; set; }
        public int SuccessfullyAdded { get; set; }
        public int Skipped { get; set; }
        public int SkippedEmptyDrugName { get; set; }
        public int PatientsProcessed { get; set; }
        public int VisitsCreated { get; set; }
        public string Message { get; set; } = "";
        public List<string> SkippedReasons { get; set; } = new();
        public List<AddedHistoricalPrescriptionSummary> AddedPrescriptions { get; set; } = new();
    }

    public class AddedHistoricalPrescriptionSummary
    {
        public string PatientId { get; set; } = "";
        public string PatientName { get; set; } = "";
        public string Date { get; set; } = "";
        public int MedicationCount { get; set; }
        public List<string> Medications { get; set; } = new();
    }

    public class ExcelHistoricalPrescriptionRow
    {
        public string PatientId { get; set; } = "";
        public string PatientName { get; set; } = "";
        public string DrugName { get; set; } = "";
        public string Duration { get; set; } = "";
        public string Dosage { get; set; } = "";
        public string Direction { get; set; } = "";
        public string Advice { get; set; } = "";
        public string DateString { get; set; } = "";
        public DateTime ParsedDate { get; set; }
    }
}
