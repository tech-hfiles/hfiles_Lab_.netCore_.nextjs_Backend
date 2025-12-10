using HFiles_Backend.API.Interfaces;
using HFiles_Backend.API.Services;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Clinics.ResetPassword;
using HFiles_Backend.Application.DTOs.Labs;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;
using System.Security.Cryptography;

namespace HFiles_Backend.API.Controllers.Clinics
{
    [Route("api/")]
    [ApiController]
    public class ClinicResetPasswordController(
        EmailService emailService,
        IPasswordHasher<ClinicSignup> passwordHasher,
        IPasswordHasher<ClinicSuperAdmin> passwordHasher1,
        IPasswordHasher<ClinicMember> passwordHasher2,
        ILogger<ClinicResetPasswordController> logger,
        IClinicRepository clinicRepository,
        IClinicSuperAdminRepository clinicSuperAdminRepository,
        IClinicMemberRepository clinicMemberRepository,
        IUserRepository userRepository,
        IEmailTemplateService emailTemplateService
    ) : ControllerBase
    {
        private readonly EmailService _emailService = emailService;
        private readonly IPasswordHasher<ClinicSignup> _passwordHasher = passwordHasher;
        private readonly IPasswordHasher<ClinicSuperAdmin> _passwordHasher1 = passwordHasher1;
        private readonly IPasswordHasher<ClinicMember> _passwordHasher2 = passwordHasher2;
        private readonly ILogger<ClinicResetPasswordController> _logger = logger;
        private readonly IClinicRepository _clinicRepository = clinicRepository;
        private readonly IClinicSuperAdminRepository _clinicSuperAdminRepository = clinicSuperAdminRepository;
        private readonly IClinicMemberRepository _clinicMemberRepository = clinicMemberRepository;
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IEmailTemplateService _emailTemplateService = emailTemplateService;
        private const int OtpValidityMinutes = 5;





        // Verify OTP for reset Password for Clinic and Clinic Users
        [HttpPost("clinics/password-reset/verify/otp")]
        public async Task<IActionResult> VerifyClinicOtp(
        [FromBody] OtpLogin dto,
        [FromServices] OtpVerificationStore otpStore)
        {
            HttpContext.Items["Log-Category"] = "Authentication";
            _logger.LogInformation("Received OTP verification request for Clinic Email: {Email}", dto.Email);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed for OTP: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            if (dto.Email == null)
                return BadRequest(ApiResponseFactory.Fail("Email is not provided."));

            var now = DateTime.UtcNow;
            var transaction = await _clinicRepository.BeginTransactionAsync();
            var committed = false;

            try
            {
                var otpEntry = await _clinicRepository.GetLatestOtpEntryAsync(dto.Email);
                if (otpEntry == null)
                {
                    _logger.LogWarning("No OTP found for Email {Email}", dto.Email);
                    return BadRequest(ApiResponseFactory.Fail("OTP expired or not found."));
                }

                if (otpEntry.ExpiryTime < now)
                {
                    _logger.LogWarning("OTP expired for Email {Email}", dto.Email);
                    return BadRequest(ApiResponseFactory.Fail("OTP expired."));
                }

                if (otpEntry.OtpCode != dto.Otp)
                {
                    _logger.LogWarning("Invalid OTP submitted for Email {Email}", dto.Email);
                    return BadRequest(ApiResponseFactory.Fail("Invalid OTP."));
                }

                var expiredOtps = await _clinicRepository.GetExpiredOtpsAsync(dto.Email, now);
                _clinicRepository.RemoveOtpEntries(expiredOtps);
                _clinicRepository.RemoveOtpEntry(otpEntry);

                await _clinicRepository.SaveChangesAsync();
                await transaction.CommitAsync();
                committed = true;

                otpStore.StoreVerifiedOtp(dto.Email, "password_reset");

                _logger.LogInformation("OTP successfully verified for Email {Email}", dto.Email);
                return Ok(ApiResponseFactory.Success("OTP successfully verified."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OTP verification error for Email {Email}", dto.Email);
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





        // Clinic Reset Password

        [HttpPut("clinics/password-reset")]
        public async Task<IActionResult> ResetClinicPassword(
        [FromBody] PasswordReset dto,
        [FromServices] OtpVerificationStore otpStore)
        {
            HttpContext.Items["Log-Category"] = "Authentication";
            _logger.LogInformation("Received password reset request for Clinic Email: {Email}", dto.Email);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed for password reset: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            if (!otpStore.Consume(dto.Email, "password_reset"))
            {
                _logger.LogWarning("OTP not verified or already used for Email {Email}", dto.Email);
                return Unauthorized(ApiResponseFactory.Fail("OTP not verified or already used. Please verify again."));
            }

            var transaction = await _clinicRepository.BeginTransactionAsync();
            var committed = false;

            try
            {
                var clinicUser = await _clinicRepository.GetClinicByEmailAsync(dto.Email);
                if (clinicUser == null)
                {
                    _logger.LogWarning("No clinic user found for Email {Email}", dto.Email);
                    return NotFound(ApiResponseFactory.Fail("No clinic user found with this email."));
                }

                if (!string.IsNullOrEmpty(clinicUser.PasswordHash))
                {
                    var match = _passwordHasher.VerifyHashedPassword(clinicUser, clinicUser.PasswordHash, dto.NewPassword);
                    if (match == PasswordVerificationResult.Success)
                    {
                        _logger.LogWarning("New password matches existing password for Email {Email}", dto.Email);
                        return BadRequest(ApiResponseFactory.Fail("This password is already in use. Please choose a different one."));
                    }
                }

                clinicUser.PasswordHash = _passwordHasher.HashPassword(clinicUser, dto.NewPassword);
                _clinicRepository.UpdateClinic(clinicUser);
                await _clinicRepository.SaveChangesAsync();
                await transaction.CommitAsync();
                committed = true;

                _logger.LogInformation("Password reset successful for Clinic Email: {Email}", dto.Email);
                return Ok(ApiResponseFactory.Success("Password successfully reset."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during clinic password reset for Email {Email}", dto.Email);
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





        // Clinic User Reset Password
        [HttpPut("clinics/users/password-reset")]
        public async Task<IActionResult> UsersResetClinicPassword(
        [FromBody] ClinicUserPasswordReset dto,
        [FromServices] OtpVerificationStore otpStore)
        {
            HttpContext.Items["Log-Category"] = "Authentication";
            _logger.LogInformation("Received password reset request for Clinic User Email: {Email}, Clinic ID: {ClinicId}", dto.Email, dto.ClinicId);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            if (!otpStore.Consume(dto.Email, "password_reset"))
            {
                _logger.LogWarning("OTP not verified or already used for Email {Email}", dto.Email);
                return Unauthorized(ApiResponseFactory.Fail("OTP not verified or already used. Please verify again."));
            }

            var transaction = await _clinicRepository.BeginTransactionAsync();
            var committed = false;

            try
            {
                var user = await _clinicRepository.GetVerifiedUserByEmailAsync(dto.Email);
                if (user == null)
                {
                    _logger.LogWarning("User not found or email not verified for {Email}", dto.Email);
                    return NotFound(ApiResponseFactory.Fail("No user found with this email or email not verified."));
                }

                int userId = user.Id;
                string? userRole = null;
                bool passwordChanged = false;

                var superAdmin = await _clinicRepository.GetSuperAdminAsync(userId, dto.ClinicId);
                if (superAdmin != null)
                {
                    userRole = "Super Admin";

                    if (string.IsNullOrWhiteSpace(superAdmin.PasswordHash))
                    {
                        _logger.LogWarning("Missing password for Super Admin {Email}", dto.Email);
                        return BadRequest(ApiResponseFactory.Fail("Password not registered by this user."));
                    }

                    if (_passwordHasher1.VerifyHashedPassword(superAdmin, superAdmin.PasswordHash, dto.NewPassword) == PasswordVerificationResult.Success)
                    {
                        _logger.LogWarning("New password matches old one for Super Admin {Email}", dto.Email);
                        return BadRequest(ApiResponseFactory.Fail("This password is already in use. Please choose a different one."));
                    }

                    superAdmin.PasswordHash = _passwordHasher1.HashPassword(superAdmin, dto.NewPassword);
                    _clinicRepository.UpdateSuperAdmin(superAdmin);
                    passwordChanged = true;
                }
                else
                {
                    var member = await _clinicRepository.GetClinicMemberAsync(userId, dto.ClinicId);
                    if (member != null)
                    {
                        userRole = member.Role ?? "Member";

                        if (string.IsNullOrWhiteSpace(member.PasswordHash))
                        {
                            _logger.LogWarning("Missing password for Member {Email}", dto.Email);
                            return BadRequest(ApiResponseFactory.Fail("Password not registered by this user."));
                        }

                        if (_passwordHasher2.VerifyHashedPassword(member, member.PasswordHash, dto.NewPassword) == PasswordVerificationResult.Success)
                        {
                            _logger.LogWarning("New password matches old one for Member {Email}", dto.Email);
                            return BadRequest(ApiResponseFactory.Fail("This password is already in use. Please choose a different one."));
                        }

                        member.PasswordHash = _passwordHasher2.HashPassword(member, dto.NewPassword);
                        _clinicRepository.UpdateClinicMember(member);
                        passwordChanged = true;
                    }
                }

                if (!passwordChanged)
                {
                    _logger.LogWarning("No matching user found for reset: {Email}", dto.Email);
                    return NotFound(ApiResponseFactory.Fail("No matching user found for password reset."));
                }

                await _clinicRepository.SaveChangesAsync();
                await transaction.CommitAsync();
                committed = true;

                _logger.LogInformation("Password reset successful for {Email}, Role {Role}", dto.Email, userRole);
                return Ok(ApiResponseFactory.Success(new
                {
                    dto.Email,
                    UserRole = userRole
                }, "Password successfully updated."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during password reset for {Email}", dto.Email);
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
    }
}
