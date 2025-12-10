using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HFiles_Backend.API.Services;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Labs;
using HFiles_Backend.Domain.Entities.Labs;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HFiles_Backend.API.Controllers.Labs
{
    [ApiController]
    [Route("api/")]
    public class LabBranchController(AppDbContext context, LabAuthorizationService labAuthorizationService, HttpClient httpClient, LocationService locationService, ILogger<LabBranchController> logger) : ControllerBase
    {
        private readonly AppDbContext _context = context;
        private readonly LabAuthorizationService _labAuthorizationService = labAuthorizationService;
        private readonly HttpClient _httpClient = httpClient;
        private readonly LocationService _locationService = locationService;
        private readonly ILogger<LabBranchController> _logger = logger;

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



        [HttpPost("labs/branches")]
        [Authorize(Policy = "SuperAdminOrAdminPolicy")]
        public async Task<IActionResult> CreateBranch([FromBody] Branch dto, [FromServices] OtpVerificationStore otpStore)
        {
            HttpContext.Items["Log-Category"] = "Lab Management";
            _logger.LogInformation("Received request to create a new lab branch. Payload: {@dto}", dto);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            if (!otpStore.Consume(dto.Email, "create_branch"))
            {
                _logger.LogWarning("Branch creation failed: OTP not verified or already used for Email {Email}", dto.Email);
                return Unauthorized(ApiResponseFactory.Fail("OTP not verified or already used. Please verify again."));
            }

            try
            {
                var labIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
                if (!int.TryParse(labIdClaim, out int labId))
                {
                    _logger.LogWarning("Branch creation failed: Invalid or missing LabId claim.");
                    return Unauthorized(ApiResponseFactory.Fail("Invalid or missing LabId claim."));
                }

                if (!await _labAuthorizationService.IsLabAuthorized(labId, User).ConfigureAwait(false))
                {
                    _logger.LogWarning("Branch creation failed: Lab ID {LabId} is not authorized.", labId);
                    return Unauthorized(ApiResponseFactory.Fail("Permission denied. You can only create/modify/delete data for your main lab or its branches."));
                }

                if (await _context.LabSignups.AnyAsync(u => u.Email == dto.Email).ConfigureAwait(false))
                {
                    _logger.LogWarning("Branch creation failed: Email {Email} is already registered.", dto.Email);
                    return BadRequest(ApiResponseFactory.Fail("Email already registered."));
                }

                var parentUser = await _context.LabSignups
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == labId)
                    .ConfigureAwait(false);

                if (parentUser == null)
                {
                    _logger.LogWarning("Branch creation failed: Parent lab with ID {LabId} not found.", labId);
                    return Unauthorized(ApiResponseFactory.Fail("Parent lab not found."));
                }

                var epochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var last6Epoch = epochTime % 1_000_000;
                var labPrefix = dto.LabName.Length >= 3 ? dto.LabName[..3].ToUpperInvariant() : dto.LabName.ToUpperInvariant();
                var randomDigits = Random.Shared.Next(1000, 9999);
                var hfid = $"HF{last6Epoch}{labPrefix}{randomDigits}";

                var newBranch = new LabSignup
                {
                    LabName = dto.LabName,
                    Email = dto.Email,
                    PhoneNumber = dto.PhoneNumber,
                    Pincode = dto.Pincode,
                    PasswordHash = parentUser.PasswordHash,
                    HFID = hfid,
                    CreatedAtEpoch = epochTime,
                    LabReference = labId
                };

                await using var tx = await _context.Database.BeginTransactionAsync().ConfigureAwait(false);

                _context.LabSignups.Add(newBranch);
                await _context.SaveChangesAsync().ConfigureAwait(false);
                await tx.CommitAsync().ConfigureAwait(false);

                string? createdByName = await ResolveUsernameFromClaims(HttpContext, _context);
                if (string.IsNullOrWhiteSpace(createdByName))
                    return Unauthorized(ApiResponseFactory.Fail("Unable to resolve creator identity."));

                var response = new
                {
                    newBranch.Id,
                    newBranch.HFID,
                    NotificationContext = new
                    {
                        BranchName = newBranch.LabName,
                        BranchEmail = newBranch.Email,
                        HFID = newBranch.HFID,
                        createdByName = createdByName
                    },
                    NotificationMessage = $"Branch {newBranch.LabName} created successfully by {createdByName}."
                };


                HttpContext.Items["CreatedBranchId"] = newBranch.Id;
                _logger.LogInformation("Branch created successfully. Branch ID: {BranchId}, HFID: {HFID}", newBranch.Id, newBranch.HFID);

                return Ok(ApiResponseFactory.Success(response, "Branch created successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Branch creation failed due to an unexpected error.");
                return StatusCode(500, ApiResponseFactory.Fail($"An unexpected error occurred: {ex.Message}"));
            }
        }






        // Verify Branch OTP
        [HttpPost("labs/branches/verify/otp")]
        [Authorize(Policy = "SuperAdminOrAdminPolicy")]
        [Authorize]
        public async Task<IActionResult> BranchVerifyOTP(
        [FromBody] OtpLogin dto,
        [FromServices] OtpVerificationStore otpStore)
        {
            HttpContext.Items["Log-Category"] = "Authentication";
            _logger.LogInformation("Received OTP verification request for Email: {Email}", dto.Email);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                                              .Select(e => e.ErrorMessage)
                                              .ToList();
                _logger.LogWarning("Validation failed: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            if (string.IsNullOrWhiteSpace(dto.Email))
                return BadRequest(ApiResponseFactory.Fail("Email must be provided."));

            try
            {
                var now = DateTime.UtcNow;

                var otpEntry = await _context.LabOtpEntries
                    .Where(o => o.Email == dto.Email)
                    .OrderByDescending(o => o.CreatedAt)
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);

                if (otpEntry == null)
                {
                    _logger.LogWarning("OTP verification failed: No OTP entry found for Email {Email}", dto.Email);
                    return BadRequest(ApiResponseFactory.Fail("OTP expired or not found."));
                }

                if (otpEntry.ExpiryTime < now)
                {
                    _logger.LogWarning("OTP verification failed: OTP expired for Email {Email}", dto.Email);
                    return BadRequest(ApiResponseFactory.Fail("OTP expired."));
                }

                if (!string.Equals(otpEntry.OtpCode, dto.Otp, StringComparison.Ordinal))
                {
                    _logger.LogWarning("OTP verification failed: Invalid OTP for Email {Email}", dto.Email);
                    return BadRequest(ApiResponseFactory.Fail("Invalid OTP."));
                }

                await using var tx = await _context.Database.BeginTransactionAsync().ConfigureAwait(false);

                var expiredOtps = await _context.LabOtpEntries
                    .Where(o => o.Email == dto.Email && o.ExpiryTime < now)
                    .ToListAsync()
                    .ConfigureAwait(false);

                _context.LabOtpEntries.RemoveRange(expiredOtps);
                _context.LabOtpEntries.Remove(otpEntry);

                await _context.SaveChangesAsync().ConfigureAwait(false);
                await tx.CommitAsync().ConfigureAwait(false);

                otpStore.StoreVerifiedOtp(dto.Email, "create_branch");

                _logger.LogInformation("OTP verification successful for Email {Email}", dto.Email);
                return Ok(ApiResponseFactory.Success("OTP successfully verified."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OTP verification failed for Email {Email}", dto.Email);
                return StatusCode(500, ApiResponseFactory.Fail($"An unexpected error occurred: {ex.Message}"));
            }
        }






        // Retrieves all labs
        [HttpGet("labs")]
        [Authorize]
        public async Task<IActionResult> GetLabBranches()
        {
            HttpContext.Items["Log-Category"] = "Lab Management";
            var userLabIdStr = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
            _logger.LogInformation("Fetching all labs and branches for Lab ID: {LabId}", userLabIdStr);

            if (!int.TryParse(userLabIdStr, out int labId))
            {
                _logger.LogWarning("Invalid LabId claim.");
                return Unauthorized(ApiResponseFactory.Fail("Invalid or missing LabId claim."));
            }

            try
            {
                if (!await _labAuthorizationService.IsLabAuthorized(labId, User).ConfigureAwait(false))
                {
                    _logger.LogWarning("Unauthorized access for Lab ID {LabId}", labId);
                    return Unauthorized(ApiResponseFactory.Fail("Permission denied. You can only access your main lab or its branches."));
                }

                var loggedInLab = await _context.LabSignups
                    .AsNoTracking()
                    .FirstOrDefaultAsync(l => l.Id == labId)
                    .ConfigureAwait(false);

                if (loggedInLab == null)
                {
                    _logger.LogWarning("Lab ID {LabId} not found.", labId);
                    return NotFound(ApiResponseFactory.Fail($"Lab with ID {labId} not found."));
                }

                int mainLabId = loggedInLab.LabReference == 0 ? labId : loggedInLab.LabReference;

                var mainLab = await _context.LabSignups
                    .AsNoTracking()
                    .FirstOrDefaultAsync(l => l.Id == mainLabId && l.DeletedBy == 0)
                    .ConfigureAwait(false);

                if (mainLab == null)
                {
                    _logger.LogWarning("Main lab ID {MainLabId} not found.", mainLabId);
                    return NotFound(ApiResponseFactory.Fail($"Main lab with ID {mainLabId} not found."));
                }

                _logger.LogInformation("Fetching branches for Main Lab ID: {MainLabId}", mainLabId);
                var branches = await _context.LabSignups
                    .AsNoTracking()
                    .Where(l => l.LabReference == mainLabId && l.DeletedBy == 0)
                    .ToListAsync()
                    .ConfigureAwait(false);

                _logger.LogInformation("Total branches found: {Count}", branches.Count);

                var result = new List<LabInfo>
        {
            new()
            {
                LabId = mainLab.Id,
                LabName = mainLab.LabName,
                HFID = mainLab.HFID,
                Email = mainLab.Email ?? "No email available",
                PhoneNumber = mainLab.PhoneNumber ?? "No phone number available",
                Pincode = mainLab.Pincode ?? "No pincode available",
                Location = await _locationService.GetLocationDetails(mainLab.Pincode),
                Address = mainLab.Address ?? "No address available",
                ProfilePhoto = mainLab.ProfilePhoto ?? "No image preview available",
                LabType = "mainLab"
            }
        };

                var branchTasks = branches.Select(async branch => new LabInfo
                {
                    LabId = branch.Id,
                    LabName = branch.LabName,
                    HFID = branch.HFID,
                    Email = branch.Email ?? "No email available",
                    PhoneNumber = branch.PhoneNumber ?? "No phone number available",
                    Pincode = branch.Pincode ?? "No pincode available",
                    Location = await _locationService.GetLocationDetails(branch.Pincode),
                    Address = branch.Address ?? "No address available",
                    ProfilePhoto = branch.ProfilePhoto ?? "No image preview available",
                    LabType = "branch"
                });

                result.AddRange(await Task.WhenAll(branchTasks).ConfigureAwait(false));

                var response = new
                {
                    LabCounts = result.Count,
                    Labs = result
                };

                _logger.LogInformation("Successfully returning all labs and branches.");
                return Ok(ApiResponseFactory.Success(response, "Lab branches fetched successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while fetching lab branches.");
                return StatusCode(500, ApiResponseFactory.Fail($"Unexpected error: {ex.Message}"));
            }
        }






        // Soft Delete a Branch
        [HttpPut("labs/branches/{branchId}")]
        [Authorize(Policy = "SuperAdminPolicy")]
        public async Task<IActionResult> DeleteBranch([FromRoute] int branchId)
        {
            HttpContext.Items["Log-Category"] = "Lab Management";
            HttpContext.Items["BranchId"] = branchId;

            _logger.LogInformation("Received request to delete Branch ID: {BranchId}", branchId);

            try
            {
                var labAdminIdStr = User.FindFirst("LabAdminId")?.Value;
                var labIdStr = User.FindFirst("UserId")?.Value;
                var role = User.FindFirst(ClaimTypes.Role)?.Value;

                if (role is "Admin" or "Member")
                {
                    _logger.LogWarning("Branch deletion failed: Unauthorized role {Role} attempted deletion.", role);
                    return Unauthorized(ApiResponseFactory.Fail($"{role} has no permissions."));
                }

                if (!int.TryParse(labAdminIdStr, out int labAdminId) || !int.TryParse(labIdStr, out int labId))
                {
                    _logger.LogWarning("Branch deletion failed: Invalid LabId or LabAdminId claim.");
                    return Unauthorized(ApiResponseFactory.Fail("Invalid or missing lab claims."));
                }

                if (!await _labAuthorizationService.IsLabAuthorized(branchId, User).ConfigureAwait(false))
                {
                    _logger.LogWarning("Branch deletion failed: Unauthorized attempt to delete Branch ID {BranchId}", branchId);
                    return Unauthorized(ApiResponseFactory.Fail("You can only manage your main lab or its branches."));
                }

                var labSuperAdmin = await _context.LabSuperAdmins
                    .FirstOrDefaultAsync(a => a.Id == labAdminId)
                    .ConfigureAwait(false);
                if (labSuperAdmin == null)
                    return BadRequest(ApiResponseFactory.Fail("No Super Admin found."));

                var loggedInLab = await _context.LabSignups
                    .AsNoTracking()
                    .FirstOrDefaultAsync(l => l.Id == labId)
                    .ConfigureAwait(false);
                if (loggedInLab == null)
                    return NotFound(ApiResponseFactory.Fail($"Lab with ID {labId} not found."));

                int mainLabId = loggedInLab.LabReference == 0 ? labId : loggedInLab.LabReference;

                var branch = await _context.LabSignups
                    .FirstOrDefaultAsync(l => l.Id == branchId)
                    .ConfigureAwait(false);

                if (branch == null)
                    return NotFound(ApiResponseFactory.Fail($"Branch with ID {branchId} not found."));

                if (branch.LabReference == 0)
                    return BadRequest(ApiResponseFactory.Fail("Cannot delete the main lab."));

                if (branch.LabReference != mainLabId)
                    return Unauthorized(ApiResponseFactory.Fail($"Branch with ID {branchId} does not belong to your lab."));

                if (branch.DeletedBy != 0)
                    return BadRequest(ApiResponseFactory.Fail("This branch has already been deleted."));

                await using var tx = await _context.Database.BeginTransactionAsync().ConfigureAwait(false);

                branch.DeletedBy = labSuperAdmin.Id;
                _context.LabSignups.Update(branch);
                await _context.SaveChangesAsync().ConfigureAwait(false);
                await tx.CommitAsync().ConfigureAwait(false);

                var deletedByUser = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == labSuperAdmin.UserId)
                    .ConfigureAwait(false);

                if (deletedByUser == null)
                    return BadRequest(ApiResponseFactory.Fail("No user found."));

                string? deletedByName = await ResolveUsernameFromClaims(HttpContext, _context);
                if (string.IsNullOrWhiteSpace(deletedByName))
                    return Unauthorized(ApiResponseFactory.Fail("Unable to resolve creator identity."));

                var response = new
                {
                    BranchId = branch.Id,
                    BranchName = branch.LabName,
                    DeletedBy = deletedByName,
                    NotificationContext = new
                    {
                        BranchId = branch.Id,
                        BranchName = branch.LabName,
                        DeletedBy = deletedByName
                    },
                    NotificationMessage = $"Branch '{branch.LabName}' was deleted by {deletedByName}."
                };


                _logger.LogInformation("Branch ID {BranchId} successfully deleted by {DeletedBy}.", branch.Id, deletedByName);
                return Ok(ApiResponseFactory.Success(response, $"Branch deleted successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Branch deletion failed due to an unexpected error.");
                return StatusCode(500, ApiResponseFactory.Fail($"An unexpected error occurred: {ex.Message}"));
            }
        }






        [HttpGet("labs/branches/{pincode}")]
        [Authorize]
        public async Task<IActionResult> GetLocation([FromRoute][RegularExpression(@"^\d{6}$", ErrorMessage = "Pincode must be a 6-digit number.")] string pincode)
        {
            HttpContext.Items["Log-Category"] = "Location Retrieval";
            _logger.LogInformation("Fetching location details for Pincode: {Pincode}", pincode);

            if (string.IsNullOrWhiteSpace(pincode))
            {
                _logger.LogWarning("Pincode missing.");
                return BadRequest(ApiResponseFactory.Fail("Pincode is required."));
            }

            try
            {
                await using var tx = await _context.Database.BeginTransactionAsync().ConfigureAwait(false);

                var location = await _locationService.GetLocationDetails(pincode).ConfigureAwait(false);

                if (location == null)
                {
                    _logger.LogWarning("No details found for Pincode {Pincode}", pincode);
                    return NotFound(ApiResponseFactory.Fail($"No location details found for pincode {pincode}."));
                }

                await tx.CommitAsync().ConfigureAwait(false);

                _logger.LogInformation("Location fetched for Pincode {Pincode}", pincode);
                return Ok(ApiResponseFactory.Success(new { success = true, location }, "Location details fetched successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during location fetch for Pincode {Pincode}", pincode);
                return StatusCode(500, ApiResponseFactory.Fail($"An unexpected error occurred: {ex.Message}"));
            }
        }






        [HttpGet("labs/deleted-branches")]
        [Authorize]
        public async Task<IActionResult> GetDeletedBranches()
        {
            HttpContext.Items["Log-Category"] = "Lab Management";

            var userLabIdStr = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
            _logger.LogInformation("Fetching deleted branches for Lab ID: {LabId}", userLabIdStr);

            if (!int.TryParse(userLabIdStr, out int labId))
            {
                _logger.LogWarning("Invalid or missing LabId claim.");
                return Unauthorized(ApiResponseFactory.Fail("Invalid or missing LabId claim."));
            }

            try
            {
                if (!await _labAuthorizationService.IsLabAuthorized(labId, User).ConfigureAwait(false))
                {
                    _logger.LogWarning("Unauthorized access for Lab ID {LabId}", labId);
                    return Unauthorized(ApiResponseFactory.Fail("Permission denied. You can only manage your main lab or its branches."));
                }

                var loggedInLab = await _context.LabSignups
                    .AsNoTracking()
                    .FirstOrDefaultAsync(l => l.Id == labId)
                    .ConfigureAwait(false);

                if (loggedInLab == null)
                {
                    _logger.LogWarning("Lab ID {LabId} not found.", labId);
                    return NotFound(ApiResponseFactory.Fail($"Lab with ID {labId} not found."));
                }

                int mainLabId = loggedInLab.LabReference == 0 ? labId : loggedInLab.LabReference;

                _logger.LogInformation("Retrieving deleted branches for Main Lab ID: {MainLabId}", mainLabId);

                await using var tx = await _context.Database.BeginTransactionAsync().ConfigureAwait(false);

                var deletedBranches = await _context.LabSignups
                    .AsNoTracking()
                    .Where(l => l.LabReference == mainLabId && l.DeletedBy != 0)
                    .Select(l => new
                    {
                        l.Id,
                        l.LabName,
                        l.Email,
                        l.HFID,
                        l.ProfilePhoto,
                        DeletedByUser = (from sa in _context.LabSuperAdmins
                                         join ud in _context.Users on sa.UserId equals ud.Id
                                         where sa.Id == l.DeletedBy
                                         select ud.FirstName + " " + ud.LastName)
                                         .FirstOrDefault(),
                        DeletedByUserRole = "Super Admin"
                    })
                    .ToListAsync().ConfigureAwait(false);

                await tx.CommitAsync().ConfigureAwait(false);

                if (!deletedBranches.Any())
                {
                    _logger.LogWarning("No deleted branches found for Lab ID {LabId}", labId);
                    return NotFound(ApiResponseFactory.Fail("No deleted branches found."));
                }

                _logger.LogInformation("Returning {Count} deleted branches for Lab ID {LabId}", deletedBranches.Count, labId);
                return Ok(ApiResponseFactory.Success(deletedBranches, "Deleted branches fetched successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while fetching deleted branches.");
                return StatusCode(500, ApiResponseFactory.Fail($"An unexpected error occurred: {ex.Message}"));
            }
        }






        // Revert Branches
        [HttpPatch("labs/revert-branch")]
        [Authorize(Policy = "SuperAdminOrAdminPolicy")]
        public async Task<IActionResult> RevertDeletedBranch([FromBody] RevertBranch dto)
        {
            HttpContext.Items["Log-Category"] = "Lab Management";
            _logger.LogInformation("Revert request received for Branch ID: {BranchId}", dto.Id);

            var labIdStr = User.FindFirst("UserId")?.Value;
            var revertedByIdStr = User.FindFirst("LabAdminId")?.Value;
            var revertedByRole = User.FindFirst(ClaimTypes.Role)?.Value;

            if (!int.TryParse(labIdStr, out int labId) || !int.TryParse(revertedByIdStr, out int revertedById))
            {
                _logger.LogWarning("Revert failed: Invalid LabId or LabAdminId claims.");
                return Unauthorized(ApiResponseFactory.Fail("Invalid or missing lab claims."));
            }

            try
            {
                if (!await _labAuthorizationService.IsLabAuthorized(labId, User).ConfigureAwait(false))
                {
                    _logger.LogWarning("Revert failed: Unauthorized lab access for Lab ID {LabId}", labId);
                    return Unauthorized(ApiResponseFactory.Fail("Unauthorized lab access."));
                }

                var loggedInLab = await _context.LabSignups
                    .AsNoTracking()
                    .FirstOrDefaultAsync(l => l.Id == labId)
                    .ConfigureAwait(false);
                if (loggedInLab == null)
                    return BadRequest(ApiResponseFactory.Fail("Lab not found."));

                int mainLabId = loggedInLab.LabReference == 0 ? labId : loggedInLab.LabReference;

                var branchIds = await _context.LabSignups
                    .AsNoTracking()
                    .Where(l => l.LabReference == mainLabId)
                    .Select(l => l.Id)
                    .ToListAsync()
                    .ConfigureAwait(false);
                branchIds.Add(mainLabId);

                var branch = await _context.LabSignups
                    .FirstOrDefaultAsync(l => l.Id == dto.Id && branchIds.Contains(l.Id) && l.DeletedBy != 0)
                    .ConfigureAwait(false);

                if (branch == null)
                {
                    _logger.LogWarning("Revert failed: Branch ID {BranchId} not found or not deleted.", dto.Id);
                    return NotFound(ApiResponseFactory.Fail("Branch not found or not deleted."));
                }

                var admin = await _context.LabSuperAdmins
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Id == revertedById)
                    .ConfigureAwait(false);
                if (admin == null)
                    return BadRequest(ApiResponseFactory.Fail("Super Admin not found."));

                var revertedByUser = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == admin.UserId)
                    .ConfigureAwait(false);
                if (revertedByUser == null)
                    return BadRequest(ApiResponseFactory.Fail("Admin user details not found."));

                string? revertedByName = await ResolveUsernameFromClaims(HttpContext, _context);
                if (string.IsNullOrWhiteSpace(revertedByName))
                    return Unauthorized(ApiResponseFactory.Fail("Unable to resolve creator identity."));

                await using var tx = await _context.Database.BeginTransactionAsync().ConfigureAwait(false);
                branch.DeletedBy = 0;
                _context.LabSignups.Update(branch);
                await _context.SaveChangesAsync().ConfigureAwait(false);
                await tx.CommitAsync().ConfigureAwait(false);

                var response = new
                {
                    BranchId = dto.Id,
                    BranchName = branch.LabName,
                    RevertedBy = revertedByName,
                    RevertedByRole = revertedByRole,
                    NotificationContext = new
                    {
                        BranchId = dto.Id,
                        BranchName = branch.LabName,
                        RevertedBy = revertedByName,
                        RevertedByRole = revertedByRole
                    },
                    NotificationMessage = $"Branch '{branch.LabName}' was restored by {revertedByName} ({revertedByRole})."
                };


                _logger.LogInformation("Branch ID {BranchId} reverted by {RevertedBy} ({Role})",
                    dto.Id, revertedByName, revertedByRole);

                return Ok(ApiResponseFactory.Success(response, "Branch reverted successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during branch revert for ID {BranchId}.", dto.Id);
                return StatusCode(500, ApiResponseFactory.Fail("Unexpected error occurred while reverting the branch."));
            }
        }

    }
}
