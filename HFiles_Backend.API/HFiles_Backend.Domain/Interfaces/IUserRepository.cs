using HFiles_Backend.Domain.Entities.Users;

namespace HFiles_Backend.Domain.Interfaces
{
    public interface IUserRepository
    {
        Task<Dictionary<int, User>> GetAllUsersAsync();
        Task<User?> GetByIdAsync(int id);
        Task<User?> GetVerifiedUserByEmailAsync(string email);
        Task<string?> GetFullNameBySuperAdminIdAsync(int superAdminId);
        Task<User?> GetUserByHFIDAsync(string hfid);
        Task<List<User>> GetAllAsync();
        Task<User?> GetByPhoneNumberAsync(string phoneNumber);
        Task SaveAsync(UserReport report);
        Task<User?> GetUserByPatientIdAsync(string patientId);
        Task<User?> GetUserByEmailForLookupAsync(string email);
    }
}
