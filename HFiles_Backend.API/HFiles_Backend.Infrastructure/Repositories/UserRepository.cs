using HFiles_Backend.Domain.Entities.Users;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HFiles_Backend.Infrastructure.Repositories
{
    public class UserRepository(AppDbContext context) : IUserRepository
    {
        private readonly AppDbContext _context = context;

        public async Task<Dictionary<int, User>> GetAllUsersAsync()
        {
            return await _context.Users.ToDictionaryAsync(u => u.Id);
        }

        public async Task<User?> GetByIdAsync(int id)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<User?> GetVerifiedUserByEmailAsync(string email)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Email == email && u.IsEmailVerified && u.UserReference == 0);
        }
        public async Task<string?> GetFullNameBySuperAdminIdAsync(int superAdminId)
        {
            return await (from sa in _context.ClinicSuperAdmins
                          join u in _context.Users on sa.UserId equals u.Id
                          where sa.Id == superAdminId
                          select u.FirstName + " " + u.LastName)
                          .FirstOrDefaultAsync();
        }

        public async Task<User?> GetUserByHFIDAsync(string hfid)
        {
            return await _context.Users
                .Where(u => u.HfId == hfid && u.DeletedBy == 0)
                .FirstOrDefaultAsync();
        }

        public async Task<List<User>> GetAllAsync()
        {
            return await _context.Users.ToListAsync();
        }

        public async Task<User?> GetByPhoneNumberAsync(string phoneNumber)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber && u.UserReference == 0);
        }

        public async Task SaveAsync(UserReport report)
        {
            _context.UserReports.Add(report);
            await _context.SaveChangesAsync();
        }

        public async Task<User?> GetUserByPatientIdAsync(string patientId)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.PatientId == patientId && u.DeletedBy == 0);
        }

        public async Task<User?> GetUserByEmailForLookupAsync(string email)
        {
            return await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u =>
                    u.Email == email &&
                    u.DeletedBy == 0);
        }
    }
}
