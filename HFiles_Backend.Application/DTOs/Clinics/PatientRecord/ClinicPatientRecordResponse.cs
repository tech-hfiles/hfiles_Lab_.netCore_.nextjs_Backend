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
    }
}
