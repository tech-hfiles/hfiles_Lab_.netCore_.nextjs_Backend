using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.API.DTOs.Clinics
{
    public class InvoiceHistoricalImportRequest
    {
        [Required(ErrorMessage = "Excel file is required")]
        public IFormFile ExcelFile { get; set; } = null!;
    }

    public class InvoiceHistoricalImportResponse
    {
        public int TotalProcessed { get; set; }
        public int Successful { get; set; }
        public int Failed { get; set; }
        public int PatientsProcessed { get; set; }
        public int VisitsCreated { get; set; }
        public string Message { get; set; } = "";
        public List<string> SkippedReasons { get; set; } = new();
        public List<AddedInvoiceHistoricalSummary> AddedInvoices { get; set; } = new();
    }

    public class AddedInvoiceHistoricalSummary
    {
        public string PatientId { get; set; } = "";
        public string PatientName { get; set; } = "";
        public string HFID { get; set; } = "";
        public string AppointmentDate { get; set; } = "";
        public string InvoiceId { get; set; } = "";
        public int ServiceCount { get; set; }
        public int TotalCost { get; set; }
        public List<string> Services { get; set; } = new();
    }

    public class ExcelInvoiceHistoricalRow
    {
        public string PatientName { get; set; } = "";
        public string PatientId { get; set; } = "";
        public string DateString { get; set; } = "";
        public DateTime ParsedDate { get; set; }
        public string InvoiceId { get; set; } = "";
        public string ServiceName { get; set; } = "";
        public int Cost { get; set; }  // The actual price
        public string QuantityValue { get; set; } = "";
        public string QuantityType { get; set; } = "";  // "QTY" - not used in calculations
    }

    // ===== PDF Generation DTOs =====

    public class InvoiceHistoricalPdfRequest
    {
        [Required]
        public int ClinicId { get; set; }
    }

    public class InvoiceHistoricalPdfResponse
    {
        public int TotalProcessed { get; set; }
        public int Successful { get; set; }
        public int Failed { get; set; }
        public string Message { get; set; } = "";
        public List<InvoiceHistoricalPdfSuccess> SuccessfulRecords { get; set; } = new();
        public List<InvoiceHistoricalPdfFailed> FailedRecords { get; set; } = new();
    }

    public class InvoiceHistoricalPdfSuccess
    {
        public int RecordId { get; set; }
        public string PatientName { get; set; } = "";
        public string HFID { get; set; } = "";
        public string AppointmentDate { get; set; } = "";
        public string InvoiceUrl { get; set; } = "";
        public decimal FileSizeKB { get; set; }
    }

    public class InvoiceHistoricalPdfFailed
    {
        public int RecordId { get; set; }
        public string Reason { get; set; } = "";
    }

    // ===== JSON Payload Structure =====

    public class InvoiceHistoricalJsonPayload
    {
        public InvoicePatientInfo Patient { get; set; } = new();
        public List<InvoiceServiceInfo> Services { get; set; } = new();
        public int TotalCost { get; set; }
        public int GrandTotal { get; set; }
        public int Paid { get; set; }
        public InvoiceClinicInfo ClinicInfo { get; set; } = new();
    }

    //public class InvoicePatientInfo
    //{
    //    public string Name { get; set; } = "";
    //    public string Hfid { get; set; } = "";
    //    public string Gender { get; set; } = "";
    //    public string Invid { get; set; } = "";
    //    public string Dob { get; set; } = "";
    //    public string Date { get; set; } = "";
    //    public string Mobile { get; set; } = "";
    //    public string City { get; set; } = "";
    //}

    //public class InvoiceServiceInfo
    //{
    //    public string Name { get; set; } = "";
    //    public string QtyPerDay { get; set; } = "";
    //    public int Cost { get; set; }
    //    public int Total { get; set; }
    //}

    //public class InvoiceClinicInfo
    //{
    //    public string Name { get; set; } = "";
    //    public string Subtitle { get; set; } = "";
    //    public string Website { get; set; } = "";
    //}
}
