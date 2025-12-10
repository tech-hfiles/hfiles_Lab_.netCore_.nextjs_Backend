using HFiles_Backend.API.Interfaces;
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
         IUserRepository userRepository,
          ITokenBlacklistService tokenBlacklistService
        ) : ControllerBase
    {
        private readonly ILogger<ClinicAdminController> _logger = logger;
        private readonly IClinicRepository _clinicRepository = clinicRepository;
        private readonly IClinicSuperAdminRepository _clinicSuperAdminRepository = clinicSuperAdminRepository;
        private readonly JwtTokenService _jwtTokenService = jwtTokenService;
        private readonly IPasswordHasher<ClinicSuperAdmin> _passwordHasher = passwordHasher;
        private readonly IClinicMemberRepository _clinicMemberRepository = clinicMemberRepository;
        private readonly IUserRepository _userRepository = userRepository;
        private readonly ITokenBlacklistService _tokenBlacklistService = tokenBlacklistService;





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

                    // Clear blacklisted tokens for this user and clinic
                    await _tokenBlacklistService.RemoveUserBlacklistAsync(dto.UserId);

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

                    // Clear blacklisted tokens for this user and clinic
                    await _tokenBlacklistService.RemoveUserBlacklistAsync(dto.UserId);

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
            var transaction = await _clinicRepository.BeginTransactionAsync();
            try
            {
                var clinicEntry = await _clinicRepository.GetByIdAsync(clinicId);
                if (clinicEntry == null)
                {
                    _logger.LogWarning("Clinic with ID {ClinicId} not found.", clinicId);
                    return NotFound(ApiResponseFactory.Fail($"Clinic with ID {clinicId} not found."));
                }
                int mainClinicId = clinicEntry.ClinicReference == 0 ? clinicId : clinicEntry.ClinicReference;
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
                    UserCounts = memberDtos.Count + 1,
                    SuperAdmin = superAdminDto,
                    Members = memberDtos
                };
                _logger.LogInformation("Successfully fetched users for Clinic ID {ClinicId}.", clinicId);
                return Ok(ApiResponseFactory.Success(response, "Users fetched successfully."));
            }
            finally
            {
                if (transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.RollbackAsync();
                }
            }
        }





        // Promote admin to super admin with forced logout
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
            var currentSessionId = User.FindFirst("SessionId")?.Value;

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

                // BLACKLIST ALL EXISTING TOKENS FOR BOTH USERS
                await _tokenBlacklistService.BlacklistAllUserTokensAsync(currentSuperAdmin.UserId, clinicId, "super_admin_promotion");
                await _tokenBlacklistService.BlacklistAllUserTokensAsync(member.UserId, clinicId, "promoted_to_super_admin");

                await transaction.CommitAsync();

                var oldSuperAdminUser = await _userRepository.GetByIdAsync(currentSuperAdmin.UserId);
                var newSuperAdminUser = await _userRepository.GetByIdAsync(member.UserId);

                string newSuperAdminName = $"{newSuperAdminUser?.FirstName} {newSuperAdminUser?.LastName}".Trim();
                string oldSuperAdminName = $"{oldSuperAdminUser?.FirstName} {oldSuperAdminUser?.LastName}".Trim();

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
                    // Force logout flags for frontend
                    ForceLogout = new
                    {
                        CurrentSuperAdmin = new
                        {
                            UserId = currentSuperAdmin.UserId,
                            Username = oldSuperAdminName,
                            Reason = "demoted_from_super_admin",
                            Message = "You have been demoted from Super Admin. Please login again with your new Admin credentials."
                        },
                        PromotedUser = new
                        {
                            UserId = member.UserId,
                            Username = newSuperAdminName,
                            Reason = "promoted_to_super_admin",
                            Message = "Congratulations! You have been promoted to Super Admin. Please login again with your new credentials."
                        }
                    },
                    NotificationContext = new
                    {
                        PromotedTo = "Super Admin",
                        NewSuperAdminName = newSuperAdminName,
                        OldSuperAdminName = oldSuperAdminName
                    },
                    NotificationMessage = $"{newSuperAdminName} has been promoted to Super Admin, replacing {oldSuperAdminName}. Both users must login again."
                };

                _logger.LogInformation("Promotion successful. New SuperAdmin: {NewSuperAdminId}, Old SuperAdmin: {OldSuperAdminId}. Tokens blacklisted for both users.", newSuperAdmin.Id, currentSuperAdmin.Id);
                return Ok(ApiResponseFactory.Success(response, $"{member.Role} promoted to Super Admin successfully. Both users must login again."));
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
