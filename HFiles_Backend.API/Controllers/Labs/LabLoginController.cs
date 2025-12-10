using System.Security.Cryptography;
using HFiles_Backend.API.Services;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Labs;
using HFiles_Backend.Domain.Entities.Labs;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HFiles_Backend.API.Controllers.Labs
{
    [ApiController]
    [Route("api/")]
    public class LabLoginController(
        AppDbContext context,
        EmailService emailService,
        IPasswordHasher<LabSignup> passwordHasher,
        ILogger<LabLoginController> logger,
        IWhatsappService whatsappService) : ControllerBase

    {
        private readonly AppDbContext _context = context;
        private readonly EmailService _emailService = emailService;
        private readonly IWhatsappService _whatsappService = whatsappService;
        private readonly IPasswordHasher<LabSignup> _passwordHasher = passwordHasher;
        private readonly ILogger<LabLoginController> _logger = logger;
        private const int OtpValidityMinutes = 5;





        // Sends OTP
        [HttpPost("labs/otp")]
        public async Task<IActionResult> SendOtp([FromBody] LoginOtpRequest dto)
        {
            HttpContext.Items["Log-Category"] = "Authentication";
            _logger.LogInformation("Received OTP request for Email: {Email}, Phone: {PhoneNumber}", dto.Email, dto.PhoneNumber);

            if (string.IsNullOrWhiteSpace(dto.Email) == string.IsNullOrWhiteSpace(dto.PhoneNumber))
            {
                _logger.LogWarning("Invalid request: Either Email or PhoneNumber must be provided.");
                return BadRequest(ApiResponseFactory.Fail("Provide either Email or PhoneNumber, not both."));
            }

            try
            {
                var otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
                var now = DateTime.UtcNow;
                var expiryTime = now.AddMinutes(OtpValidityMinutes);

                using var transaction = await _context.Database.BeginTransactionAsync().ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(dto.Email))
                {
                    var user = await _context.LabSignups
                        .FirstOrDefaultAsync(u => u.Email == dto.Email)
                        .ConfigureAwait(false);

                    if (user == null)
                    {
                        _logger.LogWarning("OTP generation failed: Email {Email} not registered.", dto.Email);
                        return BadRequest(ApiResponseFactory.Fail("Email not registered."));
                    }

                    var otpEntry = new LabOtpEntry
                    {
                        Email = dto.Email,
                        OtpCode = otp,
                        CreatedAt = now,
                        ExpiryTime = expiryTime
                    };

                    _context.LabOtpEntries.Add(otpEntry);
                    await _context.SaveChangesAsync().ConfigureAwait(false);

                    var body = $"""
                    <html>
                    <body style='font-family: Arial, sans-serif; line-height: 1.6;'>
                        <p>Hello,</p>
                        <p>Your OTP for <strong>Hfiles</strong> login is:</p>
                        <h2 style='color: #333;'>{otp}</h2>
                        <p>This OTP is valid for <strong>{OtpValidityMinutes} minutes</strong>.</p>
                        <p>If you didn’t request this, you can ignore this email.</p>
                        <br/>
                        <p>Best regards,<br/>The Hfiles Team</p>
                    </body>
                    </html>
                    """;

                    await _emailService.SendEmailAsync(dto.Email, "Your Hfiles Login OTP", body).ConfigureAwait(false);
                    await transaction.CommitAsync().ConfigureAwait(false);

                    _logger.LogInformation("OTP sent successfully to Email {Email}.", dto.Email);
                    return Ok(ApiResponseFactory.Success("OTP sent successfully to Email."));
                }
                else
                {
                    var user = await _context.LabSignups
                        .FirstOrDefaultAsync(u => u.PhoneNumber == dto.PhoneNumber)
                        .ConfigureAwait(false);

                    if (user == null)
                    {
                        _logger.LogWarning("OTP generation failed: Phone {PhoneNumber} not registered.", dto.PhoneNumber);
                        return BadRequest(ApiResponseFactory.Fail("Phone number not registered."));
                    }

                    var otpEntry = new LabOtpEntry
                    {
                        Email = dto.PhoneNumber,
                        OtpCode = otp,
                        CreatedAt = now,
                        ExpiryTime = expiryTime
                    };

                    _context.LabOtpEntries.Add(otpEntry);
                    await _context.SaveChangesAsync().ConfigureAwait(false);

                    try
                    {
                        await _whatsappService.SendOtpAsync(otp, dto.PhoneNumber!).ConfigureAwait(false);
                        await transaction.CommitAsync().ConfigureAwait(false);

                        _logger.LogInformation("OTP sent successfully to Phone {PhoneNumber}.", dto.PhoneNumber);
                        return Ok(ApiResponseFactory.Success("OTP sent successfully to Phone."));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send OTP via WhatsApp for Phone: {PhoneNumber}", dto.PhoneNumber);
                        return StatusCode(500, ApiResponseFactory.Fail("OTP generated but failed to send notification."));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OTP generation failed due to an unexpected error.");
                return StatusCode(500, ApiResponseFactory.Fail($"Unexpected error: {ex.Message}"));
            }
        }





        // Login via Email/Phone + OTP
        [HttpPost("labs/login/otp")]
        public async Task<IActionResult> LoginViaOtp([FromBody] OtpLogin dto)
        {
            HttpContext.Items["Log-Category"] = "Authentication";

            _logger.LogInformation("Received OTP login request for Email: {Email}, Phone: {PhoneNumber}", dto.Email, dto.PhoneNumber);

            if (string.IsNullOrWhiteSpace(dto.Email) == string.IsNullOrWhiteSpace(dto.PhoneNumber))
            {
                _logger.LogWarning("Invalid OTP login request — both or neither identifier provided.");
                return BadRequest(ApiResponseFactory.Fail("Provide either Email or PhoneNumber, not both."));
            }

            try
            {
                var now = DateTime.UtcNow;
                object? responseData = null;
                LabOtpEntry? otpEntry = null;

                using var transaction = await _context.Database.BeginTransactionAsync().ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(dto.Email))
                {
                    var user = await _context.LabSignups
                        .FirstOrDefaultAsync(u => u.Email == dto.Email)
                        .ConfigureAwait(false);

                    if (user == null)
                    {
                        _logger.LogWarning("OTP login failed: Email {Email} not registered.", dto.Email);
                        return BadRequest(ApiResponseFactory.Fail("Email not registered."));
                    }

                    otpEntry = await _context.LabOtpEntries
                        .Where(o => o.Email == dto.Email)
                        .OrderByDescending(o => o.CreatedAt)
                        .FirstOrDefaultAsync()
                        .ConfigureAwait(false);

                    responseData = new
                    {
                        UserId = user.Id,
                        user.Email,
                        user.IsSuperAdmin
                    };
                }
                else if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
                {
                    var user = await _context.LabSignups
                        .FirstOrDefaultAsync(u => u.PhoneNumber == dto.PhoneNumber)
                        .ConfigureAwait(false);

                    if (user == null)
                    {
                        _logger.LogWarning("OTP login failed: Phone {PhoneNumber} not registered.", dto.PhoneNumber);
                        return BadRequest(ApiResponseFactory.Fail("Phone number not registered."));
                    }

                    otpEntry = await _context.LabOtpEntries
                        .Where(o => o.Email == dto.PhoneNumber)
                        .OrderByDescending(o => o.CreatedAt)
                        .FirstOrDefaultAsync()
                        .ConfigureAwait(false);

                    responseData = new
                    {
                        UserId = user.Id,
                        user.PhoneNumber,
                        user.Email,
                        user.IsSuperAdmin
                    };
                }

                if (otpEntry == null)
                {
                    _logger.LogWarning("OTP login failed: No OTP found.");
                    return BadRequest(ApiResponseFactory.Fail("OTP expired or not found."));
                }

                if (otpEntry.ExpiryTime < now)
                {
                    _logger.LogWarning("OTP login failed: OTP expired.");
                    return BadRequest(ApiResponseFactory.Fail("OTP expired."));
                }

                if (otpEntry.OtpCode != dto.Otp)
                {
                    _logger.LogWarning("OTP login failed: Invalid OTP submitted.");
                    return BadRequest(ApiResponseFactory.Fail("Invalid OTP."));
                }

                var expiredOtps = await _context.LabOtpEntries
                    .Where(x =>
                        (x.Email == dto.Email || x.Email == dto.PhoneNumber) &&
                        x.ExpiryTime < now)
                    .ToListAsync()
                    .ConfigureAwait(false);

                _context.LabOtpEntries.RemoveRange(expiredOtps);
                _context.LabOtpEntries.Remove(otpEntry);

                await _context.SaveChangesAsync().ConfigureAwait(false);
                await transaction.CommitAsync().ConfigureAwait(false);

                _logger.LogInformation("OTP login successful for identifier {Identifier}.", dto.Email ?? dto.PhoneNumber);
                return Ok(ApiResponseFactory.Success(responseData, "Lab login successful, proceed to LabAdmin login."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OTP login failed due to unexpected error for Email {Email}", dto.Email);
                return StatusCode(500, ApiResponseFactory.Fail($"An unexpected error occurred: {ex.Message}"));
            }
        }





        // Login via Email + Password
        [HttpPost("labs/login/password")]
        public async Task<IActionResult> LoginViaPassword([FromBody] PasswordLogin dto)
        {
            HttpContext.Items["Log-Category"] = "Authentication";

            _logger.LogInformation("Received password login request for Email: {Email}", dto.Email);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                                              .Select(e => e.ErrorMessage)
                                              .ToList();
                _logger.LogWarning("Validation failed: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync().ConfigureAwait(false);

                var user = await _context.LabSignups
                    .FirstOrDefaultAsync(u => u.Email == dto.Email)
                    .ConfigureAwait(false);

                if (user == null)
                {
                    _logger.LogWarning("Login failed: Email {Email} not registered.", dto.Email);
                    return BadRequest(ApiResponseFactory.Fail("Email not registered."));
                }

                if (string.IsNullOrWhiteSpace(user.PasswordHash))
                {
                    _logger.LogWarning("Login failed: No password set for Email {Email}.", dto.Email);
                    return BadRequest(ApiResponseFactory.Fail("Password is not set for this account."));
                }

                var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
                if (result == PasswordVerificationResult.Failed)
                {
                    _logger.LogWarning("Login failed: Incorrect password for Email {Email}.", dto.Email);
                    return BadRequest(ApiResponseFactory.Fail("Incorrect password."));
                }

                var responseData = new
                {
                    UserId = user.Id,
                    user.Email,
                    user.IsSuperAdmin
                };

                await transaction.CommitAsync().ConfigureAwait(false);

                _logger.LogInformation("Password login successful for Email {Email}.", dto.Email);
                return Ok(ApiResponseFactory.Success(responseData, "Lab login successful, proceed to LabAdmin login."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Password login failed due to unexpected error for Email {Email}", dto.Email);
                return StatusCode(500, ApiResponseFactory.Fail($"An unexpected error occurred: {ex.Message}"));
            }
        }
    }
}