using HFiles_Backend.Domain.Enums;

namespace HFiles_Backend.Application.DTOs.Clinics.PatientRecord
{
    public class PatientHistoryResponse
    {
        public string PatientName { get; set; } = string.Empty;
        public string HfId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public List<VisitRecordGroup> Visits { get; set; } = new();
    }

    public class VisitRecordGroup
    {
        public DateTime AppointmentDate { get; set; }
        public bool IsVerified { get; set; }
        public List<string> ConsentForms { get; set; } = new List<string>(); // Keep for backward compatibility
        public List<ConsentFormSimple> ConsentFormsWithNames { get; set; } = new List<ConsentFormSimple>(); // New field with names and URLs
        public List<ConsentFormInfo> ConsentFormsDetails { get; set; } = new List<ConsentFormInfo>(); // Detailed info for internal use
        public List<PatientRecordItem> Records { get; set; } = new List<PatientRecordItem>();
    }

    public class PatientRecordItem
    {
        public RecordType Type { get; set; }
        public string Url { get; set; } = string.Empty;
        public bool SendToPatient { get; set; }
        public string Category { get; set; } = string.Empty;
    }
}
