using HFiles_Backend.API.Services;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Labs;
using HFiles_Backend.Domain.Entities.Labs;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace HFiles_Backend.API.Controllers.Labs
{
    [ApiController]
    [Route("api/")]
    public class LabMemberController(AppDbContext context, IPasswordHasher<LabMember> passwordHasher, LabAuthorizationService labAuthorizationService, ILogger<LabMemberController> logger) : ControllerBase
    {
        private readonly AppDbContext _context = context;
        private readonly IPasswordHasher<LabMember> _passwordHasher = passwordHasher;
        private readonly LabAuthorizationService _labAuthorizationService = labAuthorizationService;
        private readonly ILogger<LabMemberController> _logger = logger;

        private static string GenerateNotificationMessage(List<string> names, string promoter)
        {
            if (names == null || names.Count == 0)
                return "No members promoted.";

            if (names.Count == 1)
                return $"{names[0]} was promoted to Admin by {promoter}.";

            string joinedNames = string.Join(", ", names);
            return $"{joinedNames} were promoted to Admin by {promoter}.";
        }


        private async Task<string?> ResolveUsernameFromClaims(HttpContext httpContext, AppDbContext dbContext)
        {
            var role = httpContext.User.FindFirst(ClaimTypes.Role)?.Value;
            var adminIdStr = httpContext.User.FindFirst("LabAdminId")?.Value;

            if (!int.TryParse(adminIdStr, out var adminId)) return null;

            if (role == "Super Admin")
            {
                var superAdmin = await dbContext.LabSuperAdmins.FirstOrDefaultAsync(sa => sa.Id == adminId);
                if (superAdmin != null)
                {
                    var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == superAdmin.UserId && u.DeletedBy == 0);
                    return $"{user?.FirstName} {user?.LastName}".Trim();
                }
            }

            if (role == "Admin")
            {
                var member = await dbContext.LabMembers.FirstOrDefaultAsync(m => m.Id == adminId);
                if (member != null)
                {
                    var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == member.UserId && u.DeletedBy == 0);
                    return $"{user?.FirstName} {user?.LastName}".Trim();
                }
            }

            return null;
        }





        // Create Member
        [HttpPost("labs/members")]
        [Authorize(Policy = "SuperAdminOrAdminPolicy")]
        public async Task<IActionResult> AddMember([FromBody] CreateMember dto)
        {
            HttpContext.Items["Log-Category"] = "User Management";
            _logger.LogInformation("Received request to create new lab member. HFID: {HFID}, Branch ID: {BranchId}", dto.HFID, dto.BranchId);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            try
            {
                var labIdClaim = User.FindFirst("UserId")?.Value;
                if (labIdClaim == null || !int.TryParse(labIdClaim, out int labId))
                    return Unauthorized(ApiResponseFactory.Fail("Invalid or missing LabId claim."));

                if (!await _labAuthorizationService.IsLabAuthorized(labId, User).ConfigureAwait(false))
                    return Unauthorized(ApiResponseFactory.Fail("Permission denied. You can only create/modify/delete data for your main lab or its branches."));

                var createdByClaim = User.FindFirst("LabAdminId")?.Value;
                if (createdByClaim == null || !int.TryParse(createdByClaim, out int createdBy))
                    return Unauthorized(ApiResponseFactory.Fail("Invalid or missing LabAdminId in token."));

                string? createdByName = await ResolveUsernameFromClaims(HttpContext, _context);
                if (string.IsNullOrWhiteSpace(createdByName))
                    return Unauthorized(ApiResponseFactory.Fail("Unable to resolve creator identity."));


                await using var tx = await _context.Database.BeginTransactionAsync().ConfigureAwait(false);

                var userDetails = await _context.Users
                    .FirstOrDefaultAsync(u => u.HfId == dto.HFID)
                    .ConfigureAwait(false);

                if (userDetails == null)
                    return NotFound(ApiResponseFactory.Fail($"No user found with HFID {dto.HFID}."));

                var labEntry = await _context.LabSignups
                    .FirstOrDefaultAsync(l => l.Id == dto.BranchId)
                    .ConfigureAwait(false);

                if (labEntry == null)
                    return NotFound(ApiResponseFactory.Fail($"No lab found with Branch ID {dto.BranchId}."));

                int mainLabId = labEntry.LabReference == 0 ? labId : labEntry.LabReference;

                var existingMember = await _context.LabMembers
                    .FirstOrDefaultAsync(m =>
                        m.UserId == userDetails.Id &&
                        (m.LabId == dto.BranchId || m.LabId == labEntry.LabReference || m.LabId == mainLabId))
                    .ConfigureAwait(false);

                if (existingMember != null)
                {
                    string fullName = $"{userDetails.FirstName} {userDetails.LastName}";
                    return BadRequest(ApiResponseFactory.Fail($"{fullName}'s HFID {dto.HFID} already exists as {existingMember.Role} in Branch {existingMember.LabId}."));
                }

                var superAdmin = await _context.LabSuperAdmins
                    .FirstOrDefaultAsync(a =>
                        a.UserId == userDetails.Id && a.IsMain == 1 && a.LabId == labId)
                    .ConfigureAwait(false);

                if (superAdmin != null)
                    return BadRequest(ApiResponseFactory.Fail("User is already a registered Super Admin."));

                var newMember = new LabMember
                {
                    UserId = userDetails.Id,
                    LabId = labEntry.Id,
                    PasswordHash = _passwordHasher.HashPassword(null!, dto.Password),
                    CreatedBy = createdBy
                };

                _context.LabMembers.Add(newMember);
                await _context.SaveChangesAsync().ConfigureAwait(false);
                await tx.CommitAsync().ConfigureAwait(false);

                var fullNames = $"{userDetails.FirstName} {userDetails.LastName}".Trim();

                var responseData = new CreateMemberResponse
                {
                    UserId = newMember.UserId,
                    Name = fullNames,
                    Email = userDetails.Email!,
                    LabId = newMember.LabId,
                    LabName = labEntry.LabName!,
                    CreatedBy = createdByName,
                    Role = newMember.Role,
                    EpochTime = newMember.EpochTime,
                    BranchLabId = newMember.LabId != labId ? newMember.LabId : 0,
                    NotificationContext = new NotificationContext
                    {
                        MemberName = fullNames,
                        CreatedByName = createdByName
                    },
                    NotificationMessage = $"{fullNames} was successfully added by {createdByName}"
                };

                _logger.LogInformation("New lab member created successfully. User ID: {UserId}, Lab ID: {LabId}.", newMember.UserId, newMember.LabId);
                return Ok(ApiResponseFactory.Success(responseData, "Member added successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Member creation failed due to an unexpected error.");
                return StatusCode(500, ApiResponseFactory.Fail($"An unexpected error occurred: {ex.Message}"));
            }
        }





        // Promote Members to Admins API
        [HttpPost("labs/members/promote")]
        [Authorize(Policy = "SuperAdminOrAdminPolicy")]
        public async Task<IActionResult> PromoteLabMembers([FromBody] PromoteMembersRequest dto)
        {
            HttpContext.Items["Log-Category"] = "Role Management";
            _logger.LogInformation("Promote request received for members: {Ids}", dto.Ids);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            try
            {
                var labIdStr = User.FindFirst("UserId")?.Value;
                var promoterIdStr = User.FindFirst("LabAdminId")?.Value;
                var promoterRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";

                if (!int.TryParse(labIdStr, out var labId) || !int.TryParse(promoterIdStr, out var promoterId))
                    return Unauthorized(ApiResponseFactory.Fail("Missing or invalid lab/user claims."));

                if (!await _labAuthorizationService.IsLabAuthorized(labId, User).ConfigureAwait(false))
                    return Unauthorized(ApiResponseFactory.Fail("Permission denied to promote members."));

                var promoterUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == promoterId).ConfigureAwait(false);
                var promoterName = $"{promoterUser?.FirstName} {promoterUser?.LastName}".Trim();

                var labEntry = await _context.LabSignups.FirstOrDefaultAsync(l => l.Id == labId).ConfigureAwait(false);
                if (labEntry == null)
                    return NotFound(ApiResponseFactory.Fail("Main lab not found."));

                int mainLabId = labEntry.LabReference == 0 ? labId : labEntry.LabReference;

                var branchIds = await _context.LabSignups
                    .Where(l => l.LabReference == mainLabId)
                    .Select(l => l.Id)
                    .ToListAsync()
                    .ConfigureAwait(false);

                branchIds.Add(mainLabId);

                var promoteResults = new List<object>();
                var memberNames = new List<string>();
                int? branchIdForAudit = null;

                await using var tx = await _context.Database.BeginTransactionAsync().ConfigureAwait(false);

                foreach (var memberId in dto.Ids)
                {
                    var member = await _context.LabMembers
                        .FirstOrDefaultAsync(m => m.Id == memberId && branchIds.Contains(m.LabId) && m.DeletedBy == 0)
                        .ConfigureAwait(false);

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

                    member.Role = "Admin";
                    member.PromotedBy = promoterId;
                    _context.LabMembers.Update(member);

                    var memberUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == member.UserId).ConfigureAwait(false);
                    var memberName = $"{memberUser?.FirstName} {memberUser?.LastName}".Trim();
                    memberNames.Add(memberName);

                    if (member.LabId != labId)
                        branchIdForAudit = member.LabId;

                    promoteResults.Add(new
                    {
                        member.Id,
                        Status = "Success",
                        NewRole = "Admin",
                        PromotedBy = promoterId,
                        PromotedByName = promoterName,
                        MemberName = memberName,
                        BranchLabId = branchIdForAudit
                    });
                }

                await _context.SaveChangesAsync().ConfigureAwait(false);
                await tx.CommitAsync().ConfigureAwait(false);

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
                    BranchLabId = branchIdForAudit,
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while promoting members.");
                return StatusCode(500, ApiResponseFactory.Fail($"Unexpected error: {ex.Message}"));
            }
        }





        // Delete member
        [HttpPut("labs/members/{memberId}")]
        [Authorize(Policy = "SuperAdminOrAdminPolicy")]
        public async Task<IActionResult> DeleteLabMember([FromRoute][Range(1, int.MaxValue)] int memberId)
        {
            HttpContext.Items["Log-Category"] = "User Management";
            HttpContext.Items["MemberId"] = memberId;

            _logger.LogInformation("Request received to delete member ID: {MemberId}", memberId);

            try
            {
                var labIdStr = User.FindFirst("UserId")?.Value;
                var deletedByStr = User.FindFirst("LabAdminId")?.Value;
                var deletedByRole = User.FindFirst(ClaimTypes.Role)?.Value;

                if (!int.TryParse(labIdStr, out var labId) || !int.TryParse(deletedByStr, out var deletedById))
                    return Unauthorized(ApiResponseFactory.Fail("Missing or invalid user claims."));

                if (!await _labAuthorizationService.IsLabAuthorized(labId, User).ConfigureAwait(false))
                    return Unauthorized(ApiResponseFactory.Fail("You are not authorized to manage this lab."));

                await using var tx = await _context.Database.BeginTransactionAsync().ConfigureAwait(false);

                var lab = await _context.LabSignups.FirstOrDefaultAsync(l => l.Id == labId).ConfigureAwait(false);
                if (lab == null)
                    return NotFound(ApiResponseFactory.Fail("Lab not found."));

                int mainLabId = lab.LabReference == 0 ? labId : lab.LabReference;

                var branchIds = await _context.LabSignups
                    .Where(l => l.LabReference == mainLabId)
                    .Select(l => l.Id)
                    .ToListAsync().ConfigureAwait(false);
                branchIds.Add(mainLabId);

                var member = await _context.LabMembers
                    .FirstOrDefaultAsync(m => m.Id == memberId && branchIds.Contains(m.LabId) && m.DeletedBy == 0)
                    .ConfigureAwait(false);

                if (member == null)
                    return NotFound(ApiResponseFactory.Fail("Lab member not found."));

                var memberUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == member.UserId).ConfigureAwait(false);
                var deletedByUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == deletedById).ConfigureAwait(false);

                string memberName = $"{memberUser?.FirstName} {memberUser?.LastName}".Trim();
                string? deletedByName = await ResolveUsernameFromClaims(HttpContext, _context);
                if (string.IsNullOrWhiteSpace(deletedByName))
                    return Unauthorized(ApiResponseFactory.Fail("Unable to resolve creator identity."));

                member.DeletedBy = deletedById;
                _context.LabMembers.Update(member);
                await _context.SaveChangesAsync().ConfigureAwait(false);
                await tx.CommitAsync().ConfigureAwait(false);

                int? branchLabId = member.LabId != labId ? member.LabId : null;

                var response = new
                {
                    MemberId = member.Id,
                    DeletedBy = deletedById,
                    DeletedByRole = deletedByRole,
                    MemberName = memberName,
                    DeletedByName = deletedByName,
                    BranchLabId = branchLabId,
                    NotificationContext = new
                    {
                        MemberName = memberName,
                        DeletedByName = deletedByName,
                        Role = member.Role
                    },
                    NotificationMessage = $"{member.Role} {memberName} was deleted by {deletedByName}."
                };

                _logger.LogInformation("Successfully deleted member ID: {MemberId} by {DeletedByName} ({Role})", member.Id, deletedByName, deletedByRole);
                return Ok(ApiResponseFactory.Success(response, $"{member.Role} deleted successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while deleting member ID: {MemberId}", memberId);
                return StatusCode(500, ApiResponseFactory.Fail($"Unexpected error: {ex.Message}"));
            }
        }





        // Get all Deleted Users of the selected Lab
        [HttpGet("labs/{labId}/deleted-users")]
        [Authorize]
        public async Task<IActionResult> GetDeletedUsers([FromRoute][Range(1, int.MaxValue)] int labId)
        {
            HttpContext.Items["Log-Category"] = "User Management";
            _logger.LogInformation("Fetching deleted users for Lab ID: {LabId}", labId);

            try
            {
                var userLabIdStr = User.FindFirst("UserId")?.Value;
                if (!int.TryParse(userLabIdStr, out int userLabId))
                {
                    _logger.LogWarning("Deleted user fetch failed: Invalid or missing LabId claim.");
                    return Unauthorized(ApiResponseFactory.Fail("Invalid or missing LabId claim."));
                }

                if (!await _labAuthorizationService.IsLabAuthorized(userLabId, User).ConfigureAwait(false))
                {
                    _logger.LogWarning("Unauthorized access attempt for Lab ID {LabId}", userLabId);
                    return Unauthorized(ApiResponseFactory.Fail("Permission denied. You can only manage your main lab or its branches."));
                }

                await using var tx = await _context.Database.BeginTransactionAsync().ConfigureAwait(false);

                var loggedInLab = await _context.LabSignups
                    .AsNoTracking()
                    .FirstOrDefaultAsync(l => l.Id == userLabId)
                    .ConfigureAwait(false);

                if (loggedInLab == null)
                {
                    _logger.LogWarning("Deleted user fetch failed: Lab ID {LabId} not found.", userLabId);
                    return BadRequest(ApiResponseFactory.Fail("Lab not found"));
                }

                int mainLabId = loggedInLab.LabReference == 0 ? userLabId : loggedInLab.LabReference;

                var branchIds = await _context.LabSignups
                    .Where(l => l.LabReference == mainLabId)
                    .Select(l => l.Id)
                    .ToListAsync()
                    .ConfigureAwait(false);
                branchIds.Add(mainLabId);

                var deletedMembers = await (
                    from m in _context.LabMembers
                    where m.LabId == labId && m.DeletedBy != 0
                    join ud in _context.Users on m.UserId equals ud.Id
                    select new
                    {
                        m.Id,
                        m.UserId,
                        Name = $"{ud.FirstName} {ud.LastName}",
                        Email = ud.Email ?? "Email Not Found",
                        HFID = ud.HfId ?? "HFID Not Found",
                        ProfilePhoto = string.IsNullOrEmpty(ud.ProfilePhoto) ? "No image preview available" : ud.ProfilePhoto,
                        m.LabId,
                        m.Role,
                        DeletedByUser = (
                            from sa in _context.LabSuperAdmins
                            join sUser in _context.Users on sa.UserId equals sUser.Id
                            where sa.Id == m.DeletedBy && (branchIds.Contains(m.LabId))
                            select $"{sUser.FirstName} {sUser.LastName}"
                        ).FirstOrDefault() ?? (
                            from lm in _context.LabMembers
                            join lUser in _context.Users on lm.UserId equals lUser.Id
                            where lm.Id == m.DeletedBy && (branchIds.Contains(m.LabId))
                            select $"{lUser.FirstName} {lUser.LastName}"
                        ).FirstOrDefault() ?? "Name Not Found",

                        DeletedByUserRole = (
                            from sa in _context.LabSuperAdmins
                            where sa.Id == m.DeletedBy && (branchIds.Contains(m.LabId))
                            select "Super Admin"
                        ).FirstOrDefault() ?? (
                            from lm in _context.LabMembers
                            where lm.Id == m.DeletedBy && (branchIds.Contains(m.LabId))
                            select lm.Role
                        ).FirstOrDefault() ?? "Role Not Found"
                    }).ToListAsync().ConfigureAwait(false);

                if (!deletedMembers.Any())
                {
                    _logger.LogWarning("No deleted users found for Lab ID {LabId}.", labId);
                    return NotFound(ApiResponseFactory.Fail("No deleted users found for this lab."));
                }

                await tx.CommitAsync().ConfigureAwait(false);

                _logger.LogInformation("Deleted users fetched: Lab ID {LabId}, Count = {Count}", labId, deletedMembers.Count);
                return Ok(ApiResponseFactory.Success(new { DeletedMembers = deletedMembers }, "Deleted users fetched successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching deleted users for Lab ID {LabId}.", labId);
                return StatusCode(500, ApiResponseFactory.Fail("An unexpected error occurred."));
            }
        }





        // Revert Deleted Users
        [HttpPatch("labs/revert-user")]
        [Authorize(Policy = "SuperAdminOrAdminPolicy")]
        public async Task<IActionResult> RevertDeletedUser([FromBody] RevertUser dto)
        {
            HttpContext.Items["Log-Category"] = "User Management";

            _logger.LogInformation("Reverting user. User ID: {UserId}, Lab ID: {LabId}, New Role: {Role}", dto.Id, dto.LabId, dto.Role);

            try
            {
                var labIdClaim = User.FindFirst("UserId")?.Value;
                var labAdminIdClaim = User.FindFirst("LabAdminId")?.Value;
                var revertedByRoleClaim = User.FindFirst(ClaimTypes.Role)?.Value;

                if (!int.TryParse(labIdClaim, out int requestLabId) || !int.TryParse(labAdminIdClaim, out int revertedById))
                    return Unauthorized(ApiResponseFactory.Fail("Invalid or missing LabId/LabAdminId claim."));

                if (!await _labAuthorizationService.IsLabAuthorized(requestLabId, User).ConfigureAwait(false))
                    return Unauthorized(ApiResponseFactory.Fail("Permission denied. You can only manage your main lab or its branches."));

                await using var tx = await _context.Database.BeginTransactionAsync().ConfigureAwait(false);

                var loggedInLab = await _context.LabSignups.FirstOrDefaultAsync(l => l.Id == requestLabId).ConfigureAwait(false);
                if (loggedInLab == null)
                    return BadRequest(ApiResponseFactory.Fail("Lab not found."));

                int mainLabId = loggedInLab.LabReference == 0 ? requestLabId : loggedInLab.LabReference;
                var branchIds = await _context.LabSignups
                    .Where(l => l.LabReference == mainLabId)
                    .Select(l => l.Id)
                    .ToListAsync().ConfigureAwait(false);
                branchIds.Add(mainLabId);

                var user = await _context.LabMembers
                    .FirstOrDefaultAsync(m => m.Id == dto.Id && m.LabId == dto.LabId && m.DeletedBy != 0)
                    .ConfigureAwait(false);

                if (user == null)
                    return NotFound(ApiResponseFactory.Fail("User not found or not marked as deleted."));

                var labSuperAdmin = await _context.LabSuperAdmins
                    .FirstOrDefaultAsync(a => a.Id == revertedById)
                    .ConfigureAwait(false);

                var revertedByUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == labSuperAdmin!.UserId)
                    .ConfigureAwait(false);

                var reinstatedUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == user.UserId)
                    .ConfigureAwait(false);


                string? revertedByName = await ResolveUsernameFromClaims(HttpContext, _context);
                if (string.IsNullOrWhiteSpace(revertedByName))
                    return Unauthorized(ApiResponseFactory.Fail("Unable to resolve creator identity."));
                string reinstatedUserName = $"{reinstatedUser?.FirstName} {reinstatedUser?.LastName}".Trim();

                user.DeletedBy = 0;
                user.Role = dto.Role ?? "Member";
                user.PromotedBy = dto.Role == "Admin" && int.TryParse(labAdminIdClaim, out int adminId) ? adminId : 0;

                _context.LabMembers.Update(user);
                await _context.SaveChangesAsync().ConfigureAwait(false);
                await tx.CommitAsync().ConfigureAwait(false);

                int? branchId = user.LabId != requestLabId ? user.LabId : null;

                var response = new
                {
                    UserId = user.Id,
                    user.LabId,
                    BranchLabId = branchId,
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


                _logger.LogInformation("Reverted user ID {UserId} with new role {NewRole} by {RevertedBy}", user.Id, user.Role, revertedByName);
                return Ok(ApiResponseFactory.Success(response, "User reverted successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred in RevertDeletedUser.");
                return StatusCode(500, ApiResponseFactory.Fail("Unexpected error."));
            }
        }





        // Permanently Removes User
        [HttpDelete("labs/remove-user")]
        [Authorize(Policy = "SuperAdminPolicy")]
        public async Task<IActionResult> PermanentlyDeleteUser([FromBody] DeleteUser dto)
        {
            HttpContext.Items["Log-Category"] = "User Management";
            _logger.LogInformation("Request to permanently delete user. User ID: {UserId}, Lab ID: {LabId}", dto.Id, dto.LabId);

            try
            {
                var labIdClaim = User.FindFirst("UserId")?.Value;
                if (!int.TryParse(labIdClaim, out int requestLabId))
                    return Unauthorized(ApiResponseFactory.Fail("Invalid or missing LabId claim."));

                if (!await _labAuthorizationService.IsLabAuthorized(requestLabId, User).ConfigureAwait(false))
                    return Unauthorized(ApiResponseFactory.Fail("Unauthorized to manage this lab or its branches."));

                var deletedByIdClaim = User.FindFirst("LabAdminId")?.Value;
                var deletedByRoleClaim = User.FindFirst(ClaimTypes.Role)?.Value;

                if (!int.TryParse(deletedByIdClaim, out int deletedById))
                    return Unauthorized(ApiResponseFactory.Fail("Invalid or missing LabAdminId claim."));

                var labSuperAdmin = await _context.LabSuperAdmins.FirstOrDefaultAsync(a => a.Id == deletedById).ConfigureAwait(false);
                if (labSuperAdmin == null)
                    return BadRequest(ApiResponseFactory.Fail("No Super Admin found."));

                var user = await _context.LabMembers
                    .FirstOrDefaultAsync(m => m.Id == dto.Id && m.LabId == dto.LabId && m.DeletedBy != 0)
                    .ConfigureAwait(false);

                if (user == null)
                    return NotFound(ApiResponseFactory.Fail("User not found."));

                var deletedByUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == labSuperAdmin.UserId).ConfigureAwait(false);
                if (deletedByUser == null)
                    return BadRequest(ApiResponseFactory.Fail("User details for Super Admin not found."));


                string? deletedBy = await ResolveUsernameFromClaims(HttpContext, _context);
                if (string.IsNullOrWhiteSpace(deletedBy))
                    return Unauthorized(ApiResponseFactory.Fail("Unable to resolve creator identity."));

                var userBeingDeleted = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == user.UserId).ConfigureAwait(false);
                string deletedUserName = $"{userBeingDeleted?.FirstName} {userBeingDeleted?.LastName}".Trim();

                int? branchId = dto.LabId != requestLabId ? dto.LabId : null;

                await using var transaction = await _context.Database.BeginTransactionAsync().ConfigureAwait(false);

                try
                {
                    _context.Remove(user);
                    await _context.SaveChangesAsync().ConfigureAwait(false);
                    await transaction.CommitAsync().ConfigureAwait(false);

                    var response = new
                    {
                        UserId = dto.Id,
                        dto.LabId,
                        BranchLabId = branchId,
                        DeletedBy = deletedBy,
                        DeletedByRole = deletedByRoleClaim,
                        NotificationContext = new
                        {
                            DeletedUserName = deletedUserName,
                            DeletedByName = deletedBy
                        },
                        NotificationMessage = $"{user.Role} {deletedUserName} was permanently deleted by {deletedBy}."
                    };


                    _logger.LogInformation("User ID {UserId} permanently deleted from Lab ID {LabId} by {DeletedBy} ({Role})",
                        response.UserId, response.LabId, response.DeletedBy, response.DeletedByRole);

                    return Ok(ApiResponseFactory.Success(response, "User permanently deleted successfully."));
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync().ConfigureAwait(false);
                    _logger.LogError(ex, "Error deleting User ID {UserId} in Lab ID {LabId}.", dto.Id, dto.LabId);
                    return StatusCode(500, ApiResponseFactory.Fail("An unexpected error occurred while deleting the user."));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred in User Deletion API.");
                return StatusCode(500, ApiResponseFactory.Fail("Unexpected error."));
            }
        }
    }
}
