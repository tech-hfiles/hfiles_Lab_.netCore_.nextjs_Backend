using HFiles_Backend.Domain.Enums;

namespace HFiles_Backend.Application.DTOs.Clinics.PatientRecord
{
    public class ClinicPatientRecordResponse
    {
        public RecordType Type { get; set; }
        public string JsonData { get; set; } = null!;
    }
}
