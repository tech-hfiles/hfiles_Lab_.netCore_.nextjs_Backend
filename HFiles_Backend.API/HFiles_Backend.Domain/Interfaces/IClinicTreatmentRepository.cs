using HFiles_Backend.Domain.Entities.Clinics;

namespace HFiles_Backend.Domain.Interfaces
{
    public interface IClinicTreatmentRepository
    {
        Task<ClinicTreatment?> GetByIdAsync(int treatmentId);
        Task<List<ClinicTreatment>> GetByClinicIdAsync(int clinicId);
        Task SaveAsync(ClinicTreatment treatment);
        Task UpdateAsync(ClinicTreatment treatment);
        Task DeleteAsync(int treatmentId);
    }
}
