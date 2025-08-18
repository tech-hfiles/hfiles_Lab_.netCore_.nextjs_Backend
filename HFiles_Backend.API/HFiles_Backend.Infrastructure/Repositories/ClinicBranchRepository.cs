using HFiles_Backend.Application.DTOs.Clinics.Branch;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Entities.Users;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HFiles_Backend.Infrastructure.Repositories
{
    public class ClinicBranchRepository(AppDbContext context) : IClinicBranchRepository
    {
        private readonly AppDbContext _context = context;

        public async Task<bool> IsEmailRegisteredAsync(string email)
        {
            return await _context.ClinicSignups.AnyAsync(c => c.Email == email);
        }

        public async Task<ClinicSignup?> GetParentClinicAsync(int clinicId)
        {
            return await _context.ClinicSignups
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == clinicId);
        }

        public void AddBranch(ClinicSignup branch)
        {
            _context.ClinicSignups.Add(branch);
        }

        public async Task<ClinicSignup?> GetClinicByIdAsync(int clinicId)
        {
            return await _context.ClinicSignups
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == clinicId);
        }

        public async Task<ClinicSignup?> GetMainClinicAsync(int mainClinicId)
        {
            return await _context.ClinicSignups
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == mainClinicId && c.DeletedBy == 0);
        }

        public async Task<List<ClinicSignup>> GetBranchesAsync(int mainClinicId)
        {
            return await _context.ClinicSignups
                .AsNoTracking()
                .Where(c => c.ClinicReference == mainClinicId && c.DeletedBy == 0)
                .ToListAsync();
        }

        public async Task<ClinicSuperAdmin?> GetSuperAdminByIdAsync(int adminId)
        {
            return await _context.ClinicSuperAdmins.FirstOrDefaultAsync(a => a.Id == adminId);
        }

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            return await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        }

        public void UpdateClinic(ClinicSignup clinic)
        {
            _context.ClinicSignups.Update(clinic);
        }

        public async Task<List<DeletedBranchDto>> GetDeletedBranchesAsync(int mainClinicId)
        {
            return await _context.ClinicSignups
                .AsNoTracking()
                .Where(c => c.ClinicReference == mainClinicId && c.DeletedBy != 0)
                .Select(c => new DeletedBranchDto
                {
                    Id = c.Id,
                    LabName = c.ClinicName,
                    Email = c.Email,
                    HFID = c.HFID,
                    ProfilePhoto = c.ProfilePhoto,
                    DeletedBy = c.DeletedBy
                })
                .ToListAsync();
        }
        public async Task<ClinicSignup?> GetClinicByUserIdAsync(int userId)
        {
            var clinicId = await _context.ClinicSuperAdmins
                .Where(sa => sa.UserId == userId)
                .Select(sa => sa.ClinicId)
                .FirstOrDefaultAsync();

            if (clinicId == 0)
                return null;

            return await _context.ClinicSignups
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == clinicId);
        }

        public async Task<List<int>> GetBranchIdsForMainClinicAsync(int mainClinicId)
        {
            return await _context.ClinicSignups
                .AsNoTracking()
                .Where(c => c.ClinicReference == mainClinicId)
                .Select(c => c.Id)
                .ToListAsync();
        }

        public async Task<ClinicSignup?> GetDeletedBranchByIdAsync(int branchId, List<int> validBranchIds)
        {
            return await _context.ClinicSignups
                .FirstOrDefaultAsync(c =>
                    c.Id == branchId &&
                    validBranchIds.Contains(c.Id) &&
                    c.DeletedBy != 0);
        }

        public async Task UpdateBranchAsync(ClinicSignup branch)
        {
            _context.ClinicSignups.Update(branch);
            await _context.SaveChangesAsync();
        }
    }
}
