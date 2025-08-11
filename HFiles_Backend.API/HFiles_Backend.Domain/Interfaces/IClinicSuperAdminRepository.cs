using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Entities.Users;
using static System.Net.Mime.MediaTypeNames;

namespace HFiles_Backend.Domain.Interfaces
{
    public interface IClinicSuperAdminRepository
    {
        Task<User?> GetUserByHFIDAsync(string hfid);
        Task AddAsync(ClinicSuperAdmin admin);
        Task<ClinicSuperAdmin?> GetSuperAdminAsync(int userId, int clinicId, int? clinicReference);
        //Task<Application.DTOs.Labs.User?> GetMainSuperAdminDtoAsync(int clinicId);
        Task<Dictionary<int, ClinicSuperAdmin>> GetAdminsByClinicIdAsync(int clinicId);
    }
}
