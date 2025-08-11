using HFiles_Backend.API.Services;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Clinics.SuperAdmin;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;

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
          IClinicMemberRepository clinicMemberRepository
        ) : ControllerBase
    {
        private readonly ILogger<ClinicAdminController> _logger = logger;
        private readonly IClinicRepository _clinicRepository = clinicRepository;
        private readonly IClinicSuperAdminRepository _clinicSuperAdminRepository = clinicSuperAdminRepository;
        private readonly JwtTokenService _jwtTokenService = jwtTokenService;
        private readonly IPasswordHasher<ClinicSuperAdmin> _passwordHasher = passwordHasher;
        private readonly IClinicMemberRepository _clinicMemberRepository = clinicMemberRepository;





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

                var tokenData = _jwtTokenService.GenerateToken(dto.UserId, dto.Email, newAdmin.Id, dto.Role);

                _logger.LogInformation("Clinic Super Admin created. Token issued. Session ID: {SessionId}", tokenData.SessionId);

                var responseData = new
                {
                    username = $"{user.FirstName} {user.LastName}",
                    token = tokenData.Token,
                    sessionId = tokenData.SessionId
                };

                return Ok(ApiResponseFactory.Success(responseData, "Clinic Super Admin created successfully."));
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

                    var (Token, SessionId) = _jwtTokenService.GenerateToken(dto.UserId, dto.Email, admin.Id, dto.Role);
                    _logger.LogInformation("Super Admin login success: {Username} | Session ID: {SessionId}", username, SessionId);

                    response = new { Username = username, Token, SessionId };
                }
                //else if (dto.Role == "Admin" || dto.Role == "Member")
                //{
                //    var member = await _clinicMemberRepository.GetMemberAsync(userDetails.Id, dto.UserId, dto.Role);
                //    if (member == null)
                //    {
                //        _logger.LogWarning("Login failed: {Role} not found for HFID {HFID}", dto.Role, dto.HFID);
                //        return Unauthorized(ApiResponseFactory.Fail($"{dto.Role} not found. Please register first."));
                //    }

                //    if (string.IsNullOrWhiteSpace(member.PasswordHash))
                //    {
                //        _logger.LogWarning("Login failed: Password not set for {Role} {Username}", dto.Role, username);
                //        return Unauthorized(ApiResponseFactory.Fail($"Password is not set for {dto.Role}: {username}"));
                //    }

                //    if (_passwordHasher.VerifyHashedPassword(null!, member.PasswordHash, dto.Password) != PasswordVerificationResult.Success)
                //    {
                //        _logger.LogWarning("Login failed: Invalid password for {Role} {Username}", dto.Role, username);
                //        return Unauthorized(ApiResponseFactory.Fail("Invalid password."));
                //    }

                //    var tokenData = _jwtTokenService.GenerateToken(dto.UserId, dto.Email, member.Id, dto.Role);
                //    _logger.LogInformation("{Role} login success: {Username} | Session ID: {SessionId}", dto.Role, username, tokenData.SessionId);

                //    response = new { Username = username, tokenData.Token, tokenData.SessionId };
                //}
                else
                {
                    _logger.LogWarning("Login failed: Invalid role specified {Role}", dto.Role);
                    return BadRequest(ApiResponseFactory.Fail("Invalid role specified."));
                }

                await transaction.CommitAsync();
                return Ok(ApiResponseFactory.Success(response, $"{dto.Role} successfully logged in."));
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
