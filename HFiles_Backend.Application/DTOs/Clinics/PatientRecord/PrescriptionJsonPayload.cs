using Newtonsoft.Json;

namespace HFiles_Backend.Application.DTOs.Clinics.PatientRecord
{
    public class BulkPdfGenerationRequest
    {
        public int ClinicId { get; set; } = 8; // Default to 8
        public List<int> PrescriptionIds { get; set; } = new();
    }

    public class GenerateUnsentRequest
    {
        public int ClinicId { get; set; } = 8; // Default to 8
    }

    public class BulkPdfGenerationResponse
    {
        public int TotalProcessed { get; set; }
        public int Successful { get; set; }
        public int Failed { get; set; }
        public string Message { get; set; } = "";
        public List<SuccessfulRecord> SuccessfulRecords { get; set; } = new();
        public List<FailedRecord> FailedRecords { get; set; } = new();
    }

    public class SuccessfulRecord
    {
        public int PrescriptionId { get; set; }
        public string PatientName { get; set; } = "";
        public string HFID { get; set; } = "";
        public string AppointmentDate { get; set; } = "";
        public string PrescriptionUrl { get; set; } = "";
        public decimal FileSizeKB { get; set; }
    }

    public class FailedRecord
    {
        public int PrescriptionId { get; set; }
        public string Reason { get; set; } = "";
    }

    // Supporting classes (reuse from previous artifact)
    public class PrescriptionJsonPayload
    {
        [JsonProperty("patient")]
        public PrescriptionPatient Patient { get; set; } = new();

        [JsonProperty("medications")]
        public List<PrescriptionMedication> Medications { get; set; } = new();

        [JsonProperty("additionalNotes")]
        public string AdditionalNotes { get; set; } = "";
    }

    public class PrescriptionPatient
    {
        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("hfid")]
        public string Hfid { get; set; } = "";

        [JsonProperty("gender")]
        public string Gender { get; set; } = "";

        [JsonProperty("prfid")]
        public string Prfid { get; set; } = "";

        [JsonProperty("dob")]
        public string Dob { get; set; } = "";

        [JsonProperty("mobile")]
        public string Mobile { get; set; } = "";

        [JsonProperty("doctor")]
        public string Doctor { get; set; } = "";

        [JsonProperty("city")]
        public string City { get; set; } = "";
    }

    public class PrescriptionMedication
    {
        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("dosage")]
        public string Dosage { get; set; } = "";

        [JsonProperty("frequency")]
        public string Frequency { get; set; } = "";

        [JsonProperty("timing")]
        public string Timing { get; set; } = "";

        [JsonProperty("instruction")]
        public string Instruction { get; set; } = "";
    }
}
