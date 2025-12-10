using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.API.DTOs.Clinics
{
    public class Appointment2019ImportRequest
    {
        [Required]
        public IFormFile CsvFile { get; set; } = default!;

        [Required]
        [Range(1, int.MaxValue)]
        public int ClinicId { get; set; }
    }

    public class Appointment2019ImportResponse
    {
        public int TotalProcessed { get; set; }
        public int SuccessfullyAdded { get; set; }
        public int PatientNotFound { get; set; }
        public int AlreadyHasAppointment { get; set; }
        public int Skipped { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<ImportedAppointmentSummary> AddedAppointments { get; set; } = new();
        public List<string> PatientNotFoundList { get; set; } = new();
        public List<string> SkippedReasons { get; set; } = new();
    }

    public class ImportedAppointmentSummary
    {
        public int AppointmentId { get; set; }
        public int? VisitId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        public string HFID { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string AppointmentDate { get; set; } = string.Empty;
        public string AppointmentTime { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class Csv2019AppointmentRow
    {
        public DateTime Date { get; set; }
        public TimeSpan Time { get; set; } // ADDED: Store time from CSV
        public string PatientId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request model for importing 2020-2024 appointments from CSV
    /// </summary>
    public class Appointment2020To2024ImportRequest
    {
        [Required(ErrorMessage = "CSV file is required")]
        public IFormFile CsvFile { get; set; } = null!;

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Valid Clinic ID is required")]
        public int ClinicId { get; set; }
    }

    /// <summary>
    /// Response model for 2020-2024 appointment import with year-wise breakdown
    /// </summary>
    public class Appointment2020To2024ImportResponse
    {
        public int TotalProcessed { get; set; }
        public int SuccessfullyAdded { get; set; }
        public int PatientNotFound { get; set; }
        public int AlreadyHasAppointment { get; set; }
        public int Skipped { get; set; }
        public List<ImportedAppointmentSummary> AddedAppointments { get; set; } = new();
        public List<string> PatientNotFoundList { get; set; } = new();
        public List<string> SkippedReasons { get; set; } = new();
        public Dictionary<int, YearWiseAppointmentStats> YearWiseBreakdown { get; set; } = new();
        public string Message { get; set; } = "";
    }

    /// <summary>
    /// Year-wise statistics for appointment import
    /// </summary>
    public class YearWiseAppointmentStats
    {
        public int TotalAppointments { get; set; }
        public int Confirmed { get; set; }
        public int Canceled { get; set; }
        public int PatientsNotFound { get; set; }
    }
}