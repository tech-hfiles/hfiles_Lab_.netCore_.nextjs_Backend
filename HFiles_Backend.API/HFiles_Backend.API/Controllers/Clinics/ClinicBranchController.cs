using HFiles_Backend.API.Interfaces;
using HFiles_Backend.API.Services;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Clinics.Branch;
using HFiles_Backend.Application.DTOs.Labs;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Data;
using HFiles_Backend.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace HFiles_Backend.API.Controllers.Clinics
{
    [Route("api/")]
    [ApiController]
    public class ClinicBranchController(
        EmailService emailService,
        ILogger<ClinicBranchController> logger,
        IClinicRepository clinicRepository,
        IClinicSuperAdminRepository clinicSuperAdminRepository,
        IClinicMemberRepository clinicMemberRepository,
        IUserRepository userRepository,
        IClinicBranchRepository clinicBranchRepository,
        IClinicAuthorizationService clinicAuthorizationService,
        AppDbContext context,
        LocationService locationService
    ) : ControllerBase
    {
        private readonly EmailService _emailService = emailService;
        private readonly ILogger<ClinicBranchController> _logger = logger;
        private readonly IClinicRepository _clinicRepository = clinicRepository;
        private readonly IClinicSuperAdminRepository _clinicSuperAdminRepository = clinicSuperAdminRepository;
        private readonly IClinicMemberRepository _clinicMemberRepository = clinicMemberRepository;
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IClinicBranchRepository _clinicBranchRepository = clinicBranchRepository;
        private readonly IClinicAuthorizationService _clinicAuthorizationService = clinicAuthorizationService;
        private readonly AppDbContext _context = context;
        private readonly LocationService _locationService = locationService;


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





        // Create Clinic Branch
        [HttpPost("clinics/branches")]
        [Authorize(Policy = "SuperAdminOrAdminPolicy")]
        public async Task<IActionResult> CreateClinicBranch(
        [FromBody] ClinicBranch dto,
        [FromServices] OtpVerificationStore otpStore)
        {
            HttpContext.Items["Log-Category"] = "Clinic Management";
            _logger.LogInformation("Received request to create a new clinic branch. Payload: {@dto}", dto);

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

            var transaction = await _clinicRepository.BeginTransactionAsync();
            var committed = false;

            try
            {
                var clinicIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
                if (!int.TryParse(clinicIdClaim, out int clinicId))
                {
                    _logger.LogWarning("Branch creation failed: Invalid or missing ClinicId claim.");
                    return Unauthorized(ApiResponseFactory.Fail("Invalid or missing ClinicId claim."));
                }

                if (!await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User))
                {
                    _logger.LogWarning("Branch creation failed: Clinic ID {ClinicId} is not authorized.", clinicId);
                    return Unauthorized(ApiResponseFactory.Fail("Permission denied. You can only manage your main clinic or its branches."));
                }

                if (await _clinicBranchRepository.IsEmailRegisteredAsync(dto.Email))
                {
                    _logger.LogWarning("Branch creation failed: Email {Email} is already registered.", dto.Email);
                    return BadRequest(ApiResponseFactory.Fail("Email already registered."));
                }

                var parentClinic = await _clinicBranchRepository.GetParentClinicAsync(clinicId);
                if (parentClinic == null)
                {
                    _logger.LogWarning("Branch creation failed: Parent clinic with ID {ClinicId} not found.", clinicId);
                    return Unauthorized(ApiResponseFactory.Fail("Parent clinic not found."));
                }

                var epochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var last6Epoch = epochTime % 1_000_000;
                var prefix = dto.ClinicName.Length >= 3 ? dto.ClinicName[..3].ToUpperInvariant() : dto.ClinicName.ToUpperInvariant();
                var randomDigits = Random.Shared.Next(1000, 9999);
                var hfid = $"HF{last6Epoch}{prefix}{randomDigits}";

                var newBranch = new ClinicSignup
                {
                    ClinicName = dto.ClinicName,
                    Email = dto.Email,
                    PhoneNumber = dto.PhoneNumber,
                    Pincode = dto.Pincode,
                    PasswordHash = parentClinic.PasswordHash,
                    HFID = hfid,
                    CreatedAtEpoch = epochTime,
                    ClinicReference = clinicId
                };

                _clinicBranchRepository.AddBranch(newBranch);
                await _clinicRepository.SaveChangesAsync();
                await transaction.CommitAsync();
                committed = true;

                string? createdByName = await ResolveUsernameFromClaims(HttpContext, _context);
                if (string.IsNullOrWhiteSpace(createdByName))
                    return Unauthorized(ApiResponseFactory.Fail("Unable to resolve creator identity."));

                var response = new
                {
                    newBranch.Id,
                    newBranch.HFID,
                    NotificationContext = new
                    {
                        BranchName = newBranch.ClinicName,
                        BranchEmail = newBranch.Email,
                        HFID = newBranch.HFID,
                        createdByName = createdByName
                    },
                    NotificationMessage = $"Branch {newBranch.ClinicName} created successfully by {createdByName}."
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
            finally
            {
                if (!committed && transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.RollbackAsync();
                }
            }
        }





        // Verify OTP for clinic Branch
        [HttpPost("clinics/branches/verify/otp")]
        [Authorize(Policy = "SuperAdminOrAdminPolicy")]
        [Authorize]
        public async Task<IActionResult> VerifyClinicBranchOtp(
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

            var transaction = await _clinicRepository.BeginTransactionAsync();
            var committed = false;

            try
            {
                var now = DateTime.UtcNow;
                var otpEntry = await _clinicRepository.GetLatestOtpAsync(dto.Email);

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

                await _clinicRepository.RemoveExpiredOtpsAsync(dto.Email, now);
                await _clinicRepository.RemoveOtpAsync(otpEntry);
                await _clinicRepository.SaveChangesAsync();
                await transaction.CommitAsync();
                committed = true;

                otpStore.StoreVerifiedOtp(dto.Email, "create_branch");

                _logger.LogInformation("OTP verification successful for Email {Email}", dto.Email);
                return Ok(ApiResponseFactory.Success("OTP successfully verified."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OTP verification failed for Email {Email}", dto.Email);
                return StatusCode(500, ApiResponseFactory.Fail($"An unexpected error occurred: {ex.Message}"));
            }
            finally
            {
                if (!committed && transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.RollbackAsync();
                }
            }
        }





        // Retrives main clinic and its branches
        [HttpGet("clinics")]
        [Authorize]
        public async Task<IActionResult> GetClinicBranches()
        {
            HttpContext.Items["Log-Category"] = "Clinic Management";
            var userClinicIdStr = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
            _logger.LogInformation("Fetching all clinics and branches for Clinic ID: {ClinicId}", userClinicIdStr);

            if (!int.TryParse(userClinicIdStr, out int clinicId))
            {
                _logger.LogWarning("Invalid ClinicId claim.");
                return Unauthorized(ApiResponseFactory.Fail("Invalid or missing ClinicId claim."));
            }

            var transaction = await _clinicRepository.BeginTransactionAsync();
            var committed = false;

            try
            {
                if (!await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User))
                {
                    _logger.LogWarning("Unauthorized access for Clinic ID {ClinicId}", clinicId);
                    return Unauthorized(ApiResponseFactory.Fail("Permission denied. You can only access your main clinic or its branches."));
                }

                var loggedInClinic = await _clinicBranchRepository.GetClinicByIdAsync(clinicId);
                if (loggedInClinic == null)
                {
                    _logger.LogWarning("Clinic ID {ClinicId} not found.", clinicId);
                    return NotFound(ApiResponseFactory.Fail($"Clinic with ID {clinicId} not found."));
                }

                int mainClinicId = loggedInClinic.ClinicReference == 0 ? clinicId : loggedInClinic.ClinicReference;

                var mainClinic = await _clinicBranchRepository.GetMainClinicAsync(mainClinicId);
                if (mainClinic == null)
                {
                    _logger.LogWarning("Main clinic ID {MainClinicId} not found.", mainClinicId);
                    return NotFound(ApiResponseFactory.Fail($"Main clinic with ID {mainClinicId} not found."));
                }

                _logger.LogInformation("Fetching branches for Main Clinic ID: {MainClinicId}", mainClinicId);
                var branches = await _clinicBranchRepository.GetBranchesAsync(mainClinicId);

                _logger.LogInformation("Total branches found: {Count}", branches.Count);

                var result = new List<ClinicInfo>
                {
                    new()
                    {
                        ClinicId = mainClinic.Id,
                        ClinicName = mainClinic.ClinicName,
                        HFID = mainClinic.HFID,
                        Email = mainClinic.Email ?? "No email available",
                        PhoneNumber = mainClinic.PhoneNumber ?? "No phone number available",
                        Pincode = mainClinic.Pincode ?? "No pincode available",
                        Location = await _locationService.GetLocationDetails(mainClinic.Pincode),
                        Address = mainClinic.Address ?? "No address available",
                        ProfilePhoto = mainClinic.ProfilePhoto ?? "No image preview available",
                        ClinicType = "mainClinic"
                    }
                };

                var branchTasks = branches.Select(async branch => new ClinicInfo
                {
                    ClinicId = branch.Id,
                    ClinicName = branch.ClinicName,
                    HFID = branch.HFID,
                    Email = branch.Email ?? "No email available",
                    PhoneNumber = branch.PhoneNumber ?? "No phone number available",
                    Pincode = branch.Pincode ?? "No pincode available",
                    Location = await _locationService.GetLocationDetails(branch.Pincode),
                    Address = branch.Address ?? "No address available",
                    ProfilePhoto = branch.ProfilePhoto ?? "No image preview available",
                    ClinicType = "branch"
                });

                result.AddRange(await Task.WhenAll(branchTasks));

                var response = new
                {
                    ClinicCounts = result.Count,
                    Clinics = result
                };

                await transaction.CommitAsync();
                committed = true;

                _logger.LogInformation("Successfully returning all clinics and branches.");
                return Ok(ApiResponseFactory.Success(response, "Branches fetched successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while fetching clinic branches.");
                return StatusCode(500, ApiResponseFactory.Fail($"Unexpected error: {ex.Message}"));
            }
            finally
            {
                if (!committed && transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.RollbackAsync();
                }
            }
        }





        // Soft Delete Branch
        [HttpPut("clinics/branches/{branchId}")]
        [Authorize(Policy = "SuperAdminPolicy")]
        public async Task<IActionResult> DeleteClinicBranch(
        [FromRoute] int branchId)
        {
            HttpContext.Items["Log-Category"] = "Clinic Management";
            HttpContext.Items["BranchId"] = branchId;

            _logger.LogInformation("Received request to delete Clinic Branch ID: {BranchId}", branchId);

            var clinicAdminIdStr = User.FindFirst("ClinicAdminId")?.Value;
            var clinicIdStr = User.FindFirst("UserId")?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            if (role is "Admin" or "Member")
            {
                _logger.LogWarning("Branch deletion failed: Unauthorized role {Role} attempted deletion.", role);
                return Unauthorized(ApiResponseFactory.Fail($"{role} has no permissions."));
            }

            if (!int.TryParse(clinicAdminIdStr, out int clinicAdminId) || !int.TryParse(clinicIdStr, out int clinicId))
            {
                _logger.LogWarning("Branch deletion failed: Invalid ClinicId or ClinicAdminId claim.");
                return Unauthorized(ApiResponseFactory.Fail("Invalid or missing clinic claims."));
            }

            var transaction = await _clinicRepository.BeginTransactionAsync();
            var committed = false;

            try
            {
                if (!await _clinicAuthorizationService.IsClinicAuthorized(branchId, User))
                {
                    _logger.LogWarning("Branch deletion failed: Unauthorized attempt to delete Branch ID {BranchId}", branchId);
                    return Unauthorized(ApiResponseFactory.Fail("You can only manage your main clinic or its branches."));
                }

                var clinicSuperAdmin = await _clinicBranchRepository.GetSuperAdminByIdAsync(clinicAdminId);
                if (clinicSuperAdmin == null)
                    return BadRequest(ApiResponseFactory.Fail("No Super Admin found."));

                var loggedInClinic = await _clinicBranchRepository.GetClinicByIdAsync(clinicId);
                if (loggedInClinic == null)
                    return NotFound(ApiResponseFactory.Fail($"Clinic with ID {clinicId} not found."));

                int mainClinicId = loggedInClinic.ClinicReference == 0 ? clinicId : loggedInClinic.ClinicReference;

                var branch = await _clinicBranchRepository.GetClinicByIdAsync(branchId);
                if (branch == null)
                    return NotFound(ApiResponseFactory.Fail($"Branch with ID {branchId} not found."));

                if (branch.ClinicReference == 0)
                    return BadRequest(ApiResponseFactory.Fail("Cannot delete the main clinic."));

                if (branch.ClinicReference != mainClinicId)
                    return Unauthorized(ApiResponseFactory.Fail($"Branch with ID {branchId} does not belong to your clinic."));

                if (branch.DeletedBy != 0)
                    return BadRequest(ApiResponseFactory.Fail("This branch has already been deleted."));

                branch.DeletedBy = clinicSuperAdmin.Id;
                _clinicBranchRepository.UpdateClinic(branch);
                await _clinicRepository.SaveChangesAsync();
                await transaction.CommitAsync();
                committed = true;

                var deletedByUser = await _clinicBranchRepository.GetUserByIdAsync(clinicSuperAdmin.UserId);
                if (deletedByUser == null)
                    return BadRequest(ApiResponseFactory.Fail("No user found."));

                string? deletedByName = await ResolveUsernameFromClaims(HttpContext, _context);
                if (string.IsNullOrWhiteSpace(deletedByName))
                    return Unauthorized(ApiResponseFactory.Fail("Unable to resolve creator identity."));

                var response = new
                {
                    BranchId = branch.Id,
                    BranchName = branch.ClinicName,
                    DeletedBy = deletedByName,
                    NotificationContext = new
                    {
                        BranchId = branch.Id,
                        BranchName = branch.ClinicName,
                        DeletedBy = deletedByName
                    },
                    NotificationMessage = $"Branch '{branch.ClinicName}' was deleted by {deletedByName}."
                };

                _logger.LogInformation("Clinic Branch ID {BranchId} successfully deleted by {DeletedBy}.", branch.Id, deletedByName);
                return Ok(ApiResponseFactory.Success(response, $"Branch deleted successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Branch deletion failed due to an unexpected error.");
                return StatusCode(500, ApiResponseFactory.Fail($"An unexpected error occurred: {ex.Message}"));
            }
            finally
            {
                if (!committed && transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.RollbackAsync();
                }
            }
        }





        // Get Pincode of a branch
        [HttpGet("clinics/branches/{pincode}")]
        [Authorize]
        public async Task<IActionResult> GetClinicLocationByPincode(
        [FromRoute][RegularExpression(@"^\d{6}$", ErrorMessage = "Pincode must be a 6-digit number.")] string pincode)
        {
            HttpContext.Items["Log-Category"] = "Location Retrieval";
            _logger.LogInformation("Fetching location details for Pincode: {Pincode}", pincode);

            if (string.IsNullOrWhiteSpace(pincode))
            {
                _logger.LogWarning("Pincode missing.");
                return BadRequest(ApiResponseFactory.Fail("Pincode is required."));
            }

            var transaction = await _clinicRepository.BeginTransactionAsync();
            var committed = false;

            try
            {
                var location = await _locationService.GetLocationDetails(pincode).ConfigureAwait(false);
                if (location == null)
                {
                    _logger.LogWarning("No details found for Pincode {Pincode}", pincode);
                    return NotFound(ApiResponseFactory.Fail($"No location details found for pincode {pincode}."));
                }

                await transaction.CommitAsync();
                committed = true;

                _logger.LogInformation("Location fetched for Pincode {Pincode}", pincode);
                return Ok(ApiResponseFactory.Success(new { success = true, location }, "Location details fetched successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during location fetch for Pincode {Pincode}", pincode);
                return StatusCode(500, ApiResponseFactory.Fail($"An unexpected error occurred: {ex.Message}"));
            }
            finally
            {
                if (!committed && transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.RollbackAsync();
                }
            }
        }





        // Retrieves Soft Deleted Branches
        [HttpGet("clinics/deleted-branches")]
        [Authorize]
        public async Task<IActionResult> GetDeletedClinicBranches([FromServices] ClinicBranchRepository clinicBranchRepository)
        {
            HttpContext.Items["Log-Category"] = "Clinic Management";

            var userIdStr = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
            _logger.LogInformation("Fetching deleted branches for Clinic User ID: {UserId}", userIdStr);

            if (!int.TryParse(userIdStr, out int userId))
            {
                _logger.LogWarning("Invalid or missing UserId claim.");
                return Unauthorized(ApiResponseFactory.Fail("Invalid or missing UserId claim."));
            }

            if (!await _clinicAuthorizationService.IsClinicAuthorized(userId, User))
            {
                _logger.LogWarning("Unauthorized access for Clinic User ID {UserId}", userId);
                return Unauthorized(ApiResponseFactory.Fail("Permission denied. You can only manage your main clinic or its branches."));
            }

            var clinic = await _clinicBranchRepository.GetClinicByUserIdAsync(userId);
            if (clinic == null)
            {
                _logger.LogWarning("Clinic not found for User ID {UserId}", userId);
                return Ok(ApiResponseFactory.Fail($"Clinic not found for user ID {userId}."));
            }

            int mainClinicId = clinic.ClinicReference == 0 ? clinic.Id : clinic.ClinicReference;
            _logger.LogInformation("Retrieving deleted branches for Main Clinic ID: {MainClinicId}", mainClinicId);

            var transaction = await _clinicRepository.BeginTransactionAsync();
            var committed = false;

            try
            {
                var deletedBranches = await clinicBranchRepository.GetDeletedBranchesAsync(mainClinicId);

                foreach (var branch in deletedBranches)
                {
                    branch.DeletedByUser = await _userRepository.GetFullNameBySuperAdminIdAsync(branch.DeletedBy);
                    branch.DeletedByUserRole = "Super Admin";
                }

                await transaction.CommitAsync();
                committed = true;

                //if (!deletedBranches.Any())
                //{
                //    _logger.LogWarning("No deleted branches found for Clinic ID {ClinicId}", clinic.Id);
                //    return NotFound(ApiResponseFactory.Fail("No deleted branches found."));
                //}

                _logger.LogInformation("Returning {Count} deleted branches for Clinic ID {ClinicId}", deletedBranches.Count, clinic.Id);
                return Ok(ApiResponseFactory.Success(deletedBranches, "Deleted branches fetched successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while fetching deleted branches.");
                return StatusCode(500, ApiResponseFactory.Fail($"An unexpected error occurred: {ex.Message}"));
            }
            finally
            {
                if (!committed && transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.RollbackAsync();
                }
            }
        }





        // Revert Soft Deleted Branch
        [HttpPatch("clinics/revert-branch")]
        [Authorize(Policy = "SuperAdminOrAdminPolicy")]
        public async Task<IActionResult> RevertDeletedClinicBranch(
        [FromBody] RevertBranch dto)
        {
            HttpContext.Items["Log-Category"] = "Clinic Management";
            _logger.LogInformation("Revert request received for Branch ID: {BranchId}", dto.Id);

            var userIdStr = User.FindFirst("UserId")?.Value;
            var adminIdStr = User.FindFirst("ClinicAdminId")?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            if (!int.TryParse(userIdStr, out int userId) || !int.TryParse(adminIdStr, out int adminId))
            {
                _logger.LogWarning("Revert failed: Invalid UserId or ClinicAdminId claims.");
                return Unauthorized(ApiResponseFactory.Fail("Invalid or missing clinic claims."));
            }

            if (!await _clinicAuthorizationService.IsClinicAuthorized(userId, User))
            {
                _logger.LogWarning("Revert failed: Unauthorized clinic access for User ID {UserId}", userId);
                return Unauthorized(ApiResponseFactory.Fail("Unauthorized clinic access."));
            }

            var clinic = await _clinicBranchRepository.GetClinicByUserIdAsync(userId);
            if (clinic == null)
                return BadRequest(ApiResponseFactory.Fail("Clinic not found."));

            int mainClinicId = clinic.ClinicReference == 0 ? clinic.Id : clinic.ClinicReference;

            var branchIds = await _clinicBranchRepository.GetBranchIdsForMainClinicAsync(mainClinicId);
            branchIds.Add(mainClinicId);

            var branch = await _clinicBranchRepository.GetDeletedBranchByIdAsync(dto.Id, branchIds);
            if (branch == null)
            {
                _logger.LogWarning("Revert failed: Branch ID {BranchId} not found or not deleted.", dto.Id);
                return NotFound(ApiResponseFactory.Fail("Branch not found or not deleted."));
            }

            var admin = await _clinicBranchRepository.GetSuperAdminByIdAsync(adminId);
            if (admin == null)
                return BadRequest(ApiResponseFactory.Fail("Clinic Admin not found."));

            var revertedByUser = await _clinicBranchRepository.GetUserByIdAsync(admin.UserId);
            if (revertedByUser == null)
                return BadRequest(ApiResponseFactory.Fail("Admin user details not found."));

            string? revertedByName = await ResolveUsernameFromClaims(HttpContext, _context);
            if (string.IsNullOrWhiteSpace(revertedByName))
                return Unauthorized(ApiResponseFactory.Fail("Unable to resolve creator identity."));

            var transaction = await _clinicRepository.BeginTransactionAsync();
            var committed = false;

            try
            {
                branch.DeletedBy = 0;
                await _clinicBranchRepository.UpdateBranchAsync(branch);
                await transaction.CommitAsync();
                committed = true;

                var response = new
                {
                    BranchId = dto.Id,
                    BranchName = branch.ClinicName,
                    RevertedBy = revertedByName,
                    RevertedByRole = role,
                    NotificationContext = new
                    {
                        BranchId = dto.Id,
                        BranchName = branch.ClinicName,
                        RevertedBy = revertedByName,
                        RevertedByRole = role
                    },
                    NotificationMessage = $"Branch '{branch.ClinicName}' was restored by {revertedByName} ({role})."
                };

                _logger.LogInformation("Branch ID {BranchId} reverted by {RevertedBy} ({Role})", dto.Id, revertedByName, role);
                return Ok(ApiResponseFactory.Success(response, "Branch reverted successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during branch revert for ID {BranchId}.", dto.Id);
                return StatusCode(500, ApiResponseFactory.Fail("Unexpected error occurred while reverting the branch."));
            }
            finally
            {
                if (!committed && transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.RollbackAsync();
                }
            }
        }
    }
}
