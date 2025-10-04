namespace HFiles_Backend.Application.DTOs.Clinics.PatientRecord
{
    public class NextAvailableIdsResponse
    {
        public string? TreatmentId { get; set; }
        public string? PrescriptionId { get; set; }
        public string? InvoiceId { get; set; }
        public string? ReceiptId { get; set; }
    }
}
