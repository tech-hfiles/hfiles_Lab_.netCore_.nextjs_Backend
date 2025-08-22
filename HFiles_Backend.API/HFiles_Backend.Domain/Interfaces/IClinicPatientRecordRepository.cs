using HFiles_Backend.Domain.Entities.Clinics;

namespace HFiles_Backend.Domain.Interfaces
{
    public interface IClinicPatientRecordRepository
    {
        Task SaveAsync(ClinicPatientRecord record);
        Task<List<ClinicPatientRecord>> GetByClinicAndPatientAsync(int clinicId, int patientId);
        Task<List<ClinicPatientRecord>> GetByClinicPatientVisitAsync(int clinicId, int patientId, int clinicVisitId);
        Task<ClinicPatientRecord?> GetReportImageRecordAsync(int clinicId, int patientId, int visitId);
        Task UpdateAsync(ClinicPatientRecord record);
    }
}
