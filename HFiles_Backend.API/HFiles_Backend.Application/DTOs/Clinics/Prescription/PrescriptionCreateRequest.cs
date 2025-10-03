using HFiles_Backend.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HFiles_Backend.Application.DTOs.Clinics.Prescription
{
    public class PrescriptionCreateRequest
    {
        [Required(ErrorMessage = "ClinicId is required.")]
        public int ClinicId { get; set; }

        [Required(ErrorMessage = "Medication name is required.")]
        [MaxLength(100, ErrorMessage = "Medication name cannot exceed 100 characters.")]
        public string MedicationName { get; set; } = null!;

        [Required(ErrorMessage = "Medication dosage is required.")]
        [MaxLength(50, ErrorMessage = "Medication dosage cannot exceed 50 characters.")]
        public string MedicationDosage { get; set; } = null!;

        [Required(ErrorMessage = "Frequency is required.")]
        [EnumDataType(typeof(MedicationFrequency), ErrorMessage = "Invalid frequency value.")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MedicationFrequency Frequency { get; set; }

        [Required(ErrorMessage = "Timing is required.")]
        [EnumDataType(typeof(MedicationTiming), ErrorMessage = "Invalid timing value.")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MedicationTiming Timing { get; set; }

        [Required(ErrorMessage = "Duration is required.")]
        [MaxLength(50, ErrorMessage = "Duration cannot exceed 50 characters.")]
        public string Duration { get; set; } = null!;

        [MaxLength(1000, ErrorMessage = "Instructions cannot exceed 1000 characters.")]
        public string? Instructions { get; set; }
    }
}
