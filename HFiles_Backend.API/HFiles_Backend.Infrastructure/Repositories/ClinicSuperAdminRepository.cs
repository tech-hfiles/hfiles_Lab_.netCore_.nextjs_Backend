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
    }
}
