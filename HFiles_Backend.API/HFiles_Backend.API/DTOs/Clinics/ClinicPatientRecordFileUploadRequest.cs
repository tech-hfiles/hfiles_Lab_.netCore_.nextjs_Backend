using HFiles_Backend.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.API.DTOs.Clinics
{
    public class ClinicPatientRecordFileUploadRequest
    {
        [Required]
        public int ClinicId { get; set; }

        [Required]
        public int PatientId { get; set; }

        [Required]
        public int ClinicVisitId { get; set; }

        [Required]
        public RecordType Type { get; set; }

        [Required]
        public IFormFileCollection Files { get; set; } = null!;
    }
}
