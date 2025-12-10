using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.API.DTOs.Clinics
{
    public class BulkHistoricalPdfGenerationRequest
    {
        [Required]
        public int ClinicId { get; set; }

        [Required]
        public List<int> PrescriptionRecordIds { get; set; } = new();
    }

    public class GenerateAllHistoricalUnsentRequest
    {
        [Required]
        public int ClinicId { get; set; }
    }

    public class BulkHistoricalPdfGenerationResponse
    {
        public int TotalProcessed { get; set; }
        public int Successful { get; set; }
        public int Failed { get; set; }
        public int SkippedNoUser { get; set; } // Historical data without User records
        public string Message { get; set; } = "";
        public List<SuccessfulHistoricalPdfRecord> SuccessfulRecords { get; set; } = new();
        public List<FailedHistoricalPdfRecord> FailedRecords { get; set; } = new();
    }

    public class SuccessfulHistoricalPdfRecord
    {
        public int PrescriptionRecordId { get; set; }
        public string PatientId { get; set; } = ""; // PatientId like P13062001
        public string PatientName { get; set; } = "";
        public string HFID { get; set; } = "";
        public string AppointmentDate { get; set; } = "";
        public string PrescriptionUrl { get; set; } = "";
        public decimal FileSizeKB { get; set; }
        public bool IsHistorical { get; set; } = true;
    }

    public class FailedHistoricalPdfRecord
    {
        public int PrescriptionRecordId { get; set; }
        public string PatientId { get; set; } = "";
        public string Reason { get; set; } = "";
    }

    // For parsing historical prescription JSON
    public class HistoricalPrescriptionJsonPayload
    {
        public HistoricalPatientInfo Patient { get; set; } = new();
        public List<MedicationInfo> Medications { get; set; } = new();
        public string AdditionalNotes { get; set; } = "";
        public ClinicInfo ClinicInfo { get; set; } = new();
        public string Timestamp { get; set; } = "";
    }

    public class HistoricalPatientInfo
    {
        public string Name { get; set; } = "";
        public string Hfid { get; set; } = ""; // PatientId used as HFID
        public string Gender { get; set; } = "";
        public string Prfid { get; set; } = "";
        public string Dob { get; set; } = "";
        public string Mobile { get; set; } = "";
        public string Doctor { get; set; } = "";
        public string City { get; set; } = "";
        public bool IsHistoricalData { get; set; } = false;
    }

    public class MedicationInfo
    {
        public string Name { get; set; } = "";
        public string Dosage { get; set; } = "";
        public string Frequency { get; set; } = "";
        public string Timing { get; set; } = "";
        public string Instruction { get; set; } = "";
    }

    public class ClinicInfo
    {
        public string Name { get; set; } = "";
        public string Subtitle { get; set; } = "";
        public string Website { get; set; } = "";
    }
}
