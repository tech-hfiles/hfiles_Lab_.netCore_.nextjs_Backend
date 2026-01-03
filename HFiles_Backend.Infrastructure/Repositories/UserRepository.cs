using HFiles_Backend.Application.DTOs.Clinics.Statistics;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Entities.Users;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace HFiles_Backend.Infrastructure.Repositories
{
    /// <summary>
    /// Repository implementation for managing user-related operations including
    /// user retrieval, creation, and clinic-related operations.
    /// </summary>
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _context;
        private readonly ILogger<UserRepository> _logger;

        public UserRepository(AppDbContext context, ILogger<UserRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ==================== USER RETRIEVAL METHODS ====================

        /// <summary>
        /// Retrieves all users as a dictionary with user ID as the key.
        /// </summary>
        public async Task<Dictionary<int, User>> GetAllUsersAsync()
        {
            try
            {
                return await _context.Users.ToDictionaryAsync(u => u.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all users as dictionary");
                throw;
            }
        }

        /// <summary>
        /// Retrieves a user by their ID.
        /// </summary>
        public async Task<User?> GetByIdAsync(int id)
        {
            try
            {
                return await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user by ID: {UserId}", id);
                throw;
            }
        }

        /// <summary>
        /// Retrieves a verified user by email address.
        /// </summary>
        public async Task<User?> GetVerifiedUserByEmailAsync(string email)
        {
            try
            {
                return await _context.Users
                    .FirstOrDefaultAsync(u =>
                        u.Email == email &&
                        u.IsEmailVerified &&
                        u.UserReference == 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving verified user by email: {Email}", email);
                throw;
            }
        }

        /// <summary>
        /// Retrieves full name of a super admin by their ID.
        /// </summary>
        public async Task<string?> GetFullNameBySuperAdminIdAsync(int superAdminId)
        {
            try
            {
                return await (from sa in _context.ClinicSuperAdmins
                              join u in _context.Users on sa.UserId equals u.Id
                              where sa.Id == superAdminId
                              select u.FirstName + " " + u.LastName)
                              .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving full name for super admin ID: {SuperAdminId}", superAdminId);
                throw;
            }
        }

        /// <summary>
        /// Retrieves a user by their Health File ID (HFID).
        /// </summary>
        public async Task<User?> GetUserByHFIDAsync(string hfid)
        {
            try
            {
                return await _context.Users
                    .Where(u => u.HfId == hfid && u.DeletedBy == 0)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user by HFID: {HFID}", hfid);
                throw;
            }
        }

        /// <summary>
        /// Retrieves all users from the database.
        /// </summary>
        public async Task<List<User>> GetAllAsync()
        {
            try
            {
                return await _context.Users.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all users");
                throw;
            }
        }

        /// <summary>
        /// Retrieves a user by phone number.
        /// </summary>
        public async Task<User?> GetByPhoneNumberAsync(string phoneNumber)
        {
            try
            {
                return await _context.Users
                    .FirstOrDefaultAsync(u =>
                        u.PhoneNumber == phoneNumber &&
                        u.UserReference == 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user by phone number: {PhoneNumber}", phoneNumber);
                throw;
            }
        }

        /// <summary>
        /// Retrieves a user by patient ID.
        /// </summary>
        public async Task<User?> GetUserByPatientIdAsync(string patientId)
        {
            try
            {
                return await _context.Users
                    .FirstOrDefaultAsync(u =>
                        u.PatientId == patientId &&
                        u.DeletedBy == 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user by patient ID: {PatientId}", patientId);
                throw;
            }
        }

        /// <summary>
        /// Retrieves a user by email for lookup purposes (no tracking).
        /// </summary>
        public async Task<User?> GetUserByEmailForLookupAsync(string email)
        {
            try
            {
                return await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u =>
                        u.Email == email &&
                        u.DeletedBy == 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user by email for lookup: {Email}", email);
                throw;
            }
        }

        /// <summary>
        /// Retrieves a user by phone number with optional country code filtering.
        /// </summary>
        public async Task<User?> GetUserByPhoneNumberAsync(string phoneNumber, string? countryCode = null)
        {
            try
            {
                var query = _context.Users
                    .Where(u => u.PhoneNumber == phoneNumber && u.DeletedBy == 0);

                if (!string.IsNullOrWhiteSpace(countryCode))
                {
                    query = query.Where(u => u.CountryCallingCode == countryCode);
                }

                return await query.FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user by phone number: {PhoneNumber}", phoneNumber);
                throw;
            }
        }

        /// <summary>
        /// Retrieves a user by email address.
        /// </summary>
        public async Task<User?> GetUserByEmailAsync(string email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                    return null;

                return await _context.Users
                    .Where(u => u.Email == email && u.DeletedBy == 0)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user by email: {Email}", email);
                throw;
            }
        }

        // ==================== VALIDATION METHODS ====================

        /// <summary>
        /// Checks if a phone number already exists in the system.
        /// </summary>
        public async Task<bool> IsPhoneNumberExistsAsync(string phoneNumber, string? countryCode = null)
        {
            try
            {
                var query = _context.Users
                    .Where(u => u.PhoneNumber == phoneNumber && u.DeletedBy == 0);

                if (!string.IsNullOrWhiteSpace(countryCode))
                {
                    query = query.Where(u => u.CountryCallingCode == countryCode);
                }

                return await query.AnyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking phone number existence: {PhoneNumber}", phoneNumber);
                throw;
            }
        }

        /// <summary>
        /// Checks if an email already exists in the system.
        /// </summary>
        public async Task<bool> IsEmailExistsAsync(string email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                    return false;

                return await _context.Users
                    .AnyAsync(u => u.Email == email && u.DeletedBy == 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking email existence: {Email}", email);
                throw;
            }
        }

        // ==================== USER CREATION METHODS ====================

        /// <summary>
        /// Creates a new patient user and immediately saves to database.
        /// </summary>
        public async Task<User> CreatePatientUserAsync(User user)
        {
            try
            {
                await _context.Users.AddAsync(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Patient user created successfully. UserId: {UserId}, HFID: {HFID}",
                    user.Id, user.HfId);

                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating patient user for Email: {Email}", user.Email);
                throw;
            }
        }

        /// <summary>
        /// Adds a new user entity to the database context without immediately saving.
        /// Use CommitAsync() to persist the changes.
        /// </summary>
        public async Task AddUserAsync(User user)
        {
            try
            {
                await _context.Users.AddAsync(user);

                _logger.LogDebug("User added to context. Email: {Email}", user.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding user to context");
                throw;
            }
        }

        // ==================== SAVE/COMMIT METHODS ====================

        /// <summary>
        /// Saves a user report to the database.
        /// </summary>
        public async Task SaveAsync(UserReport report)
        {
            try
            {
                _context.UserReports.Add(report);
                await _context.SaveChangesAsync();

                _logger.LogInformation("User report saved successfully. ReportId: {ReportId}", report.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving user report");
                throw;
            }
        }

        /// <summary>
        /// Persists all pending changes in the current context to the database.
        /// </summary>
        public async Task<int> SaveChangesAsync()
        {
            try
            {
                var entriesWritten = await _context.SaveChangesAsync();

                _logger.LogDebug("Successfully saved {Count} entries to database", entriesWritten);

                return entriesWritten;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving changes to database");
                throw;
            }
        }

        /// <summary>
        /// Commits all pending changes in the current context to the database.
        /// This is an alias for SaveChangesAsync() to maintain consistency with transaction patterns.
        /// </summary>
        public async Task CommitAsync()
        {
            try
            {
                await SaveChangesAsync();

                _logger.LogDebug("Changes committed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error committing changes");
                throw;
            }
        }

        // ==================== TRANSACTION METHODS ====================

        /// <summary>
        /// Begins a new database transaction scope for atomic operations.
        /// </summary>
        public async Task<IDbContextTransaction> BeginTransactionAsync()
        {
            try
            {
                var transaction = await _context.Database.BeginTransactionAsync();

                _logger.LogDebug("Database transaction started");

                return transaction;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error beginning database transaction");
                throw;
            }
        }

        // ==================== CLINIC METHODS ====================

        /// <summary>
        /// Checks whether a clinic exists in the system by its ID.
        /// Only returns true for non-deleted clinics.
        /// </summary>
        public async Task<bool> ExistsAsync(int clinicId)
        {
            try
            {
                return await _context.ClinicSignups
                    .AsNoTracking()
                    .AnyAsync(c => c.Id == clinicId && c.DeletedBy == 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking clinic existence for ClinicId: {ClinicId}", clinicId);
                throw;
            }
        }

        /// <summary>
        /// Retrieves comprehensive clinic information by clinic ID.
        /// Returns null if the clinic doesn't exist or has been deleted.
        /// </summary>
        public async Task<ClinicSignup?> GetClinicByIdAsync(int clinicId)
        {
            try
            {
                var clinic = await _context.ClinicSignups
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == clinicId && c.DeletedBy == 0);

                if (clinic == null)
                {
                    _logger.LogWarning("Clinic not found for ClinicId: {ClinicId}", clinicId);
                }

                return clinic;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving clinic by ID: {ClinicId}", clinicId);
                throw;
            }
        }

        // ==================== SUBSCRIPTION METHODS ====================

        /// <summary>
        /// Adds a new subscription record to the database.
        /// The subscription is associated with a user and defines their access level.
        /// </summary>
        public async Task AddSubscriptionAsync(UserSubscription subscription)
        {
            try
            {
                await _context.UserSubscriptions.AddAsync(subscription);

                _logger.LogInformation(
                    "Subscription added for UserId: {UserId}, Plan: {Plan}",
                    subscription.UserId,
                    subscription.SubscriptionPlan);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error adding subscription for UserId: {UserId}",
                    subscription.UserId);
                throw;
            }
        }

        // ==================== CLINIC USER STATISTICS METHODS ====================

        /// <summary>
        /// Gets the count of new users who first visited a clinic within a date range.
        /// Uses optimized two-step query to avoid EF Core translation issues.
        /// </summary>
        public async Task<int> GetNewClinicUsersCountAsync(int clinicId, DateTime startDate, DateTime endDate)
        {
            try
            {
                _logger.LogInformation(
                    "Getting new users count for clinic {ClinicId} from {StartDate} to {EndDate}",
                    clinicId, startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));

                // Step 1: Get first visit dates grouped by HFID (materialized to memory)
                var firstVisits = await _context.ClinicVisits
                    .Where(cv => cv.ClinicId == clinicId)
                    .GroupBy(cv => cv.Patient.HFID)
                    .Select(g => new
                    {
                        HFID = g.Key,
                        FirstVisitDate = g.Min(cv => cv.AppointmentDate)
                    })
                    .Where(fv => fv.FirstVisitDate >= startDate && fv.FirstVisitDate < endDate)
                    .ToListAsync();

                _logger.LogDebug("Found {Count} unique HFIDs with first visits in date range", firstVisits.Count);

                // Step 2: Extract HFIDs
                var hfids = firstVisits.Select(fv => fv.HFID).ToList();

                if (!hfids.Any())
                {
                    _logger.LogInformation("No new users found for clinic {ClinicId} in specified date range", clinicId);
                    return 0;
                }

                // Step 3: Count users with these HFIDs
                var userCount = await _context.Users
                    .Where(u => hfids.Contains(u.HfId!) && u.DeletedBy == 0)
                    .CountAsync();

                _logger.LogInformation(
                    "Found {Count} new users for clinic {ClinicId} between {StartDate} and {EndDate}",
                    userCount, clinicId, startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));

                return userCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting new clinic users count for clinic {ClinicId}", clinicId);
                throw;
            }
        }

        /// <summary>
        /// Gets detailed information about new users who first visited a clinic within a date range.
        /// Uses optimized single-query approach with proper joins.
        /// </summary>
        /// <summary>
        /// Gets detailed information about new users who first visited a clinic within a date range.
        /// Uses two-step approach to avoid EF Core translation issues with MySQL.
        /// </summary>
        public async Task<List<UserClinicStatDto>> GetNewClinicUsersDetailedAsync(
            int clinicId,
            DateTime startDate,
            DateTime endDate)
        {
            try
            {
                _logger.LogInformation(
                    "Getting detailed new users for clinic {ClinicId} from {StartDate} to {EndDate}",
                    clinicId, startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));

                // Step 1: Get first visits with HFID (materialized to memory)
                var firstVisits = await _context.ClinicVisits
                    .Where(cv => cv.ClinicId == clinicId)
                    .GroupBy(cv => cv.Patient.HFID)
                    .Select(g => new
                    {
                        HFID = g.Key,
                        FirstVisitDate = g.Min(cv => cv.AppointmentDate)
                    })
                    .Where(fv => fv.FirstVisitDate >= startDate && fv.FirstVisitDate < endDate)
                    .ToListAsync();

                _logger.LogDebug("Found {Count} unique HFIDs with first visits in date range", firstVisits.Count);

                // Step 2: Extract HFIDs
                var hfids = firstVisits.Select(fv => fv.HFID).ToList();

                if (!hfids.Any())
                {
                    _logger.LogInformation("No new users found for clinic {ClinicId} in specified date range", clinicId);
                    return new List<UserClinicStatDto>();
                }

                // Step 3: Get users with these HFIDs
                var users = await _context.Users
                    .Where(u => hfids.Contains(u.HfId!) && u.DeletedBy == 0)
                    .Select(u => new
                    {
                        u.Id,
                        u.HfId,
                        u.FirstName,
                        u.LastName,
                        u.PhoneNumber,
                        u.Email,
                        u.CreatedEpoch
                    })
                    .ToListAsync();

                _logger.LogDebug("Found {Count} users matching HFIDs", users.Count);

                // Step 4: Join in memory and create DTOs
                var result = users
                    .Select(u =>
                    {
                        var firstVisit = firstVisits.FirstOrDefault(fv => fv.HFID == u.HfId);
                        return new UserClinicStatDto
                        {
                            UserId = u.Id,
                            HFID = u.HfId,
                            FullName = $"{u.FirstName ?? ""} {u.LastName ?? ""}".Trim(),
                            PhoneNumber = u.PhoneNumber,
                            Email = u.Email,
                            FirstVisitDate = firstVisit?.FirstVisitDate ?? DateTime.MinValue,
                            UserCreatedDate = DateTimeOffset.FromUnixTimeSeconds(u.CreatedEpoch).DateTime
                        };
                    })
                    .Where(u => u.FirstVisitDate != DateTime.MinValue) // Filter out any nulls
                    .OrderBy(u => u.FirstVisitDate)
                    .ThenBy(u => u.FullName)
                    .ToList();

                _logger.LogInformation(
                    "Retrieved {Count} detailed user records for clinic {ClinicId}",
                    result.Count, clinicId);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting detailed clinic users for clinic {ClinicId}", clinicId);
                throw;
            }
        }

        /// <summary>
        /// Retrieves a user by HFID excluding a specific user ID (for unique validation during updates).
        /// </summary>
        public async Task<User?> GetUserByHFIDExcludingUserIdAsync(string hfid, int excludeUserId)
        {
            try
            {
                return await _context.Users
                    .FirstOrDefaultAsync(u =>
                        u.HfId == hfid &&
                        u.Id != excludeUserId &&
                        u.DeletedBy == 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user by HFID excluding user ID: {HFID}, {UserId}", hfid, excludeUserId);
                throw;
            }
        }

        /// <summary>
        /// Retrieves a user by email excluding a specific user ID (for unique validation during updates).
        /// </summary>
        public async Task<User?> GetUserByEmailExcludingUserIdAsync(string email, int excludeUserId)
        {
            try
            {
                return await _context.Users
                    .FirstOrDefaultAsync(u =>
                        u.Email == email &&
                        u.Id != excludeUserId &&
                        u.DeletedBy == 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user by email excluding user ID: {Email}, {UserId}", email, excludeUserId);
                throw;
            }
        }

        /// <summary>
        /// Updates an existing user entity.
        /// </summary>
        public async Task UpdateUserAsync(User user)
        {
            try
            {
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation("User updated successfully. UserId: {UserId}", user.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user ID: {UserId}", user.Id);
                throw;
            }
        }
    }
}