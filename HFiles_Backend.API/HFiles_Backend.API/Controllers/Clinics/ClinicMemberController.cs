using HFiles_Backend.API.Interfaces;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Clinics.Member;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Security.Claims;

namespace HFiles_Backend.API.Controllers.Clinics
{
    [Route("api/")]
    [ApiController]
    public class ClinicMemberController(
         ILogger<ClinicMemberController> logger,
          IClinicRepository clinicRepository,
          IPasswordHasher<ClinicMember> passwordHasher,
          IClinicSuperAdminRepository clinicSuperAdminRepository,
          IClinicMemberRepository clinicMemberRepository,
          IClinicAuthorizationService clinicAuthorizationService,
          AppDbContext context
        ) : ControllerBase
    {
        private readonly ILogger<ClinicMemberController> _logger = logger;
        private readonly IClinicRepository _clinicRepository = clinicRepository;
        private readonly IPasswordHasher<ClinicMember> _passwordHasher = passwordHasher;
        private readonly IClinicSuperAdminRepository _clinicSuperAdminRepository = clinicSuperAdminRepository;
        private readonly IClinicMemberRepository _clinicMemberRepository = clinicMemberRepository;
        private readonly IClinicAuthorizationService _clinicAuthorizationService = clinicAuthorizationService;
        private readonly AppDbContext _context = context;


        private static async Task<string?> ResolveUsernameFromClaims(HttpContext httpContext, AppDbContext dbContext)
        {
            var role = httpContext.User.FindFirst(ClaimTypes.Role)?.Value;
            var adminIdStr = httpContext.User.FindFirst("ClinicAdminId")?.Value;

            if (!int.TryParse(adminIdStr, out var adminId)) return null;

            if (role == "Super Admin")
            {
                var superAdmin = await dbContext.ClinicSuperAdmins.FirstOrDefaultAsync(sa => sa.Id == adminId);
                if (superAdmin != null)
                {
                    var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == superAdmin.UserId && u.DeletedBy == 0);
                    return $"{user?.FirstName} {user?.LastName}".Trim();
                }
            }

            if (role == "Admin")
            {
                var member = await dbContext.ClinicMembers.FirstOrDefaultAsync(m => m.Id == adminId);
                if (member != null)
                {
                    var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == member.UserId && u.DeletedBy == 0);
                    return $"{user?.FirstName} {user?.LastName}".Trim();
                }
            }

            return null;
        }






        // Add Members
        [HttpPost("clinics/members")]
        [Authorize(Policy = "SuperAdminOrAdminPolicy")]
        public async Task<IActionResult> AddClinicMember([FromBody] CreateClinicMember dto)
        {
            HttpContext.Items["Log-Category"] = "User Management";
            _logger.LogInformation("Received request to create new clinic member. HFID: {HFID}, Branch ID: {BranchId}", dto.HFID, dto.BranchId);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            var transaction = await _clinicRepository.BeginTransactionAsync();

            try
            {
                var clinicIdClaim = User.FindFirst("UserId")?.Value;
                if (clinicIdClaim == null || !int.TryParse(clinicIdClaim, out int clinicId))
                    return Unauthorized(ApiResponseFactory.Fail("Invalid or missing ClinicId claim."));

                if (!await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User))
                    return Unauthorized(ApiResponseFactory.Fail("Permission denied. You can only manage your clinic or its branches."));

                var createdByClaim = User.FindFirst("ClinicAdminId")?.Value;
                if (createdByClaim == null || !int.TryParse(createdByClaim, out int createdBy))
                    return Unauthorized(ApiResponseFactory.Fail("Invalid or missing ClinicAdminId in token."));

                string? createdByName = await ResolveUsernameFromClaims(HttpContext, _context);
                if (string.IsNullOrWhiteSpace(createdByName))
                    return Unauthorized(ApiResponseFactory.Fail("Unable to resolve creator identity."));

                var userDetails = await _clinicSuperAdminRepository.GetUserByHFIDAsync(dto.HFID);
                if (userDetails == null)
                    return NotFound(ApiResponseFactory.Fail($"No user found with HFID {dto.HFID}."));

                var clinicEntry = await _clinicRepository.GetByIdAsync(dto.BranchId);
                if (clinicEntry == null)
                    return NotFound(ApiResponseFactory.Fail($"No clinic found with Branch ID {dto.BranchId}."));

                int mainClinicId = clinicEntry.ClinicReference == 0 ? clinicId : clinicEntry.ClinicReference;

                bool exists = await _clinicMemberRepository.MemberExistsAsync(userDetails.Id, dto.BranchId, "Member");
                if (exists)
                {
                    string fullName = $"{userDetails.FirstName} {userDetails.LastName}";
                    return BadRequest(ApiResponseFactory.Fail($"{fullName}'s HFID {dto.HFID} already exists as Member in Branch {dto.BranchId}."));
                }

                var superAdmin = await _clinicSuperAdminRepository.GetSuperAdminAsync(userDetails.Id, clinicId, clinicEntry.ClinicReference);
                if (superAdmin != null)
                    return BadRequest(ApiResponseFactory.Fail("User is already a registered Super Admin."));

                var newMember = new ClinicMember
                {
                    UserId = userDetails.Id,
                    ClinicId = clinicEntry.Id,
                    PasswordHash = _passwordHasher.HashPassword(null!, dto.Password),
                    CreatedBy = createdBy
                };

                await _clinicMemberRepository.AddAsync(newMember);
                await _clinicRepository.SaveChangesAsync();
                await transaction.CommitAsync();

                var fullNames = $"{userDetails.FirstName} {userDetails.LastName}".Trim();

                var responseData = new
                {
                    UserId = newMember.UserId,
                    Name = fullNames,
                    Email = userDetails.Email!,
                    ClinicId = newMember.ClinicId,
                    ClinicName = clinicEntry.ClinicName!,
                    CreatedBy = createdByName,
                    Role = newMember.Role,
                    EpochTime = newMember.EpochTime,
                    BranchClinicId = newMember.ClinicId != clinicId ? newMember.ClinicId : 0,
                    NotificationMessage = $"{fullNames} was successfully added by {createdByName}"
                };

                _logger.LogInformation("New clinic member created successfully. User ID: {UserId}, Clinic ID: {ClinicId}.", newMember.UserId, newMember.ClinicId);
                return Ok(ApiResponseFactory.Success(responseData, "Member added successfully."));
            }
            finally
            {
                if (transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.RollbackAsync();
                }
            }
        }
    }
}
