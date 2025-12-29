using HFiles_Backend.Domain.Enums;

namespace HFiles_Backend.Application.DTOs.Clinics.Appointment
{
    public class ClinicPatientResponseDto
    {
        public int TotalPatients { get; set; }
        public List<PatientDto>? Patients { get; set; }
    }

    public class PatientDto
    {
        public int PatientId { get; set; }
        public string? PatientName { get; set; }
        public string? HFID { get; set; }
        public string? ProfilePhoto { get; set; }
        public string? VisitorPhoneNumber { get; set; }
        public string? LastVisitDate { get; set; }
        public PaymentMethod? PaymentMethod { get; set; }
        public string? TreatmentNames { get; set; }
        public string? PaymentStatus { get; set; }
        public decimal AmountDue { get; set; }  // ✅ NEW: Total amount due
        public string? PackageName { get; set; }
        public List<VisitDto>? Visits { get; set; }
    }

    public class VisitDto
    {
        public int VisitId { get; set; }
        public string? AppointmentDate { get; set; }
        public string? AppointmentTime { get; set; }
        public List<string>? ConsentFormsSent { get; set; }
    }
}
