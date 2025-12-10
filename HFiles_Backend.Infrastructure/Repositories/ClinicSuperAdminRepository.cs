using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Entities.Users;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HFiles_Backend.Infrastructure.Repositories
{
    public class ClinicSuperAdminRepository(AppDbContext context) : IClinicSuperAdminRepository
    {
        private readonly AppDbContext _context = context;

        public async Task<User?> GetUserByHFIDAsync(string hfid)
        {
            return await _context.Users
                .Where(u => u.HfId == hfid)
                .FirstOrDefaultAsync();
        }

        public async Task AddAsync(ClinicSuperAdmin admin)
        {
            await _context.ClinicSuperAdmins.AddAsync(admin);
        }

        public async Task<ClinicSuperAdmin?> GetSuperAdminAsync(int userId, int clinicId, int? clinicReference)
        {
            return await _context.ClinicSuperAdmins
                .FirstOrDefaultAsync(a =>
                    a.UserId == userId &&
                    (a.ClinicId == clinicId || a.ClinicId == clinicReference) &&
                    a.IsMain == 1);
        }
        public async Task<Application.DTOs.Labs.User?> GetMainSuperAdminDtoAsync(int clinicId)
        {
            return await (
                from a in _context.ClinicSuperAdmins
                join u in _context.Users on a.UserId equals u.Id
                where a.ClinicId == clinicId && a.IsMain == 1
                select new Application.DTOs.Labs.User
                {
                    MemberId = a.Id,
                    HFID = u.HfId ?? string.Empty,
                    Name = $"{u.FirstName} {u.LastName}",
                    Email = u.Email ?? string.Empty,
                    Role = "Super Admin",
                    ProfilePhoto = string.IsNullOrEmpty(u.ProfilePhoto) ? "No image preview available" : u.ProfilePhoto
                }).FirstOrDefaultAsync();
        }

        public async Task<Dictionary<int, ClinicSuperAdmin>> GetAdminsByClinicIdAsync(int clinicId)
        {
            return await _context.ClinicSuperAdmins
                .Where(a => a.ClinicId == clinicId)
                .ToDictionaryAsync(a => a.Id);
        }
        public async Task<ClinicSuperAdmin?> GetByIdAsync(int id)
        {
            return await _context.ClinicSuperAdmins.FirstOrDefaultAsync(sa => sa.Id == id);
        }
        public async Task<ClinicSuperAdmin?> GetMainSuperAdminAsync(int clinicId)
        {
            return await _context.ClinicSuperAdmins
                .FirstOrDefaultAsync(a => a.IsMain == 1 && a.ClinicId == clinicId);
        }

        public async Task<ClinicSuperAdmin?> GetExistingSuperAdminAsync(int userId, int clinicId)
        {
            return await _context.ClinicSuperAdmins
                .FirstOrDefaultAsync(a => a.UserId == userId && a.ClinicId == clinicId && a.IsMain == 0);
        }

        public async Task<ClinicSuperAdmin?> GetMainSuperAdminAsync(int userId, int clinicId)
        {
            return await _context.ClinicSuperAdmins.FirstOrDefaultAsync(a => a.UserId == userId && a.IsMain == 1 && a.ClinicId == clinicId);
        }

        public void Update(ClinicSuperAdmin admin) => _context.ClinicSuperAdmins.Update(admin);
        public void Add(ClinicSuperAdmin admin) => _context.ClinicSuperAdmins.Add(admin);
        public async Task<ClinicSuperAdmin?> GetSuperAdminByIdAsync(int superAdminId)
        {
            return await _context.ClinicSuperAdmins
                .FirstOrDefaultAsync(sa => sa.Id == superAdminId && sa.IsMain == 1);
        }
    }
}
