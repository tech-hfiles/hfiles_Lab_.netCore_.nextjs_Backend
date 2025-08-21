using HFiles_Backend.Domain.Enums;

namespace HFiles_Backend.Application.DTOs.Clinics.Treatment
{
    public class ClinicTreatmentResponse
    {
        public string TreatmentName { get; set; } = null!;
        public int QuantityPerDay { get; set; }
        public decimal Cost { get; set; }
        public decimal Total { get; set; }
        public TreatmentStatus Status { get; set; }
    }
}
