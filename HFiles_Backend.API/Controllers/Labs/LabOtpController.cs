using System.Security.Cryptography;
using HFiles_Backend.API.Services;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Labs;
using HFiles_Backend.Domain.Entities.Labs;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace HFiles_Backend.API.Controllers.Labs
{
    [ApiController]
    [Route("api/")]
    public class LabOtpController(AppDbContext context, EmailService emailService, IWhatsappService whatsappService, ILogger<LabOtpController> logger) : ControllerBase
    {
        private readonly AppDbContext _context = context;
        private readonly EmailService _emailService = emailService;
        private readonly IWhatsappService _whatsappService = whatsappService;
        private readonly ILogger<LabOtpController> _logger = logger;





        // Generates OTP for Signup
        [HttpPost("labs/signup/otp")]
        public async Task<IActionResult> GenerateOtp([FromBody] OtpRequest dto)
        {
            HttpContext.Items["Log-Category"] = "Authentication";

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                                              .Select(e => e.ErrorMessage)
                                              .ToList();
                _logger.LogWarning("Validation failed for OTP request: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            _logger.LogInformation("OTP requested for Email: {Email}, Phone: {Phone}", dto.Email, dto.PhoneNumber);

            try
            {
                var otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
                var now = DateTime.UtcNow;

                using var transaction = await _context.Database.BeginTransactionAsync();

                var otpEntry = new LabOtpEntry
                {
                    Email = dto.Email,
                    OtpCode = otp,
                    CreatedAt = now,
                    ExpiryTime = now.AddMinutes(5)
                };

                _context.LabOtpEntries.Add(otpEntry);
                await _context.SaveChangesAsync();

                _logger.LogInformation("OTP {Otp} generated for Email {Email} at {Time}", otp, dto.Email, now);

                var subject = "Complete Your Hfiles Lab Registration";
                var body = $"""
                <p>Hello <strong>{dto.LabName}</strong>,</p>
                <p>Welcome to Hfiles!</p>
                <p>Your One-Time Password (OTP) is:</p>
                <h2>{otp}</h2>
                <p>This OTP expires in 5 minutes.</p>
                <p>Need help? <a href='mailto:contact@hfiles.in'>contact@hfiles.in</a></p>
                <p>– The Hfiles Team</p>
                """;

                await _emailService.SendEmailAsync(dto.Email, subject, body).ConfigureAwait(false);
                await _whatsappService.SendOtpAsync(otp, dto.PhoneNumber).ConfigureAwait(false);

                await transaction.CommitAsync();

                _logger.LogInformation("OTP sent to {Email} and {Phone}", dto.Email, dto.PhoneNumber);

                return Ok(ApiResponseFactory.Success(new
                {
                    dto.Email,
                    dto.PhoneNumber,
                    ExpiresInMinutes = 5
                }, "OTP has been sent to your email and phone number."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process OTP for Email {Email}", dto.Email);
                return StatusCode(500, ApiResponseFactory.Fail($"OTP generated but failed to send: {ex.Message}"));
            }
        }

    }
}
