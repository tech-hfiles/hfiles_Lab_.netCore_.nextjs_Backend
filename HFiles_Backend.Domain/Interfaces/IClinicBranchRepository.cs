using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Entities.Users;

namespace HFiles_Backend.Domain.Interfaces
{
    public interface IClinicBranchRepository
    {
        Task<bool> IsEmailRegisteredAsync(string email);
        Task<ClinicSignup?> GetParentClinicAsync(int clinicId);
        void AddBranch(ClinicSignup branch);
        Task<ClinicSignup?> GetClinicByIdAsync(int clinicId);
        Task<ClinicSignup?> GetMainClinicAsync(int mainClinicId);
        Task<List<ClinicSignup>> GetBranchesAsync(int mainClinicId);
        Task<ClinicSuperAdmin?> GetSuperAdminByIdAsync(int adminId);
        Task<User?> GetUserByIdAsync(int userId);
        void UpdateClinic(ClinicSignup clinic);
        Task<ClinicSignup?> GetClinicByUserIdAsync(int userId);
        Task<List<int>> GetBranchIdsForMainClinicAsync(int mainClinicId);
        Task<ClinicSignup?> GetDeletedBranchByIdAsync(int branchId, List<int> validBranchIds);
        Task UpdateBranchAsync(ClinicSignup branch);
    }
}
