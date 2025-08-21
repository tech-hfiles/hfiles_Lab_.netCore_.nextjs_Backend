using HFiles_Backend.API.Services;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Labs;
using HFiles_Backend.Domain.Entities.Labs;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace HFiles_Backend.API.Controllers.Labs
{
    [ApiController]
    [Route("api/")]
    public class LabResetPasswordController(AppDbContext context, EmailService emailService, IPasswordHasher<LabSignup> passwordHasher, IPasswordHasher<LabSuperAdmin> passwordHasher1, IPasswordHasher<LabMember> passwordHasher2, ILogger<LabResetPasswordController> logger) : ControllerBase
    {
        private readonly AppDbContext _context = context;
        private readonly EmailService _emailService = emailService;
        private readonly IPasswordHasher<LabSignup> _passwordHasher = passwordHasher;
        private readonly IPasswordHasher<LabSuperAdmin> _passwordHasher1 = passwordHasher1;
        private readonly IPasswordHasher<LabMember> _passwordHasher2 = passwordHasher2;
        private readonly ILogger<LabResetPasswordController> _logger = logger;
        private const int OtpValidityMinutes = 5;





        // Sends Email to Main Lab for password reset
        [HttpPost("labs/password-reset/request")]
        public async Task<IActionResult> RequestPasswordReset([FromBody] PasswordResetRequest dto)
        {
            HttpContext.Items["Log-Category"] = "Authentication";
            _logger.LogInformation("Received password reset request for Email: {Email}", dto.Email);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            try
            {
                var labUser = await _context.LabSignups.FirstOrDefaultAsync(l => l.Email == dto.Email).ConfigureAwait(false);

                if (labUser == null)
                {
                    _logger.LogWarning("No lab user found with Email {Email}", dto.Email);
                    return NotFound(ApiResponseFactory.Fail("No lab user found with this email."));
                }

                if (string.IsNullOrWhiteSpace(labUser.Email))
                {
                    _logger.LogWarning("Lab user Email is missing for {Email}", dto.Email);
                    return StatusCode(500, ApiResponseFactory.Fail("Lab user email is missing."));
                }

                var mainLab = labUser.LabReference == 0
                    ? labUser
                    : await _context.LabSignups.FirstOrDefaultAsync(l => l.Id == labUser.LabReference).ConfigureAwait(false);

                if (mainLab == null || string.IsNullOrWhiteSpace(mainLab.Email))
                {
                    _logger.LogWarning("Main lab Email is missing for reference ID {RefId}", labUser.LabReference);
                    return StatusCode(500, ApiResponseFactory.Fail("Main lab email is missing."));
                }

                var now = DateTime.UtcNow;
                var otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();

                using var transaction = await _context.Database.BeginTransactionAsync().ConfigureAwait(false);

                var otpEntry = new LabOtpEntry
                {
                    Email = dto.Email,
                    OtpCode = otp,
                    CreatedAt = now,
                    ExpiryTime = now.AddMinutes(OtpValidityMinutes)
                };

                _context.LabOtpEntries.Add(otpEntry);
                await _context.SaveChangesAsync().ConfigureAwait(false);

                string recipientEmail = mainLab.Email;
                string resetLink = "https://hfiles.co.in/forgot-password";

                string emailBody = $"""
                <html>
                <body style='font-family:Arial,sans-serif;'>
                    <p>Hello <strong>{labUser.LabName}</strong>,</p>
                    <p>Your OTP for Lab Reset Password is:</p>
                    <h2 style='color: #333;'>{otp}</h2>
                    <p>This OTP is valid for <strong>{OtpValidityMinutes} minutes</strong>.</p>
                    <p>You have requested to reset your password for your lab account. Click the button below to proceed:</p>
                    <p>
                        <a href='{resetLink}' 
                           style='background-color:#0331B5;color:white;padding:10px 20px;text-decoration:none;font-weight:bold;'>
                           Reset Password
                        </a>
                    </p>
                    <p>If you did not request this, please ignore this email.</p>
                    <br />
                    <p>Best regards,<br>The Hfiles Team</p>
                </body>
                </html>
                """;

                await _emailService.SendEmailAsync(recipientEmail, $"Password Reset Request for {labUser.LabName}", emailBody).ConfigureAwait(false);

                await transaction.CommitAsync().ConfigureAwait(false);

                _logger.LogInformation("Password reset OTP sent to {Email}", recipientEmail);

                return Ok(ApiResponseFactory.Success(new
                {
                    RecipientEmail = recipientEmail,
                    labUser.LabName
                }, $"Password reset link sent to {recipientEmail}."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error during password reset for {Email}", dto.Email);
                return StatusCode(500, ApiResponseFactory.Fail($"An error occurred while sending the reset link: {ex.Message}"));
            }
        }






        // Reset Password for Labs
        [HttpPut("labs/password-reset")]
        public async Task<IActionResult> ResetPassword([FromBody] PasswordReset dto, [FromServices] OtpVerificationStore otpStore)
        {
            HttpContext.Items["Log-Category"] = "Authentication";
            _logger.LogInformation("Received password reset request for Email: {Email}", dto.Email);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                                              .Select(e => e.ErrorMessage)
                                              .ToList();

                _logger.LogWarning("Validation failed for password reset: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            if (!otpStore.Consume(dto.Email, "password_reset"))
            {
                _logger.LogWarning("OTP not verified or already used for Email {Email}", dto.Email);
                return Unauthorized(ApiResponseFactory.Fail("OTP not verified or already used. Please verify again."));
            }

            try
            {
                var user = await _context.LabSignups.FirstOrDefaultAsync(l => l.Email == dto.Email).ConfigureAwait(false);

                if (user == null)
                {
                    _logger.LogWarning("No lab user found for Email {Email}", dto.Email);
                    return NotFound(ApiResponseFactory.Fail("No lab user found with this email."));
                }

                if (!string.IsNullOrEmpty(user.PasswordHash))
                {
                    var match = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.NewPassword);
                    if (match == PasswordVerificationResult.Success)
                    {
                        _logger.LogWarning("New password matches existing password for Email {Email}", dto.Email);
                        return BadRequest(ApiResponseFactory.Fail("This password is already in use. Please choose a different one."));
                    }
                }

                var now = DateTime.UtcNow;

                using var transaction = await _context.Database.BeginTransactionAsync().ConfigureAwait(false);

                user.PasswordHash = _passwordHasher.HashPassword(user, dto.NewPassword);
                await _context.SaveChangesAsync().ConfigureAwait(false);

                await transaction.CommitAsync().ConfigureAwait(false);

                _logger.LogInformation("Password reset successful for Email {Email}.", dto.Email);
                return Ok(ApiResponseFactory.Success(message: "Password successfully reset."));

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during password reset for Email {Email}", dto.Email);
                return StatusCode(500, ApiResponseFactory.Fail($"An unexpected error occurred: {ex.Message}"));
            }
        }





        // Sends Email to Lab Users for password reset
        [HttpPost("labs/users/password-reset/request")]
        public async Task<IActionResult> UsersRequestPasswordReset([FromBody] UserPasswordResetRequest dto)
        {
            HttpContext.Items["Log-Category"] = "Authentication";
            _logger.LogInformation("Received password reset request for User Email: {Email}, Lab ID: {LabId}", dto.Email, dto.LabId);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            try
            {
                var userDetails = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == dto.Email && u.IsEmailVerified).ConfigureAwait(false);

                if (userDetails == null || string.IsNullOrWhiteSpace(userDetails.Email))
                {
                    _logger.LogWarning("Password reset failed: No user found or missing email for {Email}", dto.Email);
                    return NotFound(ApiResponseFactory.Fail("No user found with this email or email not verified."));
                }

                int userId = userDetails.Id;
                string? recipientEmail = null;
                string? userRole = null;

                var superAdmin = await _context.LabSuperAdmins
                    .FirstOrDefaultAsync(a => a.UserId == userId && a.IsMain == 1 && a.LabId == dto.LabId).ConfigureAwait(false);

                if (superAdmin != null)
                {
                    recipientEmail = userDetails.Email;
                    userRole = "Super Admin";
                }
                else
                {
                    var labMember = await _context.LabMembers
                        .FirstOrDefaultAsync(m => m.UserId == userId && m.DeletedBy == 0 && m.LabId == dto.LabId).ConfigureAwait(false);

                    if (labMember != null)
                    {
                        recipientEmail = userDetails.Email;
                        userRole = labMember.Role;
                    }
                }

                if (recipientEmail == null)
                {
                    _logger.LogWarning("No matching user found for Lab ID {LabId}", dto.LabId);
                    return NotFound(ApiResponseFactory.Fail("No matching user found for password reset."));
                }

                var labIdToUse = superAdmin?.LabId ?? dto.LabId;
                var lab = await _context.LabSignups.FirstOrDefaultAsync(l => l.Id == labIdToUse).ConfigureAwait(false);

                if (lab == null)
                {
                    _logger.LogWarning("Lab not found for Lab ID {LabId}", labIdToUse);
                    return NotFound(ApiResponseFactory.Fail("Lab not found."));
                }

                var otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
                var now = DateTime.UtcNow;

                using var transaction = await _context.Database.BeginTransactionAsync().ConfigureAwait(false);

                var otpEntry = new LabOtpEntry
                {
                    Email = dto.Email,
                    OtpCode = otp,
                    CreatedAt = now,
                    ExpiryTime = now.AddMinutes(OtpValidityMinutes)
                };

                _context.LabOtpEntries.Add(otpEntry);
                await _context.SaveChangesAsync().ConfigureAwait(false);

                _logger.LogInformation("OTP generated for Email {Email}. OTP: {Otp}", dto.Email, otp);

                string resetLink = "https://hfiles.co.in/forgot-password";
                string emailBody = $"""
                <html>
                <body style='font-family:Arial,sans-serif;'>
                    <p>Hello <strong>{userDetails.FirstName}</strong>,</p>
                    <p>Your OTP for Reset Password is:</p>
                    <h2 style='color: #333;'>{otp}</h2>
                    <p>This OTP is valid for <strong>{OtpValidityMinutes} minutes</strong>.</p>
                    <p>You requested a reset for <strong>{lab.LabName}</strong>. Click below to proceed:</p>
                    <p>
                        <a href='{resetLink}' style='background-color:#0331B5;color:white;padding:10px 20px;text-decoration:none;font-weight:bold;'>
                            Reset Password
                        </a>
                    </p>
                    <p>If you didn't request this, just ignore it.</p>
                    <br />
                    <p>Regards,<br>The Hfiles Team</p>
                </body>
                </html>
                """;

                await _emailService.SendEmailAsync(recipientEmail, $"Password Reset Request for {userDetails.FirstName} {userDetails.LastName}", emailBody).ConfigureAwait(false);
                await transaction.CommitAsync().ConfigureAwait(false);

                _logger.LogInformation("Password reset email sent to {Email}", recipientEmail);

                return Ok(ApiResponseFactory.Success(new
                {
                    RecipientEmail = recipientEmail,
                    UserRole = userRole,
                    lab.LabName
                }, $"Password reset link sent to {recipientEmail} ({userRole})."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during password reset for {Email}", dto.Email);
                return StatusCode(500, ApiResponseFactory.Fail($"An error occurred while sending the reset link: {ex.Message}"));
            }
        }






        // Reset Password for Lab Users
        [HttpPut("labs/users/password-reset")]
        public async Task<IActionResult> UsersResetPassword([FromBody] UserPasswordReset dto, [FromServices] OtpVerificationStore otpStore)
        {
            HttpContext.Items["Log-Category"] = "Authentication";
            _logger.LogInformation("Received password reset request for User Email: {Email}, Lab ID: {LabId}", dto.Email, dto.LabId);

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

            try
            {
                var userDetails = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == dto.Email && u.IsEmailVerified).ConfigureAwait(false);

                if (userDetails == null || string.IsNullOrWhiteSpace(userDetails.Email))
                {
                    _logger.LogWarning("User not found or email is blank for {Email}", dto.Email);
                    return NotFound(ApiResponseFactory.Fail("No user found with this email or email not verified."));
                }

                int userId = userDetails.Id;
                string? userRole = null;
                string? existingHash = null;
                bool passwordChanged = false;

                using var tx = await _context.Database.BeginTransactionAsync().ConfigureAwait(false);

                var superAdmin = await _context.LabSuperAdmins
                    .FirstOrDefaultAsync(sa => sa.UserId == userId && sa.LabId == dto.LabId && sa.IsMain == 1).ConfigureAwait(false);

                if (superAdmin != null)
                {
                    userRole = "Super Admin";
                    existingHash = superAdmin.PasswordHash;

                    if (string.IsNullOrWhiteSpace(existingHash))
                    {
                        _logger.LogWarning("Missing password for Super Admin {Email}", dto.Email);
                        return BadRequest(ApiResponseFactory.Fail("Password not registered by this user."));
                    }

                    if (_passwordHasher1.VerifyHashedPassword(superAdmin, existingHash, dto.NewPassword) == PasswordVerificationResult.Success)
                    {
                        _logger.LogWarning("New password matches old one for Super Admin {Email}", dto.Email);
                        return BadRequest(ApiResponseFactory.Fail("This password is already in use. Please choose a different one."));
                    }

                    superAdmin.PasswordHash = _passwordHasher1.HashPassword(superAdmin, dto.NewPassword);
                    passwordChanged = true;
                }
                else
                {
                    var labMember = await _context.LabMembers
                        .FirstOrDefaultAsync(m => m.UserId == userId && m.LabId == dto.LabId && m.DeletedBy == 0).ConfigureAwait(false);

                    if (labMember != null)
                    {
                        userRole = labMember.Role ?? "Member";
                        existingHash = labMember.PasswordHash;

                        if (string.IsNullOrWhiteSpace(existingHash))
                        {
                            _logger.LogWarning("Missing password for Member {Email}", dto.Email);
                            return BadRequest(ApiResponseFactory.Fail("Password not registered by this user."));
                        }

                        if (_passwordHasher2.VerifyHashedPassword(labMember, existingHash, dto.NewPassword) == PasswordVerificationResult.Success)
                        {
                            _logger.LogWarning("New password matches old one for Member {Email}", dto.Email);
                            return BadRequest(ApiResponseFactory.Fail("This password is already in use. Please choose a different one."));
                        }

                        labMember.PasswordHash = _passwordHasher2.HashPassword(labMember, dto.NewPassword);
                        passwordChanged = true;
                    }
                }

                if (!passwordChanged)
                {
                    _logger.LogWarning("No matching user found for reset: {Email}", dto.Email);
                    return NotFound(ApiResponseFactory.Fail("No matching user found for password reset."));
                }

                await _context.SaveChangesAsync().ConfigureAwait(false);
                await tx.CommitAsync().ConfigureAwait(false);

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
        }






        // Verify OTP for password reset
        [HttpPost("labs/password-reset/verify/otp")]
        public async Task<IActionResult> BranchVerifyOTP([FromBody] OtpLogin dto, [FromServices] OtpVerificationStore otpStore)
        {
            HttpContext.Items["Log-Category"] = "Authentication";
            _logger.LogInformation("Received OTP verification request for Email: {Email}", dto.Email);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed for OTP: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            if (dto.Email == null)
                return BadRequest(ApiResponseFactory.Fail("Email is not provided."));

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

                using var transaction = await _context.Database.BeginTransactionAsync().ConfigureAwait(false);

                var expiredOtps = await _context.LabOtpEntries
                    .Where(x => x.Email == dto.Email && x.ExpiryTime < now)
                    .ToListAsync()
                    .ConfigureAwait(false);

                _context.LabOtpEntries.RemoveRange(expiredOtps);
                _context.LabOtpEntries.Remove(otpEntry);

                await _context.SaveChangesAsync().ConfigureAwait(false);
                await transaction.CommitAsync().ConfigureAwait(false);

                otpStore.StoreVerifiedOtp(dto.Email, "password_reset");

                _logger.LogInformation("OTP successfully verified for Email {Email}", dto.Email);
                return Ok(ApiResponseFactory.Success("OTP successfully verified."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OTP verification error for Email {Email}", dto.Email);
                return StatusCode(500, ApiResponseFactory.Fail($"An unexpected error occurred: {ex.Message}"));
            }
        }
    }
}
