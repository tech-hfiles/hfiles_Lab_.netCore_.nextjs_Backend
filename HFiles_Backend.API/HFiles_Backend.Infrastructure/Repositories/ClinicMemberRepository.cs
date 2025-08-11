using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HFiles_Backend.Infrastructure.Repositories
{
    public class ClinicMemberRepository(AppDbContext context) : IClinicMemberRepository
    {
        private readonly AppDbContext _context = context;

        public async Task<ClinicMember?> GetMemberAsync(int userId, int clinicId, string role)
        {
            return await _context.ClinicMembers
                .FirstOrDefaultAsync(m =>
                    m.UserId == userId &&
                    m.ClinicId == clinicId &&
                    m.DeletedBy == 0 &&
                    m.Role == role);
        }

        public async Task<bool> MemberExistsAsync(int userId, int clinicId, string role)
        {
            return await _context.ClinicMembers
                .AnyAsync(m =>
                    m.UserId == userId &&
                    m.ClinicId == clinicId &&
                    m.Role == role &&
                    m.DeletedBy == 0);
        }

        public async Task AddAsync(ClinicMember member)
        {
            await _context.ClinicMembers.AddAsync(member);
        }

        public async Task<List<Application.DTOs.Labs.User>> GetMemberDtosByClinicIdAsync(int clinicId)
        {
            var members = await _context.ClinicMembers
                .Include(m => m.User)
                .Include(m => m.CreatedByUser)
                .Include(m => m.PromotedByUser)
                .Where(m => m.ClinicId == clinicId && m.DeletedBy == 0)
                .ToListAsync();

            var memberDtos = new List<Application.DTOs.Labs.User>();

            foreach (var m in members)
            {
                string createdByName = m.CreatedByUser != null
                    ? $"{m.CreatedByUser.FirstName} {m.CreatedByUser.LastName}".Trim()
                    : "Itself";

                string promotedByName = m.PromotedByUser != null
                    ? $"{m.PromotedByUser.FirstName} {m.PromotedByUser.LastName}".Trim()
                    : "Not Promoted Yet";

                memberDtos.Add(new Application.DTOs.Labs.User
                {
                    MemberId = m.Id,
                    HFID = m.User?.HfId ?? string.Empty,
                    Name = $"{m.User?.FirstName} {m.User?.LastName}".Trim(),
                    Email = m.User?.Email ?? string.Empty,
                    Role = m.Role,
                    CreatedByName = createdByName,
                    PromotedByName = promotedByName,
                    ProfilePhoto = string.IsNullOrEmpty(m.User?.ProfilePhoto)
                        ? "No image preview available"
                        : m.User.ProfilePhoto
                });
            }

            return memberDtos;
        }


        public async Task<Dictionary<int, ClinicMember>> GetAllMembersAsync()
        {
            return await _context.ClinicMembers.ToDictionaryAsync(m => m.Id);
        }
        public async Task<ClinicMember?> GetByIdInBranchesAsync(int memberId, List<int> branchIds)
        {
            return await _context.ClinicMembers
                .FirstOrDefaultAsync(m => m.Id == memberId && branchIds.Contains(m.ClinicId) && m.DeletedBy == 0);
        }

        public async Task UpdateAsync(ClinicMember member)
        {
            _context.ClinicMembers.Update(member);
            await _context.SaveChangesAsync();
        }
    }
}
