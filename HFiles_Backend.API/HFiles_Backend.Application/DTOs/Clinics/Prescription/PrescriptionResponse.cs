namespace HFiles_Backend.Application.DTOs.Clinics.Prescription
{
    public class PrescriptionResponse
    {
        public string MedicationName { get; set; } = null!;
        public string MedicationDosage { get; set; } = null!;
        public string Frequency { get; set; } = null!;
        public string Timing { get; set; } = null!;
        public string? Instructions { get; set; }
    }
}
