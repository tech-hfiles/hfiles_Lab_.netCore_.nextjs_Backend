namespace HFiles_Backend.API.DTOs.Clinics
{
    // Receipt Import Request DTO
    public class ReceiptImportRequest
    {
        public IFormFile ExcelFile { get; set; } = null!;
    }

    // Receipt PDF Request DTO
    public class ReceiptPdfRequest
    {
        // Empty for now, but can be extended if needed
    }

    // Excel Receipt Row parsing class
    public class ExcelReceiptRow
    {
        public string PatientName { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        public string DateString { get; set; } = string.Empty;
        public string ReceiptId { get; set; } = string.Empty;
        public string InvoiceId { get; set; } = string.Empty;
        public string ModeOfPayment { get; set; } = string.Empty;
        public int AmountPaid { get; set; }
        public DateTime ParsedDate { get; set; }
    }

    // Receipt Import Response DTO
    public class ReceiptImportResponse
    {
        public int TotalProcessed { get; set; }
        public int Successful { get; set; }
        public int Failed { get; set; }
        public int PatientsProcessed { get; set; }
        public int VisitsCreated { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> SkippedReasons { get; set; } = new List<string>();
        public List<AddedReceiptSummary> AddedReceipts { get; set; } = new List<AddedReceiptSummary>();
    }

    // Added Receipt Summary
    public class AddedReceiptSummary
    {
        public string PatientId { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string HFID { get; set; } = string.Empty;
        public string AppointmentDate { get; set; } = string.Empty;
        public string ReceiptId { get; set; } = string.Empty;
        public string InvoiceId { get; set; } = string.Empty;
        public string ModeOfPayment { get; set; } = string.Empty;
        public int AmountPaid { get; set; }
    }

    // Receipt PDF Response DTO
    public class ReceiptPdfResponse
    {
        public int TotalProcessed { get; set; }
        public int Successful { get; set; }
        public int Failed { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<ReceiptPdfSuccess> SuccessfulRecords { get; set; } = new List<ReceiptPdfSuccess>();
        public List<ReceiptPdfFailed> FailedRecords { get; set; } = new List<ReceiptPdfFailed>();
    }

    // Receipt PDF Success Record
    public class ReceiptPdfSuccess
    {
        public int RecordId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string HFID { get; set; } = string.Empty;
        public string AppointmentDate { get; set; } = string.Empty;
        public string ReceiptUrl { get; set; } = string.Empty;
        public decimal FileSizeKB { get; set; }
    }

    // Receipt PDF Failed Record
    public class ReceiptPdfFailed
    {
        public int RecordId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    // Receipt JSON Payload (for deserializing stored JSON data)
    public class ReceiptJsonPayload
    {
        public ReceiptPatientInfo Patient { get; set; } = null!;
        public ReceiptInfo Receipt { get; set; } = null!;
        public List<ReceiptServiceItem> Services { get; set; } = new List<ReceiptServiceItem>();
        public ReceiptClinicInfo ClinicInfo { get; set; } = null!;
    }

    public class ReceiptPatientInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Uhid { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string ReceiptId { get; set; } = string.Empty;
        public string Dob { get; set; } = string.Empty;
        public string Doctor { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
    }

    public class ReceiptInfo
    {
        public string Date { get; set; } = string.Empty;
        public string ReceiptNumber { get; set; } = string.Empty;
        public string ModeOfPayment { get; set; } = string.Empty;
        public string ChequeNo { get; set; } = string.Empty;
        public int AmountPaid { get; set; }
        public string AmountInWords { get; set; } = string.Empty;
    }

    public class ReceiptServiceItem
    {
        public string Name { get; set; } = string.Empty;
        public string QtyPerDay { get; set; } = string.Empty;
        public int Cost { get; set; }
        public int Total { get; set; }
        public string ModeOfPayment { get; set; } = string.Empty;
        public string ChequeNo { get; set; } = string.Empty;
    }

    public class ReceiptClinicInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string Website { get; set; } = string.Empty;
    }
}
