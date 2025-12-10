namespace HFiles_Backend.API.DTOs.Clinics
{
    public class ClinicalNotesImportRequest
    {
        public required IFormFile ExcelFile { get; set; }
    }

    public class ClinicalNotesImportResponse
    {
        public int TotalProcessed { get; set; }
        public int Successful { get; set; }
        public int Failed { get; set; }
        public int PatientsProcessed { get; set; }
        public List<string> SkippedReasons { get; set; } = new();
        public List<AddedClinicalNotesSummary> AddedNotes { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }

    public class AddedClinicalNotesSummary
    {
        public string PatientId { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string HFID { get; set; } = string.Empty;
        public int TotalVisitNotes { get; set; }
        public List<string> VisitDates { get; set; } = new();
    }

    public class ExcelClinicalNoteRow
    {
        public string DoctorName { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        public string DateString { get; set; } = string.Empty;
        public DateTime ParsedDate { get; set; }
        public string Investigation { get; set; } = string.Empty;
        public string Diagnosis { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
    }
}
