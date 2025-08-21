using HFiles_Backend.Domain.Entities.Clinics;

namespace HFiles_Backend.Domain.Interfaces
{
    public interface IClinicVisitRepository
    {
        Task<ClinicPatient> GetOrCreatePatientAsync(string hfid, string fullName);
        Task<List<ClinicConsentForm>> GetConsentFormsByTitlesAsync(List<string> titles);
        Task SaveVisitAsync(ClinicVisit visit);
        Task<bool> HasVisitInClinicAsync(string hfid, int clinicId);
        Task<List<ClinicVisit>> GetVisitsByClinicIdAsync(int clinicId);
        Task<ClinicVisitConsentForm?> GetVisitConsentFormAsync(int visitConsentFormId);
    }
}
