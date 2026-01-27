namespace HFiles_Backend.Application.DTOs.Clinics.PatientHistory
{
    /// <summary>
    /// Response DTO for patient medical history
    /// </summary>
    public class PatientMedicalHistoryResponse
    {
        public int Id { get; set; }
        public int ClinicPatientId { get; set; }
        public int ClinicId { get; set; }
        public string HFID { get; set; } = string.Empty;
        public string? ProfilePhoto { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string? Medical { get; set; }
        public string? Surgical { get; set; }
        public string? Drugs { get; set; }
        public string? Allergies { get; set; }
        public string? GeneralExamination { get; set; }
        public string? Investigations { get; set; }
        public string? Diagnoses { get; set; }
        public string? ProvisionalDiagnosis { get; set; }
        public string? Notes { get; set; }
        public string? PresentComplaints { get; set; }
        public string? PastHistory { get; set; }
        public string? intensity { get; set; }
        public string? frequency { get; set; }
        public string? duration { get; set; }
        public string? natureofPain { get; set; }
        public string? aggravatingFactors { get; set; }
        public string? relievingFacors { get; set; }
        public long CreatedAt { get; set; }
        public long? UpdatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string? UpdatedBy { get; set; }
        public string? EmergencyNumber { get; set; }

	}
}
