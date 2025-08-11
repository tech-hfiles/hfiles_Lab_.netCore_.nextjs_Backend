using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HFiles_Backend.Infrastructure.Repositories
{
    public class ClinicMemberRepository(AppDbContext context) : IClinicMemberRepository
    {
        private readonly AppDbContext _context = context;

        //public async Task<ClinicMember?> GetMemberAsync(int userId, int clinicId, string role)
        //{
        //    return await _context.ClinicMembers
        //        .FirstOrDefaultAsync(m =>
        //            m.UserId == userId &&
        //            m.ClinicId == clinicId &&
        //            m.DeletedBy == 0 &&
        //            m.Role == role);
        //}
    }
}
