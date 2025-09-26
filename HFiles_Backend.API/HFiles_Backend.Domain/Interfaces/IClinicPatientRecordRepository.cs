using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Enums;

namespace HFiles_Backend.Domain.Interfaces
{
    public interface IClinicPatientRecordRepository
    {
        Task SaveAsync(ClinicPatientRecord record);
        Task<List<ClinicPatientRecord>> GetByClinicAndPatientAsync(int clinicId, int patientId);
        Task<List<ClinicPatientRecord>> GetByClinicPatientVisitAsync(int clinicId, int patientId, int clinicVisitId);
        Task<ClinicPatientRecord?> GetReportImageRecordAsync(int clinicId, int patientId, int visitId);
        Task UpdateAsync(ClinicPatientRecord record);
        Task<ClinicPatientRecord?> GetByCompositeKeyAsync(int clinicId, int patientId, int visitId, RecordType type);
        Task<ClinicPatient?> GetByIdAsync(int patientId);
        Task<List<ClinicPatientRecord>> GetTreatmentRecordsByClinicIdAsync(int clinicId);
        Task<List<ClinicPatientRecord>> GetPrescriptionRecordsByClinicIdAsync(int clinicId);
        Task<bool> PrescriptionExistsForVisitAsync(int clinicId, int patientId, int visitId);
    }
}
