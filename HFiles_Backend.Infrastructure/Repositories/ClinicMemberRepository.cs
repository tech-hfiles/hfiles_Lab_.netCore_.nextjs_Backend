using HFiles_Backend.Application.DTOs.Clinics.Member;
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

        //public async Task<List<Application.DTOs.Labs.User>> GetMemberDtosByClinicIdAsync(int clinicId)
        //{
        //    var members = await _context.ClinicMembers
        //        .Include(m => m.User)
        //        .Include(m => m.CreatedByUser)
        //        .Include(m => m.PromotedByUser)
        //        .Where(m => m.ClinicId == clinicId && m.DeletedBy == 0)
        //        .ToListAsync();

        //    var memberDtos = new List<Application.DTOs.Labs.User>();

        //    foreach (var m in members)
        //    {
        //        string createdByName = m.CreatedByUser != null
        //            ? $"{m.CreatedByUser.FirstName} {m.CreatedByUser.LastName}".Trim()
        //            : "Itself";

        //        string promotedByName = m.PromotedByUser != null
        //            ? $"{m.PromotedByUser.FirstName} {m.PromotedByUser.LastName}".Trim()
        //            : "Not Promoted Yet";

        //        memberDtos.Add(new Application.DTOs.Labs.User
        //        {
        //            MemberId = m.Id,
        //            HFID = m.User?.HfId ?? string.Empty,
        //            Name = $"{m.User?.FirstName} {m.User?.LastName}".Trim(),
        //            Email = m.User?.Email ?? string.Empty,
        //            Role = m.Role,
        //            Coach = m.Coach,
        //            CreatedByName = createdByName,
        //            PromotedByName = promotedByName,
        //            ProfilePhoto = string.IsNullOrEmpty(m.User?.ProfilePhoto)
        //                ? "No image preview available"
        //                : m.User.ProfilePhoto,
        //            Color = m.Color
        //        });
        //    }

        //    return memberDtos;
        //}

        public async Task<List<Application.DTOs.Labs.User>> GetMemberDtosByClinicIdAsync(int clinicId)
        {
            var members = await (
                from cm in _context.ClinicMembers
                where cm.ClinicId == clinicId && cm.DeletedBy == 0
                join user in _context.Users on cm.UserId equals user.Id

                // Left join for CreatedBy through ClinicSuperAdmin
                join createdBySA in _context.ClinicSuperAdmins on cm.CreatedBy equals createdBySA.Id into createdByJoin
                from createdBySA in createdByJoin.DefaultIfEmpty()
                join createdByUserViaSA in _context.Users on createdBySA.UserId equals createdByUserViaSA.Id into createdByUserViaSAJoin
                from createdByUserViaSA in createdByUserViaSAJoin.DefaultIfEmpty()

                    // FALLBACK: Left join CreatedBy directly to Users table (in case it's stored as UserId)
                join createdByUserDirect in _context.Users on cm.CreatedBy equals createdByUserDirect.Id into createdByDirectJoin
                from createdByUserDirect in createdByDirectJoin.DefaultIfEmpty()

                    // Left join for PromotedBy through ClinicSuperAdmin
                join promotedBySA in _context.ClinicSuperAdmins on cm.PromotedBy equals promotedBySA.Id into promotedByJoin
                from promotedBySA in promotedByJoin.DefaultIfEmpty()
                join promotedByUser in _context.Users on promotedBySA.UserId equals promotedByUser.Id into promotedByUserJoin
                from promotedByUser in promotedByUserJoin.DefaultIfEmpty()

                select new
                {
                    Member = cm,
                    User = user,
                    CreatedByUserViaSA = createdByUserViaSA,
                    CreatedByUserDirect = createdByUserDirect,
                    PromotedByUser = promotedByUser
                }
            ).ToListAsync();

            var memberDtos = new List<Application.DTOs.Labs.User>();

            foreach (var m in members)
            {
                // Handle CreatedBy: try via ClinicSuperAdmin first, then direct User lookup, then fallback
                string createdByName;
                if (m.CreatedByUserViaSA != null)
                {
                    // Found via ClinicSuperAdmin table (correct way)
                    createdByName = $"{m.CreatedByUserViaSA.FirstName} {m.CreatedByUserViaSA.LastName}".Trim();
                }
                else if (m.CreatedByUserDirect != null)
                {
                    // Found via direct User lookup (fallback for legacy/incorrect data)
                    createdByName = $"{m.CreatedByUserDirect.FirstName} {m.CreatedByUserDirect.LastName}".Trim();
                }
                else if (m.Member.CreatedBy == null || m.Member.CreatedBy == 0)
                {
                    createdByName = "Self Created";
                }
                else
                {
                    createdByName = "Unknown";
                }

                // Handle PromotedBy
                string promotedByName = m.PromotedByUser != null
                    ? $"{m.PromotedByUser.FirstName} {m.PromotedByUser.LastName}".Trim()
                    : "Not Promoted Yet";

                memberDtos.Add(new Application.DTOs.Labs.User
                {
                    MemberId = m.Member.Id,
                    HFID = m.User?.HfId ?? string.Empty,
                    Name = $"{m.User?.FirstName} {m.User?.LastName}".Trim(),
                    Email = m.User?.Email ?? string.Empty,
                    Role = m.Member.Role,
                    Coach = m.Member.Coach,
                    CreatedByName = createdByName,
                    PromotedByName = promotedByName,
                    ProfilePhoto = string.IsNullOrEmpty(m.User?.ProfilePhoto)
                        ? "No image preview available"
                        : m.User.ProfilePhoto,
                    Color = m.Member.Color
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


        public async Task<ClinicMember?> GetByIdAsync(int memberId)
        {
            return await _context.ClinicMembers
                .FirstOrDefaultAsync(m => m.Id == memberId && m.DeletedBy == 0);
        }


        public async Task<List<DeletedClinicMemberDto>> GetDeletedMembersWithDetailsAsync(int clinicId, List<int> branchIds)
        {
            var query = from m in _context.ClinicMembers
                        where m.ClinicId == clinicId && m.DeletedBy != 0
                        join ud in _context.Users on m.UserId equals ud.Id
                        // Left join to check if the same user exists in ClinicSuperAdmin table
                        join memberSuperAdmin in _context.ClinicSuperAdmins
                            on new { UserId = m.UserId, ClinicId = m.ClinicId }
                            equals new { UserId = memberSuperAdmin.UserId, ClinicId = memberSuperAdmin.ClinicId }
                            into superAdminGroup
                        from sa in superAdminGroup.DefaultIfEmpty()
                            // Include only if: 
                            // 1. User doesn't exist in super admin table (sa == null), OR
                            // 2. User exists in super admin table but is not main super admin (sa.IsMain == 0)
                        where sa == null || sa.IsMain == 0
                        select new DeletedClinicMemberDto
                        {
                            Id = m.Id,
                            UserId = m.UserId,
                            Name = $"{ud.FirstName} {ud.LastName}",
                            Email = ud.Email ?? "Email Not Found",
                            HFID = ud.HfId ?? "HFID Not Found",
                            ProfilePhoto = string.IsNullOrEmpty(ud.ProfilePhoto) ? "No image preview available" : ud.ProfilePhoto,
                            ClinicId = m.ClinicId,
                            Role = m.Role,
                            DeletedByUser = (
                                from superAdmin in _context.ClinicSuperAdmins
                                join sUser in _context.Users on superAdmin.UserId equals sUser.Id
                                where superAdmin.Id == m.DeletedBy && branchIds.Contains(m.ClinicId)
                                select $"{sUser.FirstName} {sUser.LastName}"
                            ).FirstOrDefault() ?? (
                                from cm in _context.ClinicMembers
                                join cUser in _context.Users on cm.UserId equals cUser.Id
                                where cm.Id == m.DeletedBy && branchIds.Contains(m.ClinicId)
                                select $"{cUser.FirstName} {cUser.LastName}"
                            ).FirstOrDefault() ?? "Name Not Found",
                            DeletedByUserRole = (
                                from superAdmin in _context.ClinicSuperAdmins
                                where superAdmin.Id == m.DeletedBy && branchIds.Contains(m.ClinicId)
                                select "Super Admin"
                            ).FirstOrDefault() ?? (
                                from cm in _context.ClinicMembers
                                where cm.Id == m.DeletedBy && branchIds.Contains(m.ClinicId)
                                select cm.Role
                            ).FirstOrDefault() ?? "Role Not Found"
                        };

            return await query.ToListAsync();
        }

        public async Task UpdateAsync(ClinicMember member)
        {
            _context.ClinicMembers.Update(member);
            await _context.SaveChangesAsync();
        }

        public void Update(ClinicMember member)
        {
            _context.ClinicMembers.Update(member);
        }

        public async Task<ClinicMember?> GetDeletedMemberByIdAsync(int memberId, int clinicId)
        {
            return await _context.ClinicMembers
                .FirstOrDefaultAsync(m => m.Id == memberId && m.ClinicId == clinicId && m.DeletedBy != 0);
        }

        public void Remove(ClinicMember member)
        {
            _context.ClinicMembers.Remove(member);
        }
        public async Task<ClinicMember?> GetEligibleMemberForPromotionAsync(int memberId, List<int> branchIds)
        {
            return await _context.ClinicMembers
                .FirstOrDefaultAsync(m => m.Id == memberId && m.DeletedBy == 0 && branchIds.Contains(m.ClinicId));
        }

        public async Task<ClinicMember?> GetDeletedMemberByUserIdAsync(int userId, List<int> branchIds)
        {
            return await _context.ClinicMembers
                .FirstOrDefaultAsync(m => m.UserId == userId && branchIds.Contains(m.ClinicId) && m.DeletedBy != 0);
        }

        public async Task<ClinicMember?> GetActiveMemberAsync(int userId, int clinicId)
        {
            return await _context.ClinicMembers.FirstOrDefaultAsync(m => m.UserId == userId && m.DeletedBy == 0 && m.ClinicId == clinicId);
        }

        public void Add(ClinicMember member) => _context.ClinicMembers.Add(member);

		public async Task<List<ClinicMember>> GetMembersByClinicIdAsync(int clinicId, string role)
		{
			return await _context.ClinicMembers
				.Where(m => m.ClinicId == clinicId && m.Role == role && m.DeletedBy == 0)
				.ToListAsync();
		}
	}
}
