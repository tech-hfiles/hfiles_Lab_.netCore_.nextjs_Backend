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
          IPasswordHasher<ClinicSuperAdmin> passwordHasher
        ) : ControllerBase
    {
        private readonly ILogger<ClinicAdminController> _logger = logger;
        private readonly IClinicRepository _clinicRepository = clinicRepository;
        private readonly IClinicSuperAdminRepository _clinicSuperAdminRepository = clinicSuperAdminRepository;
        private readonly JwtTokenService _jwtTokenService = jwtTokenService;
        private readonly IPasswordHasher<ClinicSuperAdmin> _passwordHasher = passwordHasher;





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
    }
}
