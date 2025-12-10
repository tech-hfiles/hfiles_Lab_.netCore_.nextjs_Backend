using Newtonsoft.Json;

namespace HFiles_Backend.API.DTOs.Clinics
{
    public class TreatmentImportRequest
    {
        public IFormFile ExcelFile { get; set; } = null!;
    }

    public class TreatmentPdfRequest
    {
        public int ClinicId { get; set; } = 8;
    }

    public class ExcelTreatmentRow
    {
        public string PatientName { get; set; } = "";
        public string PatientId { get; set; } = "";
        public string DateString { get; set; } = "";
        public string TreatmentName { get; set; } = "";
        public string Status { get; set; } = "";
        public int Cost { get; set; }
        public int Quantity { get; set; }
        public string QuantityType { get; set; } = "";
        public int FinalCost { get; set; }
        public DateTime ParsedDate { get; set; }
    }

    public class TreatmentImportResponse
    {
        public int TotalProcessed { get; set; }
        public int Successful { get; set; }
        public int Failed { get; set; }
        public int PatientsProcessed { get; set; }
        public int VisitsCreated { get; set; }
        public string Message { get; set; } = "";
        public List<string> SkippedReasons { get; set; } = new();
        public List<AddedTreatmentSummary> AddedTreatments { get; set; } = new();
    }

    public class AddedTreatmentSummary
    {
        public string PatientId { get; set; } = "";
        public string PatientName { get; set; } = "";
        public string HFID { get; set; } = "";
        public string AppointmentDate { get; set; } = "";
        public int TreatmentCount { get; set; }
        public int TotalCost { get; set; }
        public List<string> Treatments { get; set; } = new();
    }

    public class TreatmentPdfResponse
    {
        public int TotalProcessed { get; set; }
        public int Successful { get; set; }
        public int Failed { get; set; }
        public string Message { get; set; } = "";
        public List<TreatmentPdfSuccess> SuccessfulRecords { get; set; } = new();
        public List<TreatmentPdfFailed> FailedRecords { get; set; } = new();
    }

    public class TreatmentPdfSuccess
    {
        public int RecordId { get; set; }
        public string PatientName { get; set; } = "";
        public string HFID { get; set; } = "";
        public string AppointmentDate { get; set; } = "";
        public string TreatmentUrl { get; set; } = "";
        public decimal FileSizeKB { get; set; }
    }

    public class TreatmentPdfFailed
    {
        public int RecordId { get; set; }
        public string Reason { get; set; } = "";
    }

    // JSON payload classes for treatment data
    public class TreatmentJsonPayload
    {
        [JsonProperty("patient")]
        public TreatmentPatient Patient { get; set; } = new();

        [JsonProperty("treatments")]
        public List<TreatmentItem> Treatments { get; set; } = new();

        [JsonProperty("totalCost")]
        public int TotalCost { get; set; }

        [JsonProperty("grandTotal")]
        public int GrandTotal { get; set; }

        [JsonProperty("clinicInfo")]
        public TreatmentClinicInfo ClinicInfo { get; set; } = new();
    }

    public class TreatmentPatient
    {
        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("hfid")]
        public string Hfid { get; set; } = "";

        [JsonProperty("gender")]
        public string Gender { get; set; } = "";

        [JsonProperty("tid")]
        public string Tid { get; set; } = "";

        [JsonProperty("dob")]
        public string Dob { get; set; } = "";

        [JsonProperty("mobile")]
        public string Mobile { get; set; } = "";

        [JsonProperty("doctor")]
        public string Doctor { get; set; } = "";

        [JsonProperty("city")]
        public string City { get; set; } = "";
    }

    public class TreatmentItem
    {
        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("qtyPerDay")]
        public string QtyPerDay { get; set; } = "";

        [JsonProperty("cost")]
        public int Cost { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; } = "";

        [JsonProperty("total")]
        public int Total { get; set; }
    }

    public class TreatmentClinicInfo
    {
        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("subtitle")]
        public string Subtitle { get; set; } = "";

        [JsonProperty("website")]
        public string Website { get; set; } = "";
    }
}
