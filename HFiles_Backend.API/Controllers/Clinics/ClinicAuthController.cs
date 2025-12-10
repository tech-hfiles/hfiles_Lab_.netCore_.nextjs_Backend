using HFiles_Backend.API.Interfaces;
using HFiles_Backend.API.Services;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Clinics.Login;
using HFiles_Backend.Application.DTOs.Labs;
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





        // Clinic Signup
        [HttpPost("clinics")]
        public async Task<IActionResult> Signup([FromBody] Application.DTOs.Clinics.Signup.Signup dto)
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

                var user = new Domain.Entities.Clinics.ClinicSignup
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
                return Ok(ApiResponseFactory.Success(new { user.IsSuperAdmin }, "Registration successful."));
            }
            finally
            {
                if (transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.RollbackAsync();
                }
            }
        }





        // Clinic Login via OTP
        [HttpPost("clinics/login")]
        public async Task<IActionResult> LoginViaOtp([FromBody] ClinicOtpLogin dto)
        {
            HttpContext.Items["Log-Category"] = "Clinic Authentication";
            _logger.LogInformation("Received OTP login request for Email: {Email}, Phone: {PhoneNumber}", dto.Email, dto.PhoneNumber);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Model validation failed. Errors: {Errors}", string.Join(", ", errors));
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            var now = DateTime.UtcNow;
            object? responseData = null;
            ClinicOtpEntry? otpEntry = null;

            var transaction = await _clinicRepository.BeginTransactionAsync();

            try
            {
                if (!string.IsNullOrWhiteSpace(dto.Email))
                {
                    var user = await _clinicRepository.GetByEmailAsync(dto.Email);
                    if (user == null)
                    {
                        _logger.LogWarning("OTP login failed: Email {Email} not registered.", dto.Email);
                        return BadRequest(ApiResponseFactory.Fail("Email not registered."));
                    }

                    otpEntry = await _clinicRepository.GetLatestOtpAsync(dto.Email);
                    responseData = new
                    {
                        UserId = user.Id,
                        user.Email,
                        user.IsSuperAdmin
                    };
                }
                else if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
                {
                    var user = await _clinicRepository.GetByPhoneAsync(dto.PhoneNumber!);
                    if (user == null)
                    {
                        _logger.LogWarning("OTP login failed: Phone {PhoneNumber} not registered.", dto.PhoneNumber);
                        return BadRequest(ApiResponseFactory.Fail("Phone number not registered."));
                    }

                    otpEntry = await _clinicRepository.GetLatestOtpAsync(dto.PhoneNumber!);
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

                var expiredOtps = await _clinicRepository.GetExpiredOtpsAsync(dto.Email ?? dto.PhoneNumber!, now);
                await _clinicRepository.RemoveOtpAsync(otpEntry);
                await _clinicRepository.RemoveOtpsAsync(expiredOtps);

                await _clinicRepository.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("OTP login successful for identifier {Identifier}.", dto.Email ?? dto.PhoneNumber);
                return Ok(ApiResponseFactory.Success(responseData, "Login Successful."));
            }
            finally
            {
                if (transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.RollbackAsync();
                }
            }
        }





        // Clinic Login via Password
        [HttpPost("clinics/login/password")]
        public async Task<IActionResult> LoginViaPassword([FromBody] PasswordLogin dto)
        {
            HttpContext.Items["Log-Category"] = "Clinic Authentication";

            _logger.LogInformation("Received password login request for Email: {Email}", dto.Email);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                                              .Select(e => e.ErrorMessage)
                                              .ToList();
                _logger.LogWarning("Validation failed: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            var transaction = await _clinicRepository.BeginTransactionAsync();

            try
            {
                var user = await _clinicRepository.GetByEmailAsync(dto.Email);
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

                await transaction.CommitAsync();

                _logger.LogInformation("Password login successful for Email {Email}.", dto.Email);
                return Ok(ApiResponseFactory.Success(responseData, "Login Successful."));
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
