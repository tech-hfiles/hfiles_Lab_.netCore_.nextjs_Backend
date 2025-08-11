using HFiles_Backend.Application.DTOs.Clinics.HFID;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;


namespace HFiles_Backend.Infrastructure.Repositories
{
    public class ClinicRepository(AppDbContext context) : IClinicRepository
    {
        private readonly AppDbContext _context = context;

        public async Task<IDbContextTransaction> BeginTransactionAsync()
        {
            return await _context.Database.BeginTransactionAsync();
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            return await _context.ClinicSignups
                .AsNoTracking()
                .AnyAsync(c => c.Email == email);
        }

        public async Task<ClinicSignup?> GetByEmailAsync(string email)
        {
            return await _context.ClinicSignups
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Email == email);
        }

        public async Task<ClinicSignup?> GetByPhoneAsync(string phoneNumber)
        {
            return await _context.ClinicSignups
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.PhoneNumber == phoneNumber);
        }

        public async Task<ClinicOtpEntry?> GetLatestOtpAsync(string emailOrPhone)
        {
            return await _context.ClinicOtpEntries
                .Where(o => o.Email == emailOrPhone)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<ClinicSignup?> GetByIdAndEmailAsync(int id, string email)
        {
            return await _context.ClinicSignups
                .Where(c => c.Id == id && c.Email == email)
                .FirstOrDefaultAsync();
        }

        public async Task<ClinicHFIDDto?> GetHFIDByEmailAsync(string email)
        {
            return await _context.ClinicSignups
                .Where(u => u.Email == email)
                .Select(u => new ClinicHFIDDto
                {
                    Email = u.Email,
                    ClinicName = u.ClinicName,
                    HFID = u.HFID
                })
                .FirstOrDefaultAsync();
        }
        public async Task<ClinicSignup?> GetByIdAsync(int id)
        {
            return await _context.ClinicSignups
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<List<ClinicOtpEntry>> GetExpiredOtpsAsync(string identifier, DateTime now)
        {
            return await _context.ClinicOtpEntries
                .Where(x => x.Email == identifier && x.ExpiryTime < now)
                .ToListAsync();
        }

        public async Task RemoveOtpsAsync(IEnumerable<ClinicOtpEntry> otpEntries)
        {
            _context.ClinicOtpEntries.RemoveRange(otpEntries);
            await _context.SaveChangesAsync();
        }

        public async Task AddSignupAsync(ClinicSignup signup)
        {
            await _context.ClinicSignups.AddAsync(signup);
        }

        public async Task RemoveOtpAsync(ClinicOtpEntry otpEntry)
        {
            _context.ClinicOtpEntries.Remove(otpEntry);
            await _context.SaveChangesAsync();
        }

        public async Task AddOtpAsync(ClinicOtpEntry entry)
        {
            await _context.ClinicOtpEntries.AddAsync(entry);
        }

        public async Task UpdateAsync(ClinicSignup clinic)
        {
            var existing = await _context.ClinicSignups.FindAsync(clinic.Id);
            if (existing == null) return;

            _context.Entry(existing).CurrentValues.SetValues(clinic);
            await _context.SaveChangesAsync();
        }
        public async Task<List<int>> GetBranchIdsAsync(int mainClinicId)
        {
            return await _context.ClinicSignups
                .Where(c => c.ClinicReference == mainClinicId)
                .Select(c => c.Id)
                .ToListAsync();
        }
        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
