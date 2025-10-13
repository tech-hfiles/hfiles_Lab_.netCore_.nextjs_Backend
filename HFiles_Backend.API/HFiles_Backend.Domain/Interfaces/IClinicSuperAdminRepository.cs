using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Entities.Users;

namespace HFiles_Backend.Domain.Interfaces
{
    public interface IClinicSuperAdminRepository
    {
        Task<User?> GetUserByHFIDAsync(string hfid);
        Task AddAsync(ClinicSuperAdmin admin);
        Task<ClinicSuperAdmin?> GetSuperAdminAsync(int userId, int clinicId, int? clinicReference);
        Task<Dictionary<int, ClinicSuperAdmin>> GetAdminsByClinicIdAsync(int clinicId);
        Task<ClinicSuperAdmin?> GetByIdAsync(int id);
        Task<ClinicSuperAdmin?> GetMainSuperAdminAsync(int clinicId);
        Task<ClinicSuperAdmin?> GetExistingSuperAdminAsync(int userId, int clinicId);
        Task<ClinicSuperAdmin?> GetMainSuperAdminAsync(int userId, int clinicId);
        void Update(ClinicSuperAdmin admin);
        void Add(ClinicSuperAdmin admin);
        Task<ClinicSuperAdmin?> GetSuperAdminByIdAsync(int superAdminId);
    }
}
