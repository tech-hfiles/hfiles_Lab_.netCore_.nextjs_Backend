using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore.Storage;

namespace HFiles_Backend.Domain.Interfaces
{
    public interface IUserRepository
    {
        Task<Dictionary<int, User>> GetAllUsersAsync();
		Task<List<User>> GetUsersByHFIDsAsyncs(List<string> hfids);

		Task<User?> GetByIdAsync(int id);
        Task<User?> GetVerifiedUserByEmailAsync(string email);
        Task<string?> GetFullNameBySuperAdminIdAsync(int superAdminId);
        Task<User?> GetUserByHFIDAsync(string hfid);
        Task<List<User>> GetAllAsync();
        Task<User?> GetByPhoneNumberAsync(string phoneNumber);
        Task SaveAsync(UserReport report);
        Task<User?> GetUserByPatientIdAsync(string patientId);
        Task<User?> GetUserByEmailForLookupAsync(string email);
        Task<bool> IsPhoneNumberExistsAsync(string phoneNumber, string? countryCode = null);
        Task<bool> IsEmailExistsAsync(string email);
        Task<User?> GetUserByPhoneNumberAsync(string phoneNumber, string? countryCode = null);
        Task<User?> GetUserByEmailAsync(string email);
        Task<User> CreatePatientUserAsync(User user);
        Task<int> SaveChangesAsync();

        /// <summary>
        /// Adds a new user entity to the database context without immediately saving.
        /// Use CommitAsync() to persist the changes.
        /// </summary>
        /// <param name="user">The user entity to be added.</param>
        Task AddUserAsync(User user);

        /// <summary>
        /// Commits all pending changes in the current context to the database.
        /// This is an alias for SaveChangesAsync() to maintain consistency with transaction patterns.
        /// </summary>
        Task CommitAsync();

        /// <summary>
        /// Checks whether a clinic exists in the system by its ID.
        /// </summary>
        /// <param name="clinicId">The unique identifier of the clinic.</param>
        /// <returns>True if the clinic exists and is not deleted; otherwise false.</returns>
        Task<bool> ExistsAsync(int clinicId);
        Task<IDbContextTransaction> BeginTransactionAsync();
        /// <summary>
        /// Retrieves comprehensive clinic information by clinic ID.
        /// </summary>
        /// <param name="clinicId">The unique identifier of the clinic.</param>
        /// <returns>Clinic entity with all details if found; otherwise null.</returns>
        Task<ClinicSignup?> GetClinicByIdAsync(int clinicId);

        /// <summary>
        /// Adds a new subscription record to the database.
        /// </summary>
        /// <param name="subscription">The subscription entity to be added.</param>
        Task AddSubscriptionAsync(UserSubscription subscription);

        /// <summary>
        /// Gets the count of new users who first visited a clinic within a date range.
        /// </summary>
        /// <param name="clinicId">The clinic ID to check.</param>
        /// <param name="startDate">Start date of the range (inclusive).</param>
        /// <param name="endDate">End date of the range (exclusive).</param>
        /// <returns>Count of new users.</returns>
        Task<int> GetNewClinicUsersCountAsync(int clinicId, DateTime startDate, DateTime endDate);

        /// <summary>
        /// Gets detailed information about new users who first visited a clinic within a date range.
        /// </summary>
        /// <param name="clinicId">The clinic ID to check.</param>
        /// <param name="startDate">Start date of the range (inclusive).</param>
        /// <param name="endDate">End date of the range (exclusive).</param>
        /// <returns>List of user statistics.</returns>
        //Task<List<UserClinicStatDto>> GetNewClinicUsersDetailedAsync(int clinicId, DateTime startDate, DateTime endDate);

        Task<User?> GetUserByHFIDExcludingUserIdAsync(string hfid, int excludeUserId);
        Task<User?> GetUserByEmailExcludingUserIdAsync(string email, int excludeUserId);
        Task UpdateUserAsync(User user);
        Task<User?> GetIndependentUserByEmailAndPhoneAsync(string email, string phoneNumber, string countryCode);
        Task<List<User>> GetUsersByHFIDsAsync(List<string> hfids);
    }
}
