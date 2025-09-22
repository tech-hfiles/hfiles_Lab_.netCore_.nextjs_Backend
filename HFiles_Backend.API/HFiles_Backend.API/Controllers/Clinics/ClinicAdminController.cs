using HFiles_Backend.API.Services;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Clinics.SuperAdmin;
using HFiles_Backend.Application.DTOs.Labs;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace HFiles_Backend.API.Controllers.Clinics
{
    [Route("api/")]
    [ApiController]
    public class ClinicAdminController(
         ILogger<ClinicAdminController> logger,
         IClinicRepository clinicRepository,
         IClinicSuperAdminRepository clinicSuperAdminRepository,
         JwtTokenService jwtTokenService,
         IPasswordHasher<ClinicSuperAdmin> passwordHasher,
         IClinicMemberRepository clinicMemberRepository,
         IUserRepository userRepository
        ) : ControllerBase
    {
        private readonly ILogger<ClinicAdminController> _logger = logger;
        private readonly IClinicRepository _clinicRepository = clinicRepository;
        private readonly IClinicSuperAdminRepository _clinicSuperAdminRepository = clinicSuperAdminRepository;
        private readonly JwtTokenService _jwtTokenService = jwtTokenService;
        private readonly IPasswordHasher<ClinicSuperAdmin> _passwordHasher = passwordHasher;
        private readonly IClinicMemberRepository _clinicMemberRepository = clinicMemberRepository;
        private readonly IUserRepository _userRepository = userRepository;





        // Add Super Admin
        [HttpPost("clinics/super-admins")]
        public async Task<IActionResult> CreateClinicSuperAdmin([FromBody] CreateClinicSuperAdmin dto)
        {
            HttpContext.Items["Log-Category"] = "User Management";
            _logger.LogInformation("Received request to create Clinic Super Admin. Payload: {@dto}", dto);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            var transaction = await _clinicRepository.BeginTransactionAsync();

            try
            {
                var clinic = await _clinicRepository.GetByIdAndEmailAsync(dto.UserId, dto.Email);
                if (clinic == null)
                {
                    _logger.LogWarning("Invalid credentials: UserId={UserId}, Email={Email}", dto.UserId, dto.Email);
                    return NotFound(ApiResponseFactory.Fail("Invalid Credentials."));
                }

                if (clinic.IsSuperAdmin)
                {
                    _logger.LogWarning("Super Admin already exists for clinic {ClinicName}.", clinic.ClinicName);
                    return BadRequest(ApiResponseFactory.Fail($"A Super Admin already exists for the clinic {clinic.ClinicName}."));
                }

                var user = await _clinicSuperAdminRepository.GetUserByHFIDAsync(dto.HFID);
                if (user == null)
                {
                    _logger.LogWarning("No user found with HFID {HFID}.", dto.HFID);
                    return NotFound(ApiResponseFactory.Fail($"No user found with HFID {dto.HFID}."));
                }

                _logger.LogInformation("Creating Clinic Super Admin for user: {UserId}, Clinic: {ClinicId}", user.Id, dto.UserId);

                var newAdmin = new ClinicSuperAdmin
                {
                    UserId = user.Id,
                    ClinicId = dto.UserId,
                    PasswordHash = _passwordHasher.HashPassword(null!, dto.Password),
                    EpochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    IsMain = 1
                };

                await _clinicSuperAdminRepository.AddAsync(newAdmin);
                clinic.IsSuperAdmin = true;
                await _clinicRepository.UpdateAsync(clinic);
                await _clinicRepository.SaveChangesAsync();
                await transaction.CommitAsync();

                var tokenData = _jwtTokenService.GenerateToken(dto.UserId, dto.Email, 0, dto.Role, newAdmin.Id);

                _logger.LogInformation("Clinic Super Admin created. Token issued. Session ID: {SessionId}", tokenData.SessionId);

                var responseData = new
                {
                    username = $"{user.FirstName} {user.LastName}",
                    token = tokenData.Token,
                    sessionId = tokenData.SessionId
                };

                return Ok(ApiResponseFactory.Success(responseData, "Super Admin created successfully."));
            }
            finally
            {
                if (transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.RollbackAsync();
                }
            }
        }





        // User Login
        [HttpPost("clinics/users/login")]
        public async Task<IActionResult> ClinicUserLogin([FromBody] ClinicUserLogin dto)
        {
            HttpContext.Items["Log-Category"] = "Authentication";
            _logger.LogInformation("Received login request for HFID: {HFID}, Role: {Role}", dto.HFID, dto.Role);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            var transaction = await _clinicRepository.BeginTransactionAsync();

            try
            {
                var userDetails = await _clinicSuperAdminRepository.GetUserByHFIDAsync(dto.HFID);
                if (userDetails == null)
                {
                    _logger.LogWarning("Login failed: No user found with HFID {HFID}", dto.HFID);
                    return NotFound(ApiResponseFactory.Fail($"No Super Admin/Admin/Member found with HFID {dto.HFID}."));
                }

                var username = $"{userDetails.FirstName} {userDetails.LastName}";

                var clinicSignup = await _clinicRepository.GetByIdAndEmailAsync(dto.UserId, dto.Email);
                if (clinicSignup == null || clinicSignup.DeletedBy != 0)
                {
                    _logger.LogWarning("Login failed: Invalid credentials for UserId={UserId}, Email={Email}", dto.UserId, dto.Email);
                    return NotFound(ApiResponseFactory.Fail("Invalid Credentials."));
                }

                object response;

                if (dto.Role == "Super Admin")
                {
                    var admin = await _clinicSuperAdminRepository.GetSuperAdminAsync(userDetails.Id, dto.UserId, clinicSignup.ClinicReference);
                    if (admin == null)
                    {
                        _logger.LogWarning("Login failed: Not a Super Admin for HFID {HFID}", dto.HFID);
                        return Unauthorized(ApiResponseFactory.Fail($"The user with HFID: {dto.HFID} is not a Super Admin."));
                    }

                    if (string.IsNullOrWhiteSpace(admin.PasswordHash))
                    {
                        _logger.LogWarning("Login failed: Password not set for Super Admin {Username}", username);
                        return Unauthorized(ApiResponseFactory.Fail($"Password is not set for Super Admin: {username}"));
                    }

                    if (_passwordHasher.VerifyHashedPassword(admin, admin.PasswordHash, dto.Password) != PasswordVerificationResult.Success)
                    {
                        _logger.LogWarning("Login failed: Invalid password for Super Admin {Username}", username);
                        return Unauthorized(ApiResponseFactory.Fail("Invalid password."));
                    }

                    var (Token, SessionId) = _jwtTokenService.GenerateToken(dto.UserId, dto.Email, 0, dto.Role, admin.Id);
                    _logger.LogInformation("Super Admin login success: {Username} | Session ID: {SessionId}", username, SessionId);

                    response = new { Username = username, Token, SessionId };
                }
                else if (dto.Role == "Admin" || dto.Role == "Member")
                {
                    var member = await _clinicMemberRepository.GetMemberAsync(userDetails.Id, dto.UserId, dto.Role);
                    if (member == null)
                    {
                        _logger.LogWarning("Login failed: {Role} not found for HFID {HFID}", dto.Role, dto.HFID);
                        return Unauthorized(ApiResponseFactory.Fail($"{dto.Role} not found. Please register first."));
                    }

                    if (string.IsNullOrWhiteSpace(member.PasswordHash))
                    {
                        _logger.LogWarning("Login failed: Password not set for {Role} {Username}", dto.Role, username);
                        return Unauthorized(ApiResponseFactory.Fail($"Password is not set for {dto.Role}: {username}"));
                    }

                    if (_passwordHasher.VerifyHashedPassword(null!, member.PasswordHash, dto.Password) != PasswordVerificationResult.Success)
                    {
                        _logger.LogWarning("Login failed: Invalid password for {Role} {Username}", dto.Role, username);
                        return Unauthorized(ApiResponseFactory.Fail("Invalid password."));
                    }

                    var tokenData = _jwtTokenService.GenerateToken(dto.UserId, dto.Email, 0, dto.Role, member.Id);
                    _logger.LogInformation("{Role} login success: {Username} | Session ID: {SessionId}", dto.Role, username, tokenData.SessionId);

                    response = new { Username = username, tokenData.Token, tokenData.SessionId };
                }
                else
                {
                    _logger.LogWarning("Login failed: Invalid role specified {Role}", dto.Role);
                    return BadRequest(ApiResponseFactory.Fail("Invalid role specified."));
                }

                await transaction.CommitAsync();
                return Ok(ApiResponseFactory.Success(response, $"{username} successfully logged in."));
            }
            finally
            {
                if (transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.RollbackAsync();
                }
            }
        }





        // Get Clinic Users
        [HttpGet("clinics/{clinicId}/users")]
        public async Task<IActionResult> GetAllClinicUsers(
           [FromRoute][Range(1, int.MaxValue, ErrorMessage = "Clinic ID must be greater than zero.")] int clinicId,
           [FromServices] ClinicSuperAdminRepository clinicSuperAdminRepository,
           [FromServices] ClinicMemberRepository clinicMemberRepository)
        {
            HttpContext.Items["Log-Category"] = "User Retrieval";
            _logger.LogInformation("Received request to fetch all users for Clinic ID: {ClinicId}", clinicId);

            // Extract claims from JWT token
            var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
            var clinicAdminIdClaim = User.FindFirst("ClinicAdminId")?.Value;
            var userClinicIdClaim = User.FindFirst("UserId")?.Value;
            var tokenIssuedAtClaim = User.FindFirst("iat")?.Value; // Token issued at time
            var sessionIdClaim = User.FindFirst("SessionId")?.Value; // Session ID from token

            if (string.IsNullOrEmpty(roleClaim) || string.IsNullOrEmpty(clinicAdminIdClaim) || string.IsNullOrEmpty(userClinicIdClaim))
            {
                _logger.LogWarning("Missing required claims in JWT token");
                return Unauthorized(ApiResponseFactory.Fail("Invalid or missing authentication claims."));
            }

            if (!int.TryParse(clinicAdminIdClaim, out int clinicAdminId) || !int.TryParse(userClinicIdClaim, out int userClinicId))
            {
                _logger.LogWarning("Invalid format for ClinicAdminId or UserId claims");
                return Unauthorized(ApiResponseFactory.Fail("Invalid authentication claims format."));
            }

            var transaction = await _clinicRepository.BeginTransactionAsync();

            try
            {
                // Special validation for Super Admin tokens - they should only work immediately after promotion
                if (roleClaim == "Super Admin")
                {
                    // Check if token was issued recently (within last 10 seconds of promotion)
                    if (long.TryParse(tokenIssuedAtClaim, out long tokenIssuedAt))
                    {
                        var tokenIssueTime = DateTimeOffset.FromUnixTimeSeconds(tokenIssuedAt);
                        var currentTime = DateTimeOffset.UtcNow;
                        var timeDifference = currentTime - tokenIssueTime;

                        // Only allow Super Admin tokens that were issued in the last 10 seconds
                        // This means the token is only valid immediately after promotion
                        if (timeDifference.TotalSeconds > 10)
                        {
                            _logger.LogWarning("Super Admin token expired. Token issued at: {TokenIssueTime}, Current time: {CurrentTime}, Difference: {TimeDifference} seconds",
                                tokenIssueTime, currentTime, timeDifference.TotalSeconds);
                            return Unauthorized(ApiResponseFactory.Fail("Super Admin token has expired. Please login again with your new role."));
                        }

                        _logger.LogInformation("Super Admin token is valid. Token issued {TimeDifference} seconds ago", timeDifference.TotalSeconds);
                    }
                    else
                    {
                        _logger.LogWarning("Invalid or missing token issue time for Super Admin");
                        return Unauthorized(ApiResponseFactory.Fail("Invalid Super Admin token. Please login again."));
                    }

                    // Validate the super admin exists and is main
                    var superAdmin = await _clinicSuperAdminRepository.GetByIdAsync(clinicAdminId);
                    if (superAdmin == null || superAdmin.IsMain != 1)
                    {
                        _logger.LogWarning("Super Admin not found or not main admin. ClinicAdminId: {ClinicAdminId}", clinicAdminId);
                        return Unauthorized(ApiResponseFactory.Fail("Super Admin validation failed. Please login again."));
                    }

                    // Check if this super admin belongs to the correct clinic
                    var clinicEntrys = await _clinicRepository.GetByIdAsync(userClinicId);
                    if (clinicEntrys != null)
                    {
                        int mainClinicIds = clinicEntrys.ClinicReference == 0 ? userClinicId : clinicEntrys.ClinicReference;
                        if (superAdmin.ClinicId != mainClinicIds)
                        {
                            _logger.LogWarning("Super Admin clinic mismatch. Expected: {ExpectedClinicId}, Actual: {ActualClinicId}", mainClinicIds, superAdmin.ClinicId);
                            return Unauthorized(ApiResponseFactory.Fail("Super Admin clinic validation failed."));
                        }
                    }
                }
                else if (roleClaim == "Admin" || roleClaim == "Member")
                {
                    // Regular validation for Admin/Member
                    var member = await _clinicMemberRepository.GetByIdInBranchesAsync(clinicAdminId, new List<int> { userClinicId });
                    if (member == null || member.Role != roleClaim || member.DeletedBy != 0)
                    {
                        _logger.LogWarning("Role validation failed for {Role}. ClinicAdminId: {ClinicAdminId}", roleClaim, clinicAdminId);
                        return Unauthorized(ApiResponseFactory.Fail("Role validation failed. Your current role does not match the token claims."));
                    }
                }
                else
                {
                    _logger.LogWarning("Invalid role claim: {Role}", roleClaim);
                    return Unauthorized(ApiResponseFactory.Fail("Invalid role specified."));
                }

                // Proceed with original logic after role validation
                var clinicEntry = await _clinicRepository.GetByIdAsync(clinicId);
                if (clinicEntry == null)
                {
                    _logger.LogWarning("Clinic with ID {ClinicId} not found.", clinicId);
                    return NotFound(ApiResponseFactory.Fail($"Clinic with ID {clinicId} not found."));
                }

                int mainClinicId = clinicEntry.ClinicReference == 0 ? clinicId : clinicEntry.ClinicReference;

                // Additional authorization check - ensure user can access this clinic
                if (roleClaim == "Super Admin")
                {
                    // Super admin should be able to access main clinic and its branches
                    var userClinicEntry = await _clinicRepository.GetByIdAsync(userClinicId);
                    if (userClinicEntry != null)
                    {
                        int userMainClinicId = userClinicEntry.ClinicReference == 0 ? userClinicId : userClinicEntry.ClinicReference;
                        if (userMainClinicId != mainClinicId)
                        {
                            _logger.LogWarning("Super Admin trying to access unauthorized clinic. User Clinic: {UserClinicId}, Requested Clinic: {ClinicId}", userClinicId, clinicId);
                            return Unauthorized(ApiResponseFactory.Fail("You are not authorized to access this clinic's users."));
                        }
                    }
                }
                else if (roleClaim == "Admin" || roleClaim == "Member")
                {
                    // Admin/Member should only access their own clinic or branches within the same main clinic
                    var userClinicEntry = await _clinicRepository.GetByIdAsync(userClinicId);
                    if (userClinicEntry != null)
                    {
                        int userMainClinicId = userClinicEntry.ClinicReference == 0 ? userClinicId : userClinicEntry.ClinicReference;
                        if (userMainClinicId != mainClinicId)
                        {
                            _logger.LogWarning("{Role} trying to access unauthorized clinic. User Clinic: {UserClinicId}, Requested Clinic: {ClinicId}", roleClaim, userClinicId, clinicId);
                            return Unauthorized(ApiResponseFactory.Fail("You are not authorized to access this clinic's users."));
                        }
                    }
                }

                var superAdminDto = await clinicSuperAdminRepository.GetMainSuperAdminDtoAsync(mainClinicId);
                var memberDtos = await clinicMemberRepository.GetMemberDtosByClinicIdAsync(clinicId);

                _logger.LogInformation("Total Members found: {MemberCount}", memberDtos.Count);

                if (superAdminDto == null && memberDtos.Count == 0)
                {
                    _logger.LogWarning("No active admins or members found for Clinic ID {ClinicId}.", clinicId);
                    return NotFound(ApiResponseFactory.Fail($"No active admins or members found for Clinic ID {clinicId}."));
                }

                await transaction.CommitAsync();

                var response = new
                {
                    ClinicId = clinicId,
                    MainClinicId = mainClinicId,
                    UserCounts = memberDtos.Count + (superAdminDto != null ? 1 : 0),
                    SuperAdmin = superAdminDto,
                    Members = memberDtos
                };

                _logger.LogInformation("Successfully fetched users for Clinic ID {ClinicId}.", clinicId);
                return Ok(ApiResponseFactory.Success(response, "Users fetched successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching users for Clinic ID {ClinicId}", clinicId);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while fetching users."));
            }
            finally
            {
                if (transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.RollbackAsync();
                }
            }
        }





        // Promote admin to super admin
        // Promote admin to super admin
        [HttpPost("clinics/admin/promote")]
        [Authorize(Policy = "SuperAdminPolicy")]
        public async Task<IActionResult> PromoteClinicMemberToSuperAdmin([FromBody] PromoteAdmin dto)
        {
            HttpContext.Items["Log-Category"] = "Role Management";
            _logger.LogInformation("Received promotion request for Clinic Member ID: {MemberId}", dto.MemberId);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            var clinicAdminIdClaim = User.FindFirst("ClinicAdminId")?.Value;
            var clinicIdClaim = User.FindFirst("UserId")?.Value;

            if (!int.TryParse(clinicAdminIdClaim, out int clinicAdminId))
                return Unauthorized(ApiResponseFactory.Fail("Invalid or missing Super Admin Id in token."));

            if (!int.TryParse(clinicIdClaim, out int clinicId))
                return Unauthorized(ApiResponseFactory.Fail("Invalid or missing ClinicId in token."));

            var transaction = await _clinicRepository.BeginTransactionAsync();

            try
            {
                var loggedInClinic = await _clinicRepository.GetByIdAsync(clinicId);
                if (loggedInClinic == null)
                    return BadRequest(ApiResponseFactory.Fail("Clinic not found"));

                int mainClinicId = loggedInClinic.ClinicReference == 0 ? clinicId : loggedInClinic.ClinicReference;
                var branchIds = await _clinicRepository.GetBranchIdsAsync(mainClinicId);
                branchIds.Add(mainClinicId);

                var member = await _clinicMemberRepository.GetEligibleMemberForPromotionAsync(dto.MemberId, branchIds);
                if (member == null)
                    return NotFound(ApiResponseFactory.Fail("No clinic member found or not eligible for promotion."));

                member.DeletedBy = 1;
                _clinicMemberRepository.Update(member);

                var currentSuperAdmin = await _clinicSuperAdminRepository.GetMainSuperAdminAsync(mainClinicId);
                if (currentSuperAdmin == null)
                    return NotFound(ApiResponseFactory.Fail("No active Super Admin found."));

                currentSuperAdmin.IsMain = 0;
                _clinicSuperAdminRepository.Update(currentSuperAdmin);

                long epoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                var existedSuperAdmin = await _clinicSuperAdminRepository.GetExistingSuperAdminAsync(member.UserId, mainClinicId);
                var existedMember = await _clinicMemberRepository.GetDeletedMemberByUserIdAsync(currentSuperAdmin.UserId, branchIds);

                ClinicSuperAdmin newSuperAdmin;
                ClinicMember newClinicMember;

                if (existedSuperAdmin != null)
                {
                    existedSuperAdmin.IsMain = 1;
                    existedSuperAdmin.PasswordHash = member.PasswordHash;
                    existedSuperAdmin.EpochTime = epoch;
                    _clinicSuperAdminRepository.Update(existedSuperAdmin);
                    newSuperAdmin = existedSuperAdmin;
                }
                else
                {
                    newSuperAdmin = new ClinicSuperAdmin
                    {
                        UserId = member.UserId,
                        ClinicId = mainClinicId,
                        PasswordHash = member.PasswordHash,
                        EpochTime = epoch,
                        IsMain = 1
                    };
                    _clinicSuperAdminRepository.Add(newSuperAdmin);
                }

                if (existedMember != null)
                {
                    existedMember.ClinicId = loggedInClinic.Id;
                    existedMember.Role = "Admin";
                    existedMember.DeletedBy = 0;
                    existedMember.PromotedBy = newSuperAdmin.UserId;
                    existedMember.PasswordHash = currentSuperAdmin.PasswordHash;
                    existedMember.EpochTime = epoch;
                    _clinicMemberRepository.Update(existedMember);
                    newClinicMember = existedMember;
                }
                else
                {
                    newClinicMember = new ClinicMember
                    {
                        UserId = currentSuperAdmin.UserId,
                        ClinicId = loggedInClinic.Id,
                        Role = "Admin",
                        PasswordHash = currentSuperAdmin.PasswordHash,
                        CreatedBy = currentSuperAdmin.Id,
                        DeletedBy = 0,
                        PromotedBy = currentSuperAdmin.Id,
                        EpochTime = epoch
                    };
                    _clinicMemberRepository.Add(newClinicMember);
                }

                await _clinicRepository.SaveChangesAsync();
                await transaction.CommitAsync();

                var oldSuperAdminUser = await _userRepository.GetByIdAsync(currentSuperAdmin.UserId);
                var newSuperAdminUser = await _userRepository.GetByIdAsync(member.UserId);

                string newSuperAdminName = $"{newSuperAdminUser?.FirstName} {newSuperAdminUser?.LastName}".Trim();
                string oldSuperAdminName = $"{oldSuperAdminUser?.FirstName} {oldSuperAdminUser?.LastName}".Trim();

                // Generate temporary tokens for both users with current timestamp
                var newSuperAdminEmail = newSuperAdminUser?.Email ?? "";
                var oldSuperAdminEmail = oldSuperAdminUser?.Email ?? "";

                // Generate temporary tokens for both users (replace the previous token generation calls)
                var newSuperAdminTokenData = _jwtTokenService.GenerateTemporaryToken(
                    mainClinicId,
                    newSuperAdminEmail,
                    0,
                    "Super Admin",
                    newSuperAdmin.Id,
                    10 // 10 seconds expiry
                );

                var demotedAdminTokenData = _jwtTokenService.GenerateTemporaryToken(
                    loggedInClinic.Id,
                    oldSuperAdminEmail,
                    0,
                    "Admin",
                    newClinicMember.Id,
                    10 // 10 seconds expiry
                );

                var response = new
                {
                    NewSuperAdminId = newSuperAdmin.Id,
                    OldSuperAdminId = currentSuperAdmin.Id,
                    NewMemberId = newClinicMember.Id,
                    OldMemberId = member.Id,
                    UpdatedDeletedBy = member.DeletedBy,
                    NewSuperAdminUsername = newSuperAdminName,
                    OldSuperAdminUsername = oldSuperAdminName,
                    BranchClinicId = member.ClinicId != mainClinicId ? member.ClinicId : 0,
                    // Add temporary tokens to response
                    NewSuperAdminToken = new
                    {
                        Token = newSuperAdminTokenData.Token,
                        SessionId = newSuperAdminTokenData.SessionId,
                        ExpiresIn = 10, // seconds
                        Message = "Temporary token for immediate access. Please login again for full access."
                    },
                    DemotedAdminToken = new
                    {
                        Token = demotedAdminTokenData.Token,
                        SessionId = demotedAdminTokenData.SessionId,
                        ExpiresIn = 10, // seconds
                        Message = "Temporary token for immediate access. Please login again for full access."
                    },
                    NotificationContext = new
                    {
                        PromotedTo = "Super Admin",
                        NewSuperAdminName = newSuperAdminName,
                        OldSuperAdminName = oldSuperAdminName
                    },
                    NotificationMessage = $"{newSuperAdminName} has been promoted to Super Admin, replacing {oldSuperAdminName}."
                };

                _logger.LogInformation("Promotion successful. New SuperAdmin: {NewSuperAdminId}, Old SuperAdmin: {OldSuperAdminId}", newSuperAdmin.Id, currentSuperAdmin.Id);
                return Ok(ApiResponseFactory.Success(response, $"{member.Role} promoted to Super Admin successfully."));
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
