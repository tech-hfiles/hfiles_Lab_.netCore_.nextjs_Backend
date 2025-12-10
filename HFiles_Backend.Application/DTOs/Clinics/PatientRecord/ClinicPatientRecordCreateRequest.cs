using HFiles_Backend.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HFiles_Backend.Application.DTOs.Clinics.PatientRecord
{
    public class ClinicPatientRecordCreateRequest
    {
        [Required]
        public int ClinicId { get; set; }

        [Required]
        public int PatientId { get; set; }

        [Required]
        public int ClinicVisitId { get; set; }

        [Required]
        [EnumDataType(typeof(RecordType))]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public RecordType Type { get; set; }

        [Required]
        public string JsonData { get; set; } = null!;
    }
}
