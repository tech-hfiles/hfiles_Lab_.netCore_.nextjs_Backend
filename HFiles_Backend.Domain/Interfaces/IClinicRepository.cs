using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore.Storage;
using System.Security.Claims;

namespace HFiles_Backend.Domain.Interfaces
{
    public interface IClinicRepository
    {
        Task<IDbContextTransaction> BeginTransactionAsync();
        Task<bool> ExistsAsync(int clinicId);
        Task<bool> EmailExistsAsync(string email);
        Task<ClinicSignup?> GetByEmailAsync(string email);
        Task<ClinicSignup?> GetByPhoneAsync(string phoneNumber);
        Task<ClinicSignup?> GetByIdAndEmailAsync(int id, string email);
        Task<ClinicSignup?> GetByIdAsync(int id);
        Task<ClinicOtpEntry?> GetLatestOtpAsync(string emailOrPhone);
        Task AddSignupAsync(ClinicSignup signup);
        Task RemoveOtpAsync(ClinicOtpEntry otpEntry);
        Task<List<ClinicOtpEntry>> GetExpiredOtpsAsync(string identifier, DateTime now);
        Task RemoveExpiredOtpsAsync(string email, DateTime currentTime);
        Task RemoveOtpsAsync(IEnumerable<ClinicOtpEntry> otpEntries);
        Task AddOtpAsync(ClinicOtpEntry entry);
        Task UpdateAsync(ClinicSignup clinic);
        Task<List<int>> GetBranchIdsAsync(int mainClinicId);
        void Update(ClinicSignup clinic);
        Task<bool> IsClinicAuthorizedAsync(int clinicId, ClaimsPrincipal user);
        Task<ClinicSignup?> GetClinicByIdAsync(int clinicId);
        Task<List<int>> GetBranchClinicIdsAsync(int mainClinicId);
        Task<ClinicMember?> GetDeletedMemberAsync(int userId, int clinicId);
        Task<ClinicSuperAdmin?> GetSuperAdminByIdAsync(int adminId);
        Task<User?> GetUserByIdAsync(int userId);
        void UpdateClinicMember(ClinicMember member);
        Task<ClinicSignup?> GetClinicByEmailAsync(string email);
        Task<ClinicSignup?> GetMainClinicAsync(int clinicId);
        void AddOtpEntry(ClinicOtpEntry entry);
        Task<ClinicOtpEntry?> GetLatestOtpEntryAsync(string email);
        void RemoveOtpEntries(IEnumerable<ClinicOtpEntry> entries);
        void RemoveOtpEntry(ClinicOtpEntry entry);
        void UpdateClinic(ClinicSignup clinic);
        Task<User?> GetVerifiedUserByEmailAsync(string email);
        Task<ClinicSuperAdmin?> GetSuperAdminAsync(int userId, int clinicId);
        Task<ClinicMember?> GetClinicMemberAsync(int userId, int clinicId);
        void UpdateSuperAdmin(ClinicSuperAdmin admin);
        Task<ClinicPatient?> GetPatientByHFIDAsync(string hfid);
        Task<ClinicVisit?> GetVisitAsync(int clinicId, int patientId, DateTime visitDate);
        Task<List<ClinicVisitConsentForm>> GetConsentFormsForVisitAsync(int visitId);
        Task SaveChangesAsync();
    }
}
