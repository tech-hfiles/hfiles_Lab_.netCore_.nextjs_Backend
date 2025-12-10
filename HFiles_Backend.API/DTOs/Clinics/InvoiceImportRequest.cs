namespace HFiles_Backend.API.DTOs.Clinics
{
    // Invoice Import Request DTO
    public class InvoiceImportRequest
    {
        public IFormFile ExcelFile { get; set; } =  null!;
    }

    // Invoice PDF Request DTO
    public class InvoicePdfRequest
    {

    }

    // Excel Invoice Row Model
    public class ExcelInvoiceRow
    {
        public string PatientName { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        public string DateString { get; set; } = string.Empty;
        public DateTime ParsedDate { get; set; }
        public string InvoiceId { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public int Cost { get; set; }
        public string QuantityValue { get; set; } = string.Empty;
        public int FinalCost { get; set; }
    }

    // Invoice Import Response
    public class InvoiceImportResponse
    {
        public int TotalProcessed { get; set; }
        public int Successful { get; set; }
        public int Failed { get; set; }
        public int PatientsProcessed { get; set; }
        public int VisitsCreated { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> SkippedReasons { get; set; } = new();
        public List<AddedInvoiceSummary> AddedInvoices { get; set; } = new();
    }

    // Added Invoice Summary
    public class AddedInvoiceSummary
    {
        public string PatientId { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string HFID { get; set; } = string.Empty;
        public string AppointmentDate { get; set; } = string.Empty;
        public string InvoiceId { get; set; } = string.Empty;
        public int ServiceCount { get; set; }
        public int TotalCost { get; set; }
        public List<string> Services { get; set; } = new();
    }

    // Invoice PDF Response
    public class InvoicePdfResponse
    {
        public int TotalProcessed { get; set; }
        public int Successful { get; set; }
        public int Failed { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<InvoicePdfSuccess> SuccessfulRecords { get; set; } = new();
        public List<InvoicePdfFailed> FailedRecords { get; set; } = new();
    }

    // Invoice PDF Failed - ADD THIS CLASS
    public class InvoicePdfFailed
    {
        public int RecordId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    // Invoice PDF Success
    public class InvoicePdfSuccess
    {
        public int RecordId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string HFID { get; set; } = string.Empty;
        public string AppointmentDate { get; set; } = string.Empty;
        public string InvoiceUrl { get; set; } = string.Empty;
        public decimal FileSizeKB { get; set; }
    }

    // Invoice JSON Payload Models (for parsing stored JSON data)
    public class InvoiceJsonPayload
    {
        public InvoicePatientInfo Patient { get; set; } = new();
        public List<InvoiceServiceInfo> Services { get; set; } = new();
        public int TotalCost { get; set; }
        public int GrandTotal { get; set; }
        public int Paid { get; set; }
        public InvoiceClinicInfo ClinicInfo { get; set; } = new();
    }

    public class InvoicePatientInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Hfid { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string Invid { get; set; } = string.Empty;
        public string Dob { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
    }

    public class InvoiceServiceInfo
    {
        public string Name { get; set; } = string.Empty;
        public string QtyPerDay { get; set; } = string.Empty;
        public int Cost { get; set; }
        public int Total { get; set; }
    }

    public class InvoiceClinicInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string Website { get; set; } = string.Empty;
    }
}
