using HFiles_Backend.Domain.Enums;

namespace HFiles_Backend.Application.DTOs.Clinics.Treatment
{
    public class ClinicTreatmentResponse
    {
        public int TreatmentId { get; set; }
        public string TreatmentName { get; set; } = null!;
        public int QuantityPerDay { get; set; }
        public decimal Cost { get; set; }
        public decimal Total { get; set; }
        public int? Sessions { get; set; }
        public int? Duration { get; set; }
        public TreatmentStatus Status { get; set; }
    }
}
