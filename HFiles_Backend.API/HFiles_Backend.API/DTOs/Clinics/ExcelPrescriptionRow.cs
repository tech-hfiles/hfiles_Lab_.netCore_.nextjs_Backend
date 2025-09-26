using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.API.DTOs.Clinics
{
    public class PrescriptionImportRequest
    {
        [Required(ErrorMessage = "Excel file is required")]
        public IFormFile ExcelFile { get; set; } = null!;
    }

    public class PrescriptionImportResponse
    {
        public int TotalProcessed { get; set; }
        public int SuccessfullyAdded { get; set; }
        public int Skipped { get; set; }
        public int PatientsProcessed { get; set; }
        public int VisitsCreated { get; set; }
        public string Message { get; set; } = "";
        public List<string> SkippedReasons { get; set; } = new();
        public List<AddedPrescriptionSummary> AddedPrescriptions { get; set; } = new();
    }

    public class AddedPrescriptionSummary
    {
        public string PatientId { get; set; } = "";
        public string PatientName { get; set; } = "";
        public string HFID { get; set; } = "";
        public string Date { get; set; } = "";
        public int MedicationCount { get; set; }
        public List<string> Medications { get; set; } = new();
    }

    public class ExcelPrescriptionRow
    {
        public string PatientId { get; set; } = "";
        public string PatientName { get; set; } = "";
        public string DrugName { get; set; } = "";
        public string Duration { get; set; } = "";
        public string Dosage { get; set; } = "";
        public string Direction { get; set; } = "";
        public string Advice { get; set; } = "";
        public string DateString { get; set; } = "";
        public DateTime ParsedDate { get; set; }
    }

    // For the medication JSON structure
    public class PrescriptionMedication
    {
        public string Name { get; set; } = "";
        public string Dosage { get; set; } = "";
        public string Frequency { get; set; } = "";
        public string Timing { get; set; } = "";
        public string Instruction { get; set; } = "";
    }

    public class PrescriptionPatientInfo
    {
        public string Name { get; set; } = "";
        public string Hfid { get; set; } = "";
        public string Gender { get; set; } = "";
        public string Prfid { get; set; } = "";
        public string Dob { get; set; } = "";
        public string Mobile { get; set; } = "";
        public string Doctor { get; set; } = "";
        public string City { get; set; } = "";
    }

    public class PrescriptionClinicInfo
    {
        public string Name { get; set; } = "Arthrose";
        public string Subtitle { get; set; } = "CRANIOFACIAL PAIN & TMJ CENTRE";
        public string Website { get; set; } = "www.arthrosetmjindia.com";
    }

    public class PrescriptionJsonData
    {
        public PrescriptionPatientInfo Patient { get; set; } = new();
        public List<PrescriptionMedication> Medications { get; set; } = new();
        public string AdditionalNotes { get; set; } = "";
        public PrescriptionClinicInfo ClinicInfo { get; set; } = new();
        public string Timestamp { get; set; } = "";
    }
}
