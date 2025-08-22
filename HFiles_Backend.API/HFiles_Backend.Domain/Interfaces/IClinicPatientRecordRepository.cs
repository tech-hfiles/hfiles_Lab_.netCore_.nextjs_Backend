using HFiles_Backend.Domain.Entities.Clinics;

namespace HFiles_Backend.Domain.Interfaces
{
    public interface IClinicPatientRecordRepository
    {
        Task SaveAsync(ClinicPatientRecord record);
        Task<List<ClinicPatientRecord>> GetByClinicAndPatientAsync(int clinicId, int patientId);
        Task<List<ClinicPatientRecord>> GetByClinicPatientVisitAsync(int clinicId, int patientId, int clinicVisitId);
    }
}
