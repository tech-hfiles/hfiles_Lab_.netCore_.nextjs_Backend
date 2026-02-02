using HFiles_Backend.Domain.Enums;

namespace HFiles_Backend.Application.DTOs.Clinics.PatientRecord
{
    public class ClinicPatientRecordResponse
    {
        public int Id { get; set; }
        public RecordType Type { get; set; }
        public string JsonData { get; set; } = null!;
        public string? UniqueRecordId { get; set; }
        public long EpochTime { get; set; }

		public bool? PaymentVerify { get; set; }
        //for pdf resend fixing 
        public bool SendToPatient { get; set; }
        public int? ReferenceId { get; set; }
        public string? ReferencedUniqueRecordId { get; set; }
        public string? ReferencedRecordType { get; set; }

        // ✅ NEW: Related receipts (for Invoice type)
        public List<RelatedRecordInfo>? RelatedReceipts { get; set; }
    }
    public class RelatedRecordInfo
    {
        public int Id { get; set; }
        public string UniqueRecordId { get; set; }
        public string Type { get; set; }
        public long EpochTime { get; set; }
    }

}
