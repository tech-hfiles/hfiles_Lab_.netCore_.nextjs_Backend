using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HFiles_Backend.Infrastructure.Repositories
{
    public class ClinicRepository : IClinicRepository
    {
        private readonly AppDbContext _context;

        public ClinicRepository(AppDbContext context)
        {
            _context = context;
        }

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

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
