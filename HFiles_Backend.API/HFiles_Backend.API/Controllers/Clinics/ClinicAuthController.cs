using HFiles_Backend.API.Interfaces;
using HFiles_Backend.API.Services;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;

namespace HFiles_Backend.API.Controllers.Clinics
{
    [Route("api/")]
    [ApiController]
    public class ClinicAuthController(
        ILogger<ClinicAuthController> logger,
        IClinicRepository clinicRepository,
         IPasswordHasher<ClinicSignup> passwordHasher,
         EmailService emailService,
         IEmailTemplateService emailTemplateService
        ) : ControllerBase
    {
        private readonly ILogger<ClinicAuthController> _logger = logger;
        private readonly IClinicRepository _clinicRepository = clinicRepository;
        private readonly IPasswordHasher<ClinicSignup> _passwordHasher = passwordHasher;
        private readonly EmailService _emailService = emailService;
        private readonly IEmailTemplateService _emailTemplateService = emailTemplateService;





        [HttpPost("clinics")]
        public async Task<IActionResult> Signup([FromBody] Application.DTOs.Clinics.ClinicSignup dto)
        {
            HttpContext.Items["Log-Category"] = "Clinic Management";
            _logger.LogInformation("Signup attempt initiated for Email: {Email}", dto.Email);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Model validation failed for Email: {Email}. Errors: {Errors}", dto.Email, string.Join(", ", errors));
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            var transaction = await _clinicRepository.BeginTransactionAsync();

            try
            {
                if (await _clinicRepository.EmailExistsAsync(dto.Email))
                {
                    _logger.LogWarning("Signup failed: Email already registered - {Email}", dto.Email);
                    return BadRequest(ApiResponseFactory.Fail("Email already registered."));
                }

                var otpEntry = await _clinicRepository.GetLatestOtpAsync(dto.Email);
                if (otpEntry == null || otpEntry.ExpiryTime < DateTime.UtcNow || otpEntry.OtpCode != dto.Otp)
                {
                    _logger.LogWarning("Signup failed: OTP issue for Email: {Email}", dto.Email);
                    return BadRequest(ApiResponseFactory.Fail("Invalid or expired OTP."));
                }

                var epochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var last6Epoch = epochTime % 1000000;
                var clinicPrefix = dto.ClinicName.Length >= 3 ? dto.ClinicName.Substring(0, 3).ToUpper() : dto.ClinicName.ToUpper();
                var hfid = $"HF{last6Epoch}{clinicPrefix}{new Random().Next(1000, 9999)}";

                var user = new ClinicSignup
                {
                    ClinicName = dto.ClinicName,
                    Email = dto.Email,
                    PhoneNumber = dto.PhoneNumber,
                    Pincode = dto.Pincode,
                    CreatedAtEpoch = epochTime,
                    HFID = hfid,
                    IsSuperAdmin = false,
                    PasswordHash = _passwordHasher.HashPassword(null!, dto.Password)
                };

                await _clinicRepository.AddSignupAsync(user);
                await _clinicRepository.RemoveOtpAsync(otpEntry);
                await _clinicRepository.SaveChangesAsync();
                await transaction.CommitAsync();

                var userEmailBody = _emailTemplateService.GenerateClinicWelcomeTemplate(dto.ClinicName);
                var adminEmailBody = _emailTemplateService.GenerateClinicAdminNotificationTemplate(dto.ClinicName, dto.Email, dto.PhoneNumber, dto.Pincode);

                await _emailService.SendEmailAsync(dto.Email, "Hfiles Clinic Registration", userEmailBody);
                await _emailService.SendEmailAsync("hfilessocial@gmail.com", "New Clinic Signup", adminEmailBody);

                _logger.LogInformation("Signup successful for Email: {Email}", dto.Email);
                return Ok(ApiResponseFactory.Success(new { user.IsSuperAdmin }, "Clinic registered successfully."));
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
