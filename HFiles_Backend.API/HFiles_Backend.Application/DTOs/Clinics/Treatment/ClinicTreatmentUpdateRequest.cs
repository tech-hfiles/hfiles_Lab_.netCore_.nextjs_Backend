using HFiles_Backend.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HFiles_Backend.Application.DTOs.Clinics.Treatment
{
    public class ClinicTreatmentUpdateRequest
    {
        [MaxLength(100)]
        public string? TreatmentName { get; set; }

        [Range(1, int.MaxValue)]
        public int? QuantityPerDay { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? Cost { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? Total { get; set; }

        [EnumDataType(typeof(TreatmentStatus))]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TreatmentStatus? Status { get; set; }

        [Range(0, int.MaxValue)]
        public int? Duration { get; set; }

        [Range(0, int.MaxValue)]
        public int? Sessions { get; set; }
    }
}
