using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HFiles_Backend.API.DTOs.Clinics;
using HFiles_Backend.API.Interfaces;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Clinics.Member;
using HFiles_Backend.Application.DTOs.Labs;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Enums;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Data;
using HFiles_Backend.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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
          IUserRepository userRepository,
          AppDbContext context
        ) : ControllerBase
    {
        private readonly ILogger<ClinicMemberController> _logger = logger;
        private readonly IClinicRepository _clinicRepository = clinicRepository;
        private readonly IPasswordHasher<ClinicMember> _passwordHasher = passwordHasher;
        private readonly IClinicSuperAdminRepository _clinicSuperAdminRepository = clinicSuperAdminRepository;
        private readonly IClinicMemberRepository _clinicMemberRepository = clinicMemberRepository;
        private readonly IClinicAuthorizationService _clinicAuthorizationService = clinicAuthorizationService;
        private readonly IUserRepository _userRepository = userRepository;
        private readonly AppDbContext _context = context;


        // Method to fetch username from token
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

        // Method to generate notification message for promotion member to admin API
        private static string GenerateNotificationMessage(List<string> names, string promoter)
        {
            if (names == null || names.Count == 0)
                return "No members promoted.";

            if (names.Count == 1)
                return $"{names[0]} was promoted to Admin by {promoter}.";

            string joinedNames = string.Join(", ", names);
            return $"{joinedNames} were promoted to Admin by {promoter}.";
        }
		// Get Members by Clinic ID
		[HttpGet("clinics/{clinicId}/members")]
		[Authorize(Policy = "SuperAdminOrAdminPolicy")]
		public async Task<IActionResult> GetClinicMembers([FromRoute][Range(1, int.MaxValue)] int clinicId)
		{
			HttpContext.Items["Log-Category"] = "User Management";
			_logger.LogInformation("Fetching members for Clinic ID: {ClinicId}", clinicId);

			var userClinicIdStr = User.FindFirst("UserId")?.Value;
			if (!int.TryParse(userClinicIdStr, out int userClinicId))
			{
				_logger.LogWarning("Member fetch failed: Invalid or missing ClinicId claim.");
				return Unauthorized(ApiResponseFactory.Fail("Invalid or missing ClinicId claim."));
			}

			if (!await _clinicAuthorizationService.IsClinicAuthorized(userClinicId, User))
			{
				_logger.LogWarning("Unauthorized access attempt for Clinic ID {ClinicId}", userClinicId);
				return Unauthorized(ApiResponseFactory.Fail("Permission denied. You can only manage your main clinic or its branches."));
			}

			try
			{
				var loggedInClinic = await _clinicRepository.GetByIdAsync(userClinicId);
				if (loggedInClinic == null)
				{
					_logger.LogWarning("Member fetch failed: Clinic ID {ClinicId} not found.", userClinicId);
					return BadRequest(ApiResponseFactory.Fail("Clinic not found"));
				}

				int mainClinicId = loggedInClinic.ClinicReference == 0 ? userClinicId : loggedInClinic.ClinicReference;
				var branchIds = await _clinicRepository.GetBranchIdsAsync(mainClinicId);
				branchIds.Add(mainClinicId);

				// Verify the requested clinicId is within authorized branches
				if (!branchIds.Contains(clinicId))
				{
					_logger.LogWarning("Clinic ID {ClinicId} is not within authorized branches", clinicId);
					return Unauthorized(ApiResponseFactory.Fail("You can only view members from your clinic or its branches."));
				}

				// Get members with role "Member" for the specific clinic
				var members = await _clinicMemberRepository.GetMembersByClinicIdAsync(clinicId, "Member");

				if (!members.Any())
				{
					_logger.LogInformation("No members found for Clinic ID {ClinicId}.", clinicId);
					return Ok(ApiResponseFactory.Success(new { Members = new List<object>() }, "No members found for this clinic."));
				}

				var memberList = new List<object>();
				foreach (var member in members)
				{
					var user = await _userRepository.GetByIdAsync(member.UserId);
					if (user != null && user.DeletedBy == 0)
					{
						memberList.Add(new
						{
							MemberId = member.Id,
							UserId = member.UserId,
							Name = $"{user.FirstName} {user.LastName}".Trim(),
							Email = user.Email,
							Role = member.Role,
							ClinicId = member.ClinicId,
							Coach = member.Coach,
							CreatedAt = member.EpochTime
						});
					}
				}

				_logger.LogInformation("Members fetched: Clinic ID {ClinicId}, Count = {Count}", clinicId, memberList.Count);
				return Ok(ApiResponseFactory.Success(new { Members = memberList }, "Members fetched successfully."));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error fetching members for Clinic ID {ClinicId}", clinicId);
				return StatusCode(500, ApiResponseFactory.Fail("An error occurred while fetching members."));
			}
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
                if (createdByClaim == null || !int.TryParse(createdByClaim, out int createdByAdminId))
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

                // Check if the new user is already a Super Admin
                var existingSuperAdmin = await _clinicSuperAdminRepository.GetSuperAdminAsync(userDetails.Id, clinicId, clinicEntry.ClinicReference);
                if (existingSuperAdmin != null)
                    return BadRequest(ApiResponseFactory.Fail("User is already a registered Super Admin."));

                // Get the creator's SuperAdmin record by their ClinicAdminId (which is the Id in SuperAdmin table)
                var creatorSuperAdmin = await _clinicSuperAdminRepository.GetSuperAdminByIdAsync(createdByAdminId);
                if (creatorSuperAdmin == null)
                    return Unauthorized(ApiResponseFactory.Fail("Creator Super Admin record not found."));

                // Use the UserId from the creator's SuperAdmin record
                var newMember = new ClinicMember
                {
                    UserId = userDetails.Id,
                    ClinicId = clinicEntry.Id,
                    PasswordHash = _passwordHasher.HashPassword(null!, dto.Password),
                    CreatedBy = creatorSuperAdmin.UserId,
                    Coach = dto.Coach,
                    Color = dto.Color ?? "rgba(0, 0, 0, 1)",
                    DeletedBy = 0
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
                    Coach = newMember.Coach,
                    Color = newMember.Color,
                    EpochTime = newMember.EpochTime,
                    BranchClinicId = newMember.ClinicId != clinicId ? newMember.ClinicId : 0,
                    NotificationMessage = $"{fullNames} was successfully added by {createdByName}"
                };

                _logger.LogInformation("New clinic member created successfully. User ID: {UserId}, Clinic ID: {ClinicId}.", newMember.UserId, newMember.ClinicId);
                return Ok(ApiResponseFactory.Success(responseData, "Member added successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating clinic member for HFID {HFID}", dto.HFID);
                await transaction.RollbackAsync();
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while creating the clinic member."));
            }
            finally
            {
                if (transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.DisposeAsync();
                }
            }
        }





        // Promotes Member to Admin
        [HttpPost("clinics/members/promote")]
        [Authorize(Policy = "SuperAdminOrAdminPolicy")]
        public async Task<IActionResult> PromoteClinicMembers(
        [FromBody] PromoteMembersRequest dto)
        {
            HttpContext.Items["Log-Category"] = "Role Management";
            _logger.LogInformation("Promote request received for members: {Ids}", dto.Ids);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            var clinicIdStr = User.FindFirst("UserId")?.Value;
            var promoterIdStr = User.FindFirst("ClinicAdminId")?.Value;
            var promoterRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";

            if (!int.TryParse(clinicIdStr, out var clinicId) || !int.TryParse(promoterIdStr, out var promoterId))
                return Unauthorized(ApiResponseFactory.Fail("Missing or invalid clinic/user claims."));

            if (!await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User))
                return Unauthorized(ApiResponseFactory.Fail("Permission denied to promote members."));

            var promoterUser = await _userRepository.GetByIdAsync(promoterId);
            var promoterName = $"{promoterUser?.FirstName} {promoterUser?.LastName}".Trim();

            var clinicEntry = await _clinicRepository.GetByIdAsync(clinicId);
            if (clinicEntry == null)
                return NotFound(ApiResponseFactory.Fail("Main clinic not found."));

            int mainClinicId = clinicEntry.ClinicReference == 0 ? clinicId : clinicEntry.ClinicReference;

            var branchIds = await _clinicRepository.GetBranchIdsAsync(mainClinicId);
            branchIds.Add(mainClinicId);

            var promoteResults = new List<object>();
            var memberNames = new List<string>();
            int? branchIdForAudit = null;

            var transaction = await _clinicRepository.BeginTransactionAsync();

            try
            {
                foreach (var memberId in dto.Ids)
                {
                    var member = await _clinicMemberRepository.GetByIdInBranchesAsync(memberId, branchIds);
                    if (member == null)
                    {
                        promoteResults.Add(new { Id = memberId, Status = "Failed", Reason = "Member not found" });
                        continue;
                    }

                    if (member.Role == "Admin")
                    {
                        promoteResults.Add(new { Id = member.Id, Status = "Skipped", Reason = "Already an Admin" });
                        continue;
                    }

                    // Get the creator's SuperAdmin record by their ClinicAdminId (which is the Id in SuperAdmin table)
                    var creatorSuperAdmin = await _clinicSuperAdminRepository.GetSuperAdminByIdAsync(promoterId);
                    if (creatorSuperAdmin == null)
                        return Unauthorized(ApiResponseFactory.Fail("Creator Super Admin record not found."));

                    member.Role = "Admin";
                    member.PromotedBy = creatorSuperAdmin.UserId;
                    await _clinicMemberRepository.UpdateAsync(member);

                    var memberUser = await _userRepository.GetByIdAsync(member.UserId);
                    var memberName = $"{memberUser?.FirstName} {memberUser?.LastName}".Trim();
                    memberNames.Add(memberName);

                    if (member.ClinicId != clinicId)
                        branchIdForAudit = member.ClinicId;

                   

                    promoteResults.Add(new
                    {
                        member.Id,
                        Status = "Success",
                        NewRole = "Admin",
                        PromotedBy = creatorSuperAdmin.UserId,
                        PromotedByName = promoterName,
                        MemberName = memberName,
                        BranchClinicId = branchIdForAudit
                    });
                }

                await _clinicRepository.SaveChangesAsync();
                await transaction.CommitAsync();

                if (!promoteResults.Any(r => ((dynamic)r).Status == "Success"))
                    return BadRequest(ApiResponseFactory.Fail(promoteResults.Select(r => ((dynamic)r).Reason).ToList()));

                string? createdByName = await ResolveUsernameFromClaims(HttpContext, _context);
                if (string.IsNullOrWhiteSpace(createdByName))
                    return Unauthorized(ApiResponseFactory.Fail("Unable to resolve creator identity."));

                var response = new
                {
                    PromotedBy = promoterName,
                    PromotedById = promoterId,
                    Members = promoteResults,
                    BranchClinicId = branchIdForAudit,
                    PromotedNames = memberNames,
                    NotificationContext = new
                    {
                        PromotedNames = memberNames,
                        PromoterName = createdByName
                    },
                    NotificationMessage = GenerateNotificationMessage(memberNames, createdByName)
                };

                return Ok(ApiResponseFactory.Success(response, "Member(s) promoted successfully."));
            }
            finally
            {
                if (transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.RollbackAsync();
                }
            }
        }





        // Soft Delete a Member or Admin
        [HttpPut("clinics/members/{memberId}")]
        [Authorize(Policy = "SuperAdminOrAdminPolicy")]
        public async Task<IActionResult> DeleteClinicMember(
        [FromRoute][Range(1, int.MaxValue)] int memberId)
        {
            HttpContext.Items["Log-Category"] = "User Management";
            HttpContext.Items["MemberId"] = memberId;

            _logger.LogInformation("Request received to delete clinic member ID: {MemberId}", memberId);

            var clinicIdStr = User.FindFirst("UserId")?.Value;
            var deletedByStr = User.FindFirst("ClinicAdminId")?.Value;
            var deletedByRole = User.FindFirst(ClaimTypes.Role)?.Value;

            if (!int.TryParse(clinicIdStr, out var clinicId) || !int.TryParse(deletedByStr, out var deletedById))
                return Unauthorized(ApiResponseFactory.Fail("Missing or invalid user claims."));

            if (!await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User))
                return Unauthorized(ApiResponseFactory.Fail("You are not authorized to manage this clinic."));

            var transaction = await _clinicRepository.BeginTransactionAsync();

            try
            {
                var clinic = await _clinicRepository.GetByIdAsync(clinicId);
                if (clinic == null)
                    return NotFound(ApiResponseFactory.Fail("Clinic not found."));

                int mainClinicId = clinic.ClinicReference == 0 ? clinicId : clinic.ClinicReference;

                var branchIds = await _clinicRepository.GetBranchIdsAsync(mainClinicId);
                branchIds.Add(mainClinicId);

                var member = await _clinicMemberRepository.GetByIdInBranchesAsync(memberId, branchIds);
                if (member == null)
                    return NotFound(ApiResponseFactory.Fail("Clinic member not found."));

                var memberUser = await _userRepository.GetByIdAsync(member.UserId);
                var deletedByUser = await _userRepository.GetByIdAsync(deletedById);

                string memberName = $"{memberUser?.FirstName} {memberUser?.LastName}".Trim();
                string? deletedByName = await ResolveUsernameFromClaims(HttpContext, _context);
                if (string.IsNullOrWhiteSpace(deletedByName))
                    return Unauthorized(ApiResponseFactory.Fail("Unable to resolve creator identity."));

                member.DeletedBy = deletedById;
                _clinicMemberRepository.Update(member);

                await _clinicRepository.SaveChangesAsync();
                await transaction.CommitAsync();

                int? branchClinicId = member.ClinicId != clinicId ? member.ClinicId : null;

                var response = new
                {
                    MemberId = member.Id,
                    DeletedBy = deletedById,
                    DeletedByRole = deletedByRole,
                    MemberName = memberName,
                    DeletedByName = deletedByName,
                    BranchClinicId = branchClinicId,
                    NotificationContext = new
                    {
                        MemberName = memberName,
                        DeletedByName = deletedByName,
                        Role = member.Role
                    },
                    NotificationMessage = $"{member.Role} {memberName} was deleted by {deletedByName}."
                };

                _logger.LogInformation("Successfully deleted clinic member ID: {MemberId} by {DeletedByName} ({Role})", member.Id, deletedByName, deletedByRole);
                return Ok(ApiResponseFactory.Success(response, $"{memberName} deleted successfully."));
            }
            finally
            {
                if (transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.RollbackAsync();
                }
            }
        }





        // Get Soft Deleted Members or Admins
        [HttpGet("clinics/{clinicId}/deleted-users")]
        [Authorize]
        public async Task<IActionResult> GetDeletedClinicUsers([FromRoute][Range(1, int.MaxValue)] int clinicId, [FromServices] ClinicMemberRepository clinicMemberRepository)
        {
            HttpContext.Items["Log-Category"] = "User Management";
            _logger.LogInformation("Fetching deleted users for Clinic ID: {ClinicId}", clinicId);

            var userClinicIdStr = User.FindFirst("UserId")?.Value;
            if (!int.TryParse(userClinicIdStr, out int userClinicId))
            {
                _logger.LogWarning("Deleted user fetch failed: Invalid or missing ClinicId claim.");
                return Unauthorized(ApiResponseFactory.Fail("Invalid or missing ClinicId claim."));
            }

            if (!await _clinicAuthorizationService.IsClinicAuthorized(userClinicId, User))
            {
                _logger.LogWarning("Unauthorized access attempt for Clinic ID {ClinicId}", userClinicId);
                return Unauthorized(ApiResponseFactory.Fail("Permission denied. You can only manage your main clinic or its branches."));
            }

            var transaction = await _clinicRepository.BeginTransactionAsync();

            try
            {
                var loggedInClinic = await _clinicRepository.GetByIdAsync(userClinicId);
                if (loggedInClinic == null)
                {
                    _logger.LogWarning("Deleted user fetch failed: Clinic ID {ClinicId} not found.", userClinicId);
                    return BadRequest(ApiResponseFactory.Fail("Clinic not found"));
                }

                int mainClinicId = loggedInClinic.ClinicReference == 0 ? userClinicId : loggedInClinic.ClinicReference;
                var branchIds = await _clinicRepository.GetBranchIdsAsync(mainClinicId);
                branchIds.Add(mainClinicId);

                var deletedMembers = await clinicMemberRepository.GetDeletedMembersWithDetailsAsync(clinicId, branchIds);
                if (!deletedMembers.Any())
                {
                    _logger.LogWarning("No deleted users found for Clinic ID {ClinicId}.", clinicId);
                    return Ok(ApiResponseFactory.Fail("No deleted users found for this clinic."));
                }

                await transaction.CommitAsync();

                _logger.LogInformation("Deleted users fetched: Clinic ID {ClinicId}, Count = {Count}", clinicId, deletedMembers.Count);
                return Ok(ApiResponseFactory.Success(new { DeletedMembers = deletedMembers }, "Deleted users fetched successfully."));
            }
            finally
            {
                if (transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.RollbackAsync();
                }
            }
        }





        // Revert Soft Deleted Members or Admins
        [HttpPatch("clinics/revert-user")]
        [Authorize(Policy = "SuperAdminOrAdminPolicy")]
        public async Task<IActionResult> RevertDeletedClinicUser([FromBody] RevertClinicUser dto, [FromServices] ClinicRepository clinicRepository)
        {
            HttpContext.Items["Log-Category"] = "User Management";

            _logger.LogInformation("Reverting clinic user. User ID: {UserId}, Clinic ID: {ClinicId}, New Role: {Role}", dto.Id, dto.ClinicId, dto.Role);

            var clinicIdClaim = User.FindFirst("UserId")?.Value;
            var clinicAdminIdClaim = User.FindFirst("ClinicAdminId")?.Value;
            var revertedByRoleClaim = User.FindFirst(ClaimTypes.Role)?.Value;

            if (!int.TryParse(clinicIdClaim, out int requestClinicId) || !int.TryParse(clinicAdminIdClaim, out int revertedById))
                return Unauthorized(ApiResponseFactory.Fail("Invalid or missing ClinicId/ClinicAdminId claim."));

            if (!await _clinicRepository.IsClinicAuthorizedAsync(requestClinicId, User))
                return Unauthorized(ApiResponseFactory.Fail("Permission denied. You can only manage your main clinic or its branches."));

            var transaction = await _clinicRepository.BeginTransactionAsync();

            try
            {
                var loggedInClinic = await _clinicRepository.GetClinicByIdAsync(requestClinicId);
                if (loggedInClinic == null)
                    return BadRequest(ApiResponseFactory.Fail("Clinic not found."));

                int mainClinicId = loggedInClinic.ClinicReference == 0 ? requestClinicId : loggedInClinic.ClinicReference;
                var branchIds = await _clinicRepository.GetBranchClinicIdsAsync(mainClinicId);
                branchIds.Add(mainClinicId);

                var user = await _clinicRepository.GetDeletedMemberAsync(dto.Id, dto.ClinicId);
                if (user == null)
                    return NotFound(ApiResponseFactory.Fail("User not found or not marked as deleted."));

                var clinicSuperAdmin = await _clinicRepository.GetSuperAdminByIdAsync(revertedById);
                var revertedByUser = await _clinicRepository.GetUserByIdAsync(clinicSuperAdmin?.UserId ?? 0);
                var reinstatedUser = await _clinicRepository.GetUserByIdAsync(user.UserId);

                string? revertedByName = await clinicRepository.ResolveUsernameFromClaimsAsync(HttpContext);
                if (string.IsNullOrWhiteSpace(revertedByName))
                    return Unauthorized(ApiResponseFactory.Fail("Unable to resolve creator identity."));

                string reinstatedUserName = $"{reinstatedUser?.FirstName} {reinstatedUser?.LastName}".Trim();

                user.DeletedBy = 0;
                user.Role = dto.Role ?? "Member";
                user.PromotedBy = clinicSuperAdmin?.Id;

                _clinicRepository.UpdateClinicMember(user);
                await _clinicRepository.SaveChangesAsync();
                await transaction.CommitAsync();

                int? branchId = user.ClinicId != requestClinicId ? user.ClinicId : null;

                var response = new
                {
                    UserId = user.Id,
                    user.ClinicId,
                    BranchClinicId = branchId,
                    NewRole = user.Role,
                    user.PromotedBy,
                    RevertedBy = revertedByName,
                    RevertedByRole = revertedByRoleClaim,
                    NotificationContext = new
                    {
                        ReinstatedName = reinstatedUserName,
                        RevertedByName = revertedByName
                    },
                    NotificationMessage = $"{user.Role} {reinstatedUserName} was reinstated by {revertedByName}."
                };

                _logger.LogInformation("Reverted clinic user ID {UserId} with new role {NewRole} by {RevertedBy}", user.Id, user.Role, revertedByName);
                return Ok(ApiResponseFactory.Success(response, $"{reinstatedUserName} reverted successfully."));
            }
            finally
            {
                if (transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.RollbackAsync();
                }
            }
        }


        // Update Member Details
        [HttpPatch("clinics/members/update")]
        [Authorize(Policy = "SuperAdminOrAdminPolicy")]
        public async Task<IActionResult> UpdateClinicMemberDetails([FromBody] UpdateClinicMemberDetails dto)
        {
            HttpContext.Items["Log-Category"] = "User Management";
            _logger.LogInformation("Request received to update clinic member ID: {MemberId}", dto.MemberId);

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

                var updatedByClaim = User.FindFirst("ClinicAdminId")?.Value;
                if (updatedByClaim == null || !int.TryParse(updatedByClaim, out int updatedByAdminId))
                    return Unauthorized(ApiResponseFactory.Fail("Invalid or missing ClinicAdminId in token."));

                string? updatedByName = await ResolveUsernameFromClaims(HttpContext, _context);
                if (string.IsNullOrWhiteSpace(updatedByName))
                    return Unauthorized(ApiResponseFactory.Fail("Unable to resolve updater identity."));

                // First, get the member to find which clinic they belong to
                var member = await _clinicMemberRepository.GetByIdAsync(dto.MemberId);
                if (member == null)
                    return NotFound(ApiResponseFactory.Fail($"Member with ID {dto.MemberId} not found."));

                // Now check authorization using the member's clinic
                var memberClinic = await _clinicRepository.GetByIdAsync(member.ClinicId);
                if (memberClinic == null)
                    return NotFound(ApiResponseFactory.Fail("Member's clinic not found."));

                // Get the logged-in user's clinic
                var userClinic = await _clinicRepository.GetByIdAsync(clinicId);
                if (userClinic == null)
                    return NotFound(ApiResponseFactory.Fail("Your clinic not found."));

                // Determine main clinic IDs for both
                int memberMainClinicId = memberClinic.ClinicReference == 0 ? member.ClinicId : memberClinic.ClinicReference;
                int userMainClinicId = userClinic.ClinicReference == 0 ? clinicId : userClinic.ClinicReference;

                // Check if they belong to the same clinic family
                if (memberMainClinicId != userMainClinicId)
                    return Unauthorized(ApiResponseFactory.Fail("You can only update members from your clinic or its branches."));

                // Check if member is deleted
                if (member.DeletedBy != 0)
                    return BadRequest(ApiResponseFactory.Fail("Cannot update a deleted member."));

                // Get member user details
                var memberUser = await _userRepository.GetByIdAsync(member.UserId);
                if (memberUser == null)
                    return NotFound(ApiResponseFactory.Fail("Member user details not found."));

                // Track what was updated
                var updatedFields = new List<string>();

                // Validate and update HFID if provided
                if (!string.IsNullOrWhiteSpace(dto.HFID) && memberUser.HfId != dto.HFID)
                {
                    var existingUserWithHFID = await _userRepository.GetUserByHFIDExcludingUserIdAsync(dto.HFID, memberUser.Id);
                    if (existingUserWithHFID != null)
                        return BadRequest(ApiResponseFactory.Fail($"HFID {dto.HFID} is already in use by another user."));

                    memberUser.HfId = dto.HFID;
                    updatedFields.Add("HFID");
                }

                // Validate and update Email if provided
                if (!string.IsNullOrWhiteSpace(dto.Email) && memberUser.Email != dto.Email)
                {
                    var existingUserWithEmail = await _userRepository.GetUserByEmailExcludingUserIdAsync(dto.Email, memberUser.Id);
                    if (existingUserWithEmail != null)
                        return BadRequest(ApiResponseFactory.Fail($"Email {dto.Email} is already in use by another user."));

                    memberUser.Email = dto.Email;
                    updatedFields.Add("Email");
                }

                // Update First Name if provided
                if (!string.IsNullOrWhiteSpace(dto.FirstName) && memberUser.FirstName != dto.FirstName)
                {
                    memberUser.FirstName = dto.FirstName;
                    updatedFields.Add("First Name");
                }

                // Update Last Name if provided
                if (!string.IsNullOrWhiteSpace(dto.LastName) && memberUser.LastName != dto.LastName)
                {
                    memberUser.LastName = dto.LastName;
                    updatedFields.Add("Last Name");
                }

                // Update Coach if provided
                if (dto.Coach != null && member.Coach != dto.Coach)
                {
                    member.Coach = dto.Coach;
                    updatedFields.Add("Coach");
                }

                //Update Color if provided
                if (!string.IsNullOrWhiteSpace(dto.Color) && member.Color != dto.Color)
                {
                    member.Color = dto.Color;
                    updatedFields.Add("Color");
                }

                // Update Password if provided
                if (!string.IsNullOrWhiteSpace(dto.Password))
                {
                    member.PasswordHash = _passwordHasher.HashPassword(member, dto.Password);
                    updatedFields.Add("Password");
                }

                // Check if anything was actually updated
                if (updatedFields.Count == 0)
                {
                    return BadRequest(ApiResponseFactory.Fail("No fields were updated. Please provide values to update."));
                }

                // Save changes to both User and ClinicMember
                if (updatedFields.Any(f => f != "Coach" && f != "Password" && f != "Color"))
                {
                    await _userRepository.UpdateUserAsync(memberUser);
                }

                if (updatedFields.Any(f => f == "Coach" || f == "Password" || f == "Color"))
                {
                    await _clinicMemberRepository.UpdateAsync(member);
                }

                await _clinicRepository.SaveChangesAsync();
                await transaction.CommitAsync();

                int? branchClinicId = member.ClinicId != clinicId ? member.ClinicId : null;
                string memberName = $"{memberUser.FirstName} {memberUser.LastName}".Trim();

                var response = new
                {
                    MemberId = member.Id,
                    UserId = member.UserId,
                    Name = memberName,
                    Email = memberUser.Email,
                    HFID = memberUser.HfId,
                    ClinicId = member.ClinicId,
                    BranchClinicId = branchClinicId,
                    Role = member.Role,
                    Coach = member.Coach,
                    Color = member.Color,
                    UpdatedBy = updatedByName,
                    UpdatedFields = updatedFields,
                    EpochTime = member.EpochTime,
                    NotificationMessage = $"{member.Role} {memberName} was updated by {updatedByName}. Updated: {string.Join(", ", updatedFields)}."
                };

                _logger.LogInformation("Successfully updated clinic member ID: {MemberId}. Updated fields: {Fields}",
                    member.Id, string.Join(", ", updatedFields));

                return Ok(ApiResponseFactory.Success(response, $"{memberName} updated successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating clinic member ID: {MemberId}", dto.MemberId);
                await transaction.RollbackAsync();
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while updating the clinic member."));
            }
            finally
            {
                if (transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.DisposeAsync();
                }
            }
        }




        // Permanently Deletes User
        [HttpDelete("clinics/remove-user")]
        [Authorize(Policy = "SuperAdminPolicy")]
        public async Task<IActionResult> PermanentlyDeleteClinicUser([FromBody] PermanentlyDeleteUser dto)
        {
            HttpContext.Items["Log-Category"] = "User Management";
            _logger.LogInformation("Request to permanently delete user. User ID: {UserId}, Clinic ID: {ClinicId}", dto.Id, dto.ClinicId);

            var clinicIdClaim = User.FindFirst("UserId")?.Value;
            if (!int.TryParse(clinicIdClaim, out int requestClinicId))
                return Unauthorized(ApiResponseFactory.Fail("Invalid or missing ClinicId claim."));

            if (!await _clinicAuthorizationService.IsClinicAuthorized(requestClinicId, User))
                return Unauthorized(ApiResponseFactory.Fail("Unauthorized to manage this clinic or its branches."));

            var deletedByIdClaim = User.FindFirst("ClinicAdminId")?.Value;
            var deletedByRoleClaim = User.FindFirst(ClaimTypes.Role)?.Value;

            if (!int.TryParse(deletedByIdClaim, out int deletedById))
                return Unauthorized(ApiResponseFactory.Fail("Invalid or missing ClinicAdminId claim."));

            var transaction = await _clinicRepository.BeginTransactionAsync();

            try
            {
                var superAdmin = await _clinicSuperAdminRepository.GetByIdAsync(deletedById);
                if (superAdmin == null)
                    return BadRequest(ApiResponseFactory.Fail("No Super Admin found."));

                var member = await _clinicMemberRepository.GetDeletedMemberByIdAsync(dto.Id, dto.ClinicId);
                if (member == null)
                    return NotFound(ApiResponseFactory.Fail("User not found."));

                var deletedByUser = await _userRepository.GetByIdAsync(superAdmin.UserId);
                if (deletedByUser == null)
                    return BadRequest(ApiResponseFactory.Fail("User details for Super Admin not found."));

                string? deletedBy = await ResolveUsernameFromClaims(HttpContext, _context);
                if (string.IsNullOrWhiteSpace(deletedBy))
                    return Unauthorized(ApiResponseFactory.Fail("Unable to resolve creator identity."));

                var userBeingDeleted = await _userRepository.GetByIdAsync(member.UserId);
                string deletedUserName = $"{userBeingDeleted?.FirstName} {userBeingDeleted?.LastName}".Trim();

                int? branchId = dto.ClinicId != requestClinicId ? dto.ClinicId : null;

                _clinicMemberRepository.Remove(member);
                await _clinicRepository.SaveChangesAsync();
                await transaction.CommitAsync();

                var response = new
                {
                    UserId = dto.Id,
                    dto.ClinicId,
                    BranchClinicId = branchId,
                    DeletedBy = deletedBy,
                    DeletedByRole = deletedByRoleClaim,
                    NotificationContext = new
                    {
                        DeletedUserName = deletedUserName,
                        DeletedByName = deletedBy
                    },
                    NotificationMessage = $"{member.Role} {deletedUserName} was permanently deleted by {deletedBy}."
                };

                _logger.LogInformation("User ID {UserId} permanently deleted from Clinic ID {ClinicId} by {DeletedBy} ({Role})",
                    response.UserId, response.ClinicId, response.DeletedBy, response.DeletedByRole);

                return Ok(ApiResponseFactory.Success(response, $"{deletedUserName} permanently deleted."));
            }
            finally
            {
                if (transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.RollbackAsync();
                }
            }
        }





		[HttpGet("clinics/{clinicId}/pending-goal-settings")]
		public async Task<IActionResult> GetPendingGoalSettings([FromRoute] int clinicId)
		{
			try
			{
				var currentEpochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
				const int secondsInDay = 86400;
				var thresholdEpoch = currentEpochTime - (7 * secondsInDay);

				var baseData = await (from cv in _context.ClinicVisits
									  where cv.ClinicId == clinicId
									  join cp in _context.ClinicPatients on cv.ClinicPatientId equals cp.Id
									  join u in _context.Users on cp.HFID equals u.HfId
									  where u.DeletedBy == 0
									  join r in _context.ClinicPatientRecords on cv.Id equals r.ClinicVisitId
									  where r.Type == RecordType.Invoice
											&& r.IsGoalSettingRequired == true
											&& r.EpochTime <= thresholdEpoch
									  select new { User = u, ClinicPatient = cp, Receipt = r, VisitId = cv.Id })
									  .ToListAsync();

				var latestPerPatient = baseData
					.GroupBy(x => x.ClinicPatient.Id) // Group by ClinicPatientId
					.Select(g => g.OrderByDescending(x => x.Receipt.EpochTime).First())
					.ToList();

				// FIX: Match against ClinicPatient.Id
				var clinicPatientIds = latestPerPatient.Select(x => x.ClinicPatient.Id).ToList();

				var existingForms = await _context.high5ChocheForms
					.Where(f => f.ClinicId == clinicId
							 && clinicPatientIds.Contains(f.UserId) // DB column UserId holds ClinicPatientId
							 && f.FormName == "High5 Goal Setting")
					.Select(f => new { f.UserId, f.EpochTime })
					.ToListAsync();

				var finalResult = latestPerPatient
					.Where(p => !existingForms.Any(f => f.UserId == p.ClinicPatient.Id && f.EpochTime > p.Receipt.EpochTime))
					.Select(p => new
					{
						PatientId = p.User.Id,
						ClinicVisitId = p.VisitId,
						ClinicPatientId = p.ClinicPatient.Id,
						HFID = p.ClinicPatient.HFID,
						UserName = $"{p.User.FirstName} {p.User.LastName}".Trim(),
						DaysPending = (int)((currentEpochTime - p.Receipt.EpochTime) / secondsInDay),
						LatestReceipt = new
						{
							uniqueRecordId = p.Receipt.UniqueRecordId,
							date = DateTimeOffset.FromUnixTimeSeconds(p.Receipt.EpochTime).DateTime
						}
					}).ToList();

				return Ok(ApiResponseFactory.Success(new { PendingGoalSettings = finalResult }, $"Found {finalResult.Count} pending."));
			}
			catch (Exception ex)
			{
				return StatusCode(500, ApiResponseFactory.Fail(ex.Message));
			}
		}




	}
}
