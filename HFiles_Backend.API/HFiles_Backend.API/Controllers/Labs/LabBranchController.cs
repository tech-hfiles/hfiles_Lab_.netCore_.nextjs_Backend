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
                var labIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
                if (labIdClaim == null || !int.TryParse(labIdClaim.Value, out int labId))
                {
                    _logger.LogWarning("Branch creation failed: Invalid or missing LabId claim.");
                    return Unauthorized(ApiResponseFactory.Fail("Invalid or missing LabId claim."));
                }

                if (!await _labAuthorizationService.IsLabAuthorized(labId, User))
                {
                    _logger.LogWarning("Branch creation failed: Lab ID {LabId} is not authorized.", labId);
                    return Unauthorized(ApiResponseFactory.Fail("Permission denied. You can only create/modify/delete data for your main lab or its branches."));
                }

                var parentLabId = labId;

                bool emailExists = await _context.LabSignups.AnyAsync(u => u.Email == dto.Email);
                if (emailExists)
                {
                    _logger.LogWarning("Branch creation failed: Email {Email} is already registered.", dto.Email);
                    return BadRequest(ApiResponseFactory.Fail("Email already registered."));
                }

                var parentUser = await _context.LabSignups.FirstOrDefaultAsync(u => u.Id == parentLabId);
                if (parentUser == null)
                {
                    _logger.LogWarning("Branch creation failed: Parent lab with ID {ParentLabId} not found.", parentLabId);
                    return Unauthorized(ApiResponseFactory.Fail("Parent lab not found."));
                }

                var epochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var last6Epoch = epochTime % 1_000_000;
                var labPrefix = dto.LabName.Length >= 3 ? dto.LabName[..3].ToUpperInvariant() : dto.LabName.ToUpperInvariant();
                var randomDigits = new Random().Next(1000, 9999);
                var hfid = $"HF{last6Epoch}{labPrefix}{randomDigits}";

                var branchUser = new LabSignup
                {
                    LabName = dto.LabName,
                    Email = dto.Email,
                    PhoneNumber = dto.PhoneNumber,
                    Pincode = dto.Pincode,
                    PasswordHash = parentUser.PasswordHash,
                    HFID = hfid,
                    CreatedAtEpoch = epochTime,
                    LabReference = parentLabId
                };

                _context.LabSignups.Add(branchUser);
                await _context.SaveChangesAsync();

                var response = new
                {
                    branchUser.Id,
                    branchUser.HFID
                };

                HttpContext.Items["CreatedBranchId"] = branchUser.Id;
                _logger.LogInformation("Branch created successfully. Branch ID: {BranchId}, HFID: {HFID}", branchUser.Id, branchUser.HFID);

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
        public async Task<IActionResult> BranchVerifyOTP([FromBody] OtpLogin dto, [FromServices] OtpVerificationStore otpStore)
        {
            HttpContext.Items["Log-Category"] = "Authentication";

            _logger.LogInformation("Received OTP verification request for Email: {Email}", dto.Email);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            if (dto.Email == null)
                return BadRequest(ApiResponseFactory.Fail("Email must be provided."));

            try
            {
                var now = DateTime.UtcNow;

                var otpEntry = await _context.LabOtpEntries
                    .Where(o => o.Email == dto.Email)
                    .OrderByDescending(o => o.CreatedAt)
                    .FirstOrDefaultAsync();

                if (otpEntry == null)
                {
                    _logger.LogWarning("OTP verification failed: No OTP entry found for Email {Email}", dto.Email);
                    return BadRequest(ApiResponseFactory.Fail("OTP expired or not found."));
                }

                if (otpEntry.ExpiryTime < DateTime.UtcNow)
                {
                    _logger.LogWarning("OTP verification failed: OTP expired for Email {Email}", dto.Email);
                    return BadRequest(ApiResponseFactory.Fail("OTP expired."));
                }

                if (otpEntry.OtpCode != dto.Otp)
                {
                    _logger.LogWarning("OTP verification failed: Invalid OTP provided for Email {Email}", dto.Email);
                    return BadRequest(ApiResponseFactory.Fail("Invalid OTP."));
                }

                var expiredOtps = await _context.LabOtpEntries
                  .Where(x => x.Email == dto.Email && x.ExpiryTime < now)
                  .ToListAsync();
                _context.LabOtpEntries.RemoveRange(expiredOtps);
                _context.LabOtpEntries.Remove(otpEntry);
                await _context.SaveChangesAsync();

                otpStore.StoreVerifiedOtp(dto.Email, "create_branch");

                _logger.LogInformation("OTP verification successful for Email {Email}", dto.Email);
                return Ok(ApiResponseFactory.Success("OTP successfully verified."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OTP verification failed due to an unexpected error for Email {Email}", dto.Email);
                return StatusCode(500, ApiResponseFactory.Fail($"An unexpected error occurred: {ex.Message}"));
            }
        }





        // Retrieves all labs
        [HttpGet("labs")]
        [Authorize]
        public async Task<IActionResult> GetLabBranches()
        {
            HttpContext.Items["Log-Category"] = "Lab Management";

            _logger.LogInformation("Received request to fetch all labs and branches for Lab ID: {LabId}", User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value);

            try
            {
                var labIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
                if (labIdClaim == null || !int.TryParse(labIdClaim.Value, out int labId))
                {
                    _logger.LogWarning("Lab fetching failed: Invalid or missing LabId claim.");
                    return Unauthorized(ApiResponseFactory.Fail("Invalid or missing LabId claim."));
                }

                if (!await _labAuthorizationService.IsLabAuthorized(labId, User))
                {
                    _logger.LogWarning("Lab fetching failed: Unauthorized access for Lab ID {LabId}", labId);
                    return Unauthorized(ApiResponseFactory.Fail("Permission denied. You can only create/modify/delete data for your main lab or its branches."));
                }

                var loggedInLab = await _context.LabSignups.FirstOrDefaultAsync(l => l.Id == labId);
                if (loggedInLab == null)
                {
                    _logger.LogWarning("Lab fetching failed: Lab ID {LabId} not found.", labId);
                    return NotFound(ApiResponseFactory.Fail($"Lab with ID {labId} not found."));
                }

                int mainLabId = loggedInLab.LabReference == 0 ? labId : loggedInLab.LabReference;

                var mainLab = await _context.LabSignups.FirstOrDefaultAsync(l => l.Id == mainLabId && l.DeletedBy == 0);
                if (mainLab == null)
                {
                    _logger.LogWarning("Lab fetching failed: Main lab ID {MainLabId} not found.", mainLabId);
                    return NotFound(ApiResponseFactory.Fail($"Main lab with ID {mainLabId} not found."));
                }

                _logger.LogInformation("Fetching branches for Main Lab ID: {MainLabId}", mainLabId);
                var branches = await _context.LabSignups.Where(l => l.LabReference == mainLabId && l.DeletedBy == 0).ToListAsync();

                _logger.LogInformation("Total branches found: {BranchCount}", branches.Count);

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

                result.AddRange(await Task.WhenAll(branchTasks));

                var response = new
                {
                    LabCounts = branches.Count + 1,
                    Labs = result
                };

                _logger.LogInformation("Successfully fetched lab branches. Returning response.");
                return Ok(ApiResponseFactory.Success(response, "Lab branches fetched successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lab fetching failed due to an unexpected error.");
                return StatusCode(500, ApiResponseFactory.Fail($"An unexpected error occurred: {ex.Message}"));
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
                var labAdminIdClaim = User.Claims.FirstOrDefault(c => c.Type == "LabAdminId");
                var labIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
                var roleClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);

                if (roleClaim?.Value == "Admin" || roleClaim?.Value == "Member")
                {
                    _logger.LogWarning("Branch deletion failed: Unauthorized role {Role} attempted deletion.", roleClaim.Value);
                    return Unauthorized(ApiResponseFactory.Fail($"{roleClaim.Value} has no permissions."));
                }

                if (labAdminIdClaim == null || !int.TryParse(labAdminIdClaim.Value, out int labAdminId))
                {
                    _logger.LogWarning("Branch deletion failed: Invalid or missing Super Admin Id.");
                    return Unauthorized(ApiResponseFactory.Fail("Invalid or missing Super Admin Id in token."));
                }

                if (labIdClaim == null || !int.TryParse(labIdClaim.Value, out int labId))
                {
                    _logger.LogWarning("Branch deletion failed: Invalid or missing LabId.");
                    return Unauthorized(ApiResponseFactory.Fail("Invalid or missing LabId claim."));
                }

                if (!await _labAuthorizationService.IsLabAuthorized(branchId, User))
                {
                    _logger.LogWarning("Branch deletion failed: Unauthorized attempt to delete Branch ID {BranchId}", branchId);
                    return Unauthorized(ApiResponseFactory.Fail("Permission denied. You can only manage your main lab or its branches."));
                }

                var labSuperAdmin = await _context.LabSuperAdmins.FirstOrDefaultAsync(a => a.Id == labAdminId);
                if (labSuperAdmin == null)
                {
                    _logger.LogWarning("Branch deletion failed: No Super Admin found.");
                    return BadRequest(ApiResponseFactory.Fail("No Super Admin found."));
                }

                var loggedInLab = await _context.LabSignups.FirstOrDefaultAsync(l => l.Id == labId);
                if (loggedInLab == null)
                {
                    _logger.LogWarning("Branch deletion failed: Lab with ID {LabId} not found.", labId);
                    return NotFound(ApiResponseFactory.Fail($"Lab with ID {labId} not found."));
                }

                int mainLabId = loggedInLab.LabReference == 0 ? labId : loggedInLab.LabReference;

                var branch = await _context.LabSignups.FirstOrDefaultAsync(l => l.Id == branchId);
                if (branch == null)
                {
                    _logger.LogWarning("Branch deletion failed: Branch with ID {BranchId} not found.", branchId);
                    return NotFound(ApiResponseFactory.Fail($"Branch with ID {branchId} not found."));
                }

                if (branch.LabReference == 0)
                {
                    _logger.LogWarning("Branch deletion failed: Attempted to delete main lab {BranchId}.", branchId);
                    return BadRequest(ApiResponseFactory.Fail("Cannot delete the main lab."));
                }

                if (branch.LabReference != mainLabId)
                {
                    _logger.LogWarning("Branch deletion failed: Branch ID {BranchId} does not belong to the main lab {MainLabId}.", branchId, mainLabId);
                    return Unauthorized(ApiResponseFactory.Fail($"Branch with ID {branchId} does not belong to your lab."));
                }

                if (branch.DeletedBy != 0)
                {
                    _logger.LogWarning("Branch deletion failed: Branch ID {BranchId} has already been deleted.", branchId);
                    return BadRequest(ApiResponseFactory.Fail("This branch has already been deleted."));
                }

                branch.DeletedBy = labSuperAdmin.Id;
                await _context.SaveChangesAsync();

                var deletedByUser = await _context.UserDetails.FirstOrDefaultAsync(u => u.user_id == labSuperAdmin.UserId);
                if (deletedByUser == null)
                {
                    _logger.LogWarning("Branch deletion failed: No user found for Super Admin ID {SuperAdminId}.", labSuperAdmin.Id);
                    return BadRequest(ApiResponseFactory.Fail("No user found."));
                }

                var deletedByUserName = $"{deletedByUser.user_firstname} {deletedByUser.user_lastname}";

                var response = new
                {
                    BranchId = branch.Id,
                    BranchName = branch.LabName,
                    DeletedBy = deletedByUserName
                };

                _logger.LogInformation("Branch ID {BranchId} successfully deleted by {DeletedBy}.", branch.Id, deletedByUserName);
                return Ok(ApiResponseFactory.Success(response, $"Branch deleted successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Branch deletion failed due to an unexpected error.");
                return StatusCode(500, ApiResponseFactory.Fail($"An unexpected error occurred: {ex.Message}"));
            }
        }





        // Method to fetch Area, City & State Based on Pincode
        [HttpGet("labs/branches/{pincode}")]
        [Authorize]
        public async Task<IActionResult> GetLocation(string pincode)
        {
            HttpContext.Items["Log-Category"] = "Location Retrieval";

            _logger.LogInformation("Received request to fetch location details for Pincode: {Pincode}", pincode);

            if (string.IsNullOrEmpty(pincode))
            {
                _logger.LogWarning("Location retrieval failed: Pincode is missing.");
                return BadRequest(ApiResponseFactory.Fail("Pincode is required."));
            }

            try
            {
                var location = await _locationService.GetLocationDetails(pincode);

                if (location == null)
                {
                    _logger.LogWarning("Location retrieval failed: No details found for Pincode {Pincode}", pincode);
                    return NotFound(ApiResponseFactory.Fail($"No location details found for pincode {pincode}."));
                }

                _logger.LogInformation("Successfully fetched location details for Pincode {Pincode}. Returning response.", pincode);
                return Ok(ApiResponseFactory.Success(new { success = true, location }, "Location details fetched successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Location retrieval failed due to an unexpected error for Pincode {Pincode}", pincode);
                return StatusCode(500, ApiResponseFactory.Fail($"An unexpected error occurred: {ex.Message}"));
            }
        }






        // Get Deleted Branches
        [HttpGet("labs/deleted-branches")]
        [Authorize]
        public async Task<IActionResult> GetDeletedBranches()
        {
            HttpContext.Items["Log-Category"] = "Lab Management";

            _logger.LogInformation("Received request to fetch deleted branches for Lab ID: {LabId}", User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value);

            try
            {
                var labIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
                if (labIdClaim == null || !int.TryParse(labIdClaim.Value, out int labId))
                {
                    _logger.LogWarning("Deleted branch retrieval failed: Invalid or missing LabId claim.");
                    return Unauthorized(ApiResponseFactory.Fail("Invalid or missing LabId claim."));
                }

                if (!await _labAuthorizationService.IsLabAuthorized(labId, User))
                {
                    _logger.LogWarning("Deleted branch retrieval failed: Unauthorized access for Lab ID {LabId}", labId);
                    return Unauthorized(ApiResponseFactory.Fail("Permission denied. You can only manage your main lab or its branches."));
                }

                var loggedInLab = await _context.LabSignups.FirstOrDefaultAsync(l => l.Id == labId);
                if (loggedInLab == null)
                {
                    _logger.LogWarning("Deleted branch retrieval failed: Lab ID {LabId} not found.", labId);
                    return NotFound(ApiResponseFactory.Fail($"Lab with ID {labId} not found."));
                }

                int mainLabId = loggedInLab.LabReference == 0 ? labId : loggedInLab.LabReference;

                _logger.LogInformation("Fetching deleted branches for Main Lab ID: {MainLabId}", mainLabId);
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
                                         join ud in _context.UserDetails on sa.UserId equals ud.user_id
                                         where sa.Id == l.DeletedBy
                                         select ud.user_firstname + " " + ud.user_lastname)
                                        .FirstOrDefault(),
                        DeletedByUserRole = "Super Admin"
                    })
                    .ToListAsync();

                _logger.LogInformation("Total deleted branches found: {BranchCount}", deletedBranches.Count);

                if (!deletedBranches.Any())
                {
                    _logger.LogWarning("No deleted branches found for Lab ID {LabId}.", labId);
                    return NotFound(ApiResponseFactory.Fail($"No deleted branches found."));
                }

                _logger.LogInformation("Successfully fetched deleted branches for Lab ID {LabId}. Returning response.", labId);
                return Ok(ApiResponseFactory.Success(deletedBranches, "Deleted branches fetched successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Deleted branch retrieval failed due to an unexpected error.");
                return StatusCode(500, ApiResponseFactory.Fail($"An unexpected error occurred: {ex.Message}"));
            }
        }





        // Revert Branches
        [HttpPatch("labs/revert-branch")]
        [Authorize (Policy = "SuperAdminOrAdminPolicy")]
        public async Task<IActionResult> RevertDeletedBranch([FromBody] RevertBranch dto)
        {
            HttpContext.Items["Log-Category"] = "Lab Management";

            _logger.LogInformation("Received request to revert deleted branch. Branch ID: {BranchId}", dto.Id);

            try
            {
                var labIdClaim = User.FindFirst("UserId")?.Value;
                if (labIdClaim == null || !int.TryParse(labIdClaim, out int requestLabId))
                {
                    _logger.LogWarning("Revert failed: Invalid or missing LabId claim.");
                    return Unauthorized(ApiResponseFactory.Fail("Invalid or missing LabId claim."));
                }

                var revertedByIdClaim = User.Claims.FirstOrDefault(c => c.Type == "LabAdminId");
                var revertedByRoleClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);

                if (revertedByIdClaim == null || revertedByIdClaim == null ||
                    !int.TryParse(revertedByIdClaim.Value, out int revertedById))
                {
                    _logger.LogWarning("Member deletion failed: Missing or invalid deletion claims (LabAdminId/Role).");
                    return Unauthorized(ApiResponseFactory.Fail("Missing or invalid deletion claims (LabAdminId/Role)."));
                }

                var labSuperAdmin = await _context.LabSuperAdmins.FirstOrDefaultAsync(a => a.Id == revertedById);
                if (labSuperAdmin == null)
                {
                    _logger.LogWarning("Branch deletion failed: No Super Admin found.");
                    return BadRequest(ApiResponseFactory.Fail("No Super Admin found."));
                }

                var loggedInLab = await _context.LabSignups.FirstOrDefaultAsync(l => l.Id == requestLabId);
                if (loggedInLab == null)
                {
                    _logger.LogWarning("Promotion failed: Lab ID {LabId} not found.", requestLabId);
                    return BadRequest(ApiResponseFactory.Fail("Lab not found"));
                }

                int mainLabId = loggedInLab.LabReference == 0 ? requestLabId : loggedInLab.LabReference;

                var branchIds = await _context.LabSignups
                    .Where(l => l.LabReference == mainLabId)
                    .Select(l => l.Id)
                    .ToListAsync();

                branchIds.Add(mainLabId);

                if (!await _labAuthorizationService.IsLabAuthorized(requestLabId, User))
                {
                    _logger.LogWarning("Revert failed: Unauthorized access for Lab ID {LabId}.", requestLabId);
                    return Unauthorized(ApiResponseFactory.Fail("Permission denied. You can only manage your main lab or its branches."));
                }


                var branch = await _context.LabSignups.FirstOrDefaultAsync(l => l.Id == dto.Id && (branchIds.Contains(l.Id) || l.Id == mainLabId) && l.DeletedBy != 0);

                if (branch == null)
                {
                    _logger.LogWarning("Revert failed: Branch ID {BranchId} is not marked as deleted.", dto.Id);
                    return NotFound(ApiResponseFactory.Fail("Branch not found or not deleted."));
                }

                var revertedByUser = await _context.UserDetails.FirstOrDefaultAsync(u => u.user_id == labSuperAdmin.UserId);
                if (revertedByUser == null)
                {
                    _logger.LogWarning("Branch deletion failed: No user found for Super Admin ID {SuperAdminId}.", labSuperAdmin.Id);
                    return BadRequest(ApiResponseFactory.Fail("No user found."));
                }

                var revertedBy = $"{revertedByUser.user_firstname} {revertedByUser.user_lastname}";

                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    branch.DeletedBy = 0;

                    _context.Update(branch);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var response = new
                    {
                        BranchId = dto.Id,
                        RevertedBy = revertedBy,
                        RevertedByRole = revertedByRoleClaim?.Value
                    };

                    _logger.LogInformation("Successfully reverted Branch ID {BranchId} by User ID {RevertedBy}, Role: {RevertedByRole}",
                        response.BranchId, response.RevertedBy, response.RevertedByRole);

                    return Ok(ApiResponseFactory.Success(response, "Branch reverted successfully."));

                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Revert failed: Error occurred while reverting Branch ID {BranchId}.", dto.Id);
                    return StatusCode(500, ApiResponseFactory.Fail("An unexpected error occurred while reverting the branch."));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred in Branch Revert API.");
                return StatusCode(500, ApiResponseFactory.Fail("Unexpected error."));
            }
        }
    }
}
