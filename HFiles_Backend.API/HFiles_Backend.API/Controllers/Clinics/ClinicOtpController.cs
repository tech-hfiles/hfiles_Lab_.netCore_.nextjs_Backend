using HFiles_Backend.API.Interfaces;
using HFiles_Backend.API.Services;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Clinics.Signup;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;
using System.Security.Cryptography;
using HFiles_Backend.Application.DTOs.Clinics.Login;

namespace HFiles_Backend.API.Controllers.Clinics
{
    [Route("api/")]
    [ApiController]
    public class ClinicOtpController(
         ILogger<ClinicOtpController> logger,
        IClinicRepository clinicRepository,
        EmailService emailService,
        IWhatsappService whatsappService,
        IEmailTemplateService emailTemplateService
        ) : ControllerBase
    {
        private readonly ILogger<ClinicOtpController> _logger = logger;
        private readonly IClinicRepository _clinicRepository = clinicRepository;
        private readonly EmailService _emailService = emailService;
        private readonly IWhatsappService _whatsappService = whatsappService;
        private readonly IEmailTemplateService _emailTemplateService = emailTemplateService;
        private const int OtpValidityMinutes = 5;




        // Signup OTP
        [HttpPost("clinics/signup/otp")]
        public async Task<IActionResult> GenerateClinicOtp([FromBody] ClinicSignupOtpRequest dto)
        {
            HttpContext.Items["Log-Category"] = "Clinic Authentication";

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                                              .Select(e => e.ErrorMessage)
                                              .ToList();
                _logger.LogWarning("Validation failed for Clinic OTP request: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            _logger.LogInformation("Clinic OTP requested for Email: {Email}, Phone: {Phone}", dto.Email, dto.PhoneNumber);

            var otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            var now = DateTime.UtcNow;

            var transaction = await _clinicRepository.BeginTransactionAsync();

            try
            {
                var otpEntry = new ClinicOtpEntry
                {
                    Email = dto.Email,
                    OtpCode = otp,
                    CreatedAt = now,
                    ExpiryTime = now.AddMinutes(5)
                };

                await _clinicRepository.AddOtpAsync(otpEntry);
                await _clinicRepository.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Clinic OTP {Otp} generated for Email {Email} at {Time}", otp, dto.Email, now);

                var subject = "Complete Your Hfiles Clinic Registration";
                var body = _emailTemplateService.GenerateClinicOtpTemplate(dto.ClinicName, otp);

                await _emailService.SendEmailAsync(dto.Email, subject, body).ConfigureAwait(false);
                await _whatsappService.SendOtpAsync(otp, dto.PhoneNumber).ConfigureAwait(false);

                _logger.LogInformation("Clinic OTP sent to {Email} and {Phone}", dto.Email, dto.PhoneNumber);

                return Ok(ApiResponseFactory.Success(new
                {
                    dto.Email,
                    dto.PhoneNumber,
                    ExpiresInMinutes = 5
                }, "OTP has been sent to your email and phone number."));
            }
            finally
            {
                if (transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.RollbackAsync();
                }
            }
        }





        // Login OTP
        [HttpPost("clinics/login/otp")]
        public async Task<IActionResult> SendOtp([FromBody] ClinicLoginOtpRequest dto)
        {
            HttpContext.Items["Log-Category"] = "Clinic Authentication";
            _logger.LogInformation("Received OTP request for Email: {Email}, Phone: {PhoneNumber}", dto.Email, dto.PhoneNumber);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Model validation failed. Errors: {Errors}", string.Join(", ", errors));
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            var otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            var now = DateTime.UtcNow;
            var expiryTime = now.AddMinutes(OtpValidityMinutes);

            var transaction = await _clinicRepository.BeginTransactionAsync();

            try
            {
                if (!string.IsNullOrWhiteSpace(dto.Email))
                {
                    var user = await _clinicRepository.GetByEmailAsync(dto.Email);
                    if (user == null)
                    {
                        _logger.LogWarning("OTP generation failed: Email {Email} not registered.", dto.Email);
                        return BadRequest(ApiResponseFactory.Fail("Email not registered."));
                    }

                    var otpEntry = new ClinicOtpEntry
                    {
                        Email = dto.Email,
                        OtpCode = otp,
                        CreatedAt = now,
                        ExpiryTime = expiryTime
                    };

                    await _clinicRepository.AddOtpAsync(otpEntry);
                    await _clinicRepository.SaveChangesAsync();

                    var body = _emailTemplateService.GenerateClinicLoginOtpTemplate(otp, OtpValidityMinutes);
                    await _emailService.SendEmailAsync(dto.Email, "Your Hfiles Clinic OTP", body);

                    await transaction.CommitAsync();
                    _logger.LogInformation("OTP sent successfully to Email {Email}.", dto.Email);
                    return Ok(ApiResponseFactory.Success("OTP sent successfully to Email."));
                }
                else
                {
                    var user = await _clinicRepository.GetByPhoneAsync(dto.PhoneNumber!);
                    if (user == null)
                    {
                        _logger.LogWarning("OTP generation failed: Phone {PhoneNumber} not registered.", dto.PhoneNumber);
                        return BadRequest(ApiResponseFactory.Fail("Phone number not registered."));
                    }

                    var otpEntry = new ClinicOtpEntry
                    {
                        Email = dto.Email ?? dto.PhoneNumber!,
                        OtpCode = otp,
                        CreatedAt = now,
                        ExpiryTime = expiryTime
                    };

                    await _clinicRepository.AddOtpAsync(otpEntry);
                    await _clinicRepository.SaveChangesAsync();

                    try
                    {
                        await _whatsappService.SendOtpAsync(otp, dto.PhoneNumber!);
                        await transaction.CommitAsync();

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
