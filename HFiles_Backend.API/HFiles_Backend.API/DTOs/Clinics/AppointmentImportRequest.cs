using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.API.DTOs.Clinics
{
    public class AppointmentImportRequest
    {
        [Required]
        public IFormFile ExcelFile { get; set; } = default!;
    }

    public class AppointmentImportResponse
    {
        public int TotalProcessed { get; set; }
        public int SuccessfullyAdded { get; set; }
        public int Skipped { get; set; }
        public int PatientsFound { get; set; }
        public int PatientsNotFound { get; set; }
        public List<string> SkippedReasons { get; set; } = new();
        public List<AppointmentSummary> AddedAppointments { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }

    public class AppointmentSummary
    {
        public string PatientName { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        public string VisitorPhoneNumber { get; set; } = string.Empty;
        public string AppointmentDate { get; set; } = string.Empty;
        public string AppointmentTime { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Treatment { get; set; } = string.Empty;
        public bool PatientFoundInUsers { get; set; }
    }

    internal class ExcelAppointmentRow
    {
        public string PatientName { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        public string DateString { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Explanation { get; set; } = string.Empty;
    }
}
