using HFiles_Backend.API.Interfaces;
using HFiles_Backend.API.Services;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Clinics.Login;
using HFiles_Backend.Application.DTOs.Clinics.ResetPassword;
using HFiles_Backend.Application.DTOs.Clinics.Signup;
using HFiles_Backend.Application.DTOs.Labs;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace HFiles_Backend.API.Controllers.Clinics
{
    [Route("api/")]
    [ApiController]
    public class ClinicOtpController(
        ILogger<ClinicOtpController> logger,
        IClinicRepository clinicRepository,
        EmailService emailService,
        IWhatsappService whatsappService,
        IEmailTemplateService emailTemplateService,
        IUserRepository userRepository,
        IClinicSuperAdminRepository clinicSuperAdminRepository,
        IClinicMemberRepository clinicMemberRepository,
         IWebHostEnvironment environment
        ) : ControllerBase
    {
        private readonly ILogger<ClinicOtpController> _logger = logger;
        private readonly IClinicRepository _clinicRepository = clinicRepository;
        private readonly EmailService _emailService = emailService;
        private readonly IWhatsappService _whatsappService = whatsappService;
        private readonly IEmailTemplateService _emailTemplateService = emailTemplateService;
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IClinicSuperAdminRepository _clinicSuperAdminRepository = clinicSuperAdminRepository;
        private readonly IClinicMemberRepository _clinicMemberRepository = clinicMemberRepository;
        private const int OtpValidityMinutes = 5;
        private readonly IWebHostEnvironment _environment = environment;


        private static bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsValidPhoneNumber(string phoneNumber)
        {
            // Use the same regex as in your WhatsApp service
            var match = Regex.Match(phoneNumber, @"^(\+?\d{1,4})?(\d{10})$");
            return match.Success;
        }

        private string GetWhatsAppErrorMessage(Exception whatsappException)
        {
            var errorMessage = whatsappException.Message;

            if (errorMessage.Contains("Invalid mobile number format"))
            {
                return "Invalid mobile number format. Please provide a valid 10-digit mobile number.";
            }
            else if (errorMessage.Contains("Failed to send WhatsApp message"))
            {
                return "Failed to send OTP via WhatsApp. Please verify your phone number and try again.";
            }
            else if (errorMessage.Contains("Failed to send OTP. Please check API connectivity"))
            {
                return "WhatsApp service is temporarily unavailable. Please try again later.";
            }
            else
            {
                return "Failed to send OTP via WhatsApp. Please check your phone number and try again.";
            }
        }





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

            if (await _clinicRepository.EmailExistsAsync(dto.Email))
            {
                _logger.LogWarning("Signup failed: Email already registered - {Email}", dto.Email);
                return BadRequest(ApiResponseFactory.Fail("Email already registered."));
            }

            _logger.LogInformation("Clinic OTP requested for Email: {Email}, Phone: {Phone}", dto.Email, dto.PhoneNumber);

            var otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            var now = DateTime.UtcNow;
            var transaction = await _clinicRepository.BeginTransactionAsync();
            var committed = false;

            try
            {
                // First validate both email and WhatsApp before proceeding
                _logger.LogInformation("Validating email and WhatsApp services before generating OTP");

                // Validate email format and service availability
                if (string.IsNullOrWhiteSpace(dto.Email) || !IsValidEmail(dto.Email))
                {
                    _logger.LogWarning("Invalid email format provided: {Email}", dto.Email);
                    return BadRequest(ApiResponseFactory.Fail("Invalid email format provided."));
                }

                // Validate phone number format
                if (string.IsNullOrWhiteSpace(dto.PhoneNumber) || !IsValidPhoneNumber(dto.PhoneNumber))
                {
                    _logger.LogWarning("Invalid phone number format provided: {PhoneNumber}", dto.PhoneNumber);
                    return BadRequest(ApiResponseFactory.Fail("Invalid phone number format provided."));
                }

                // Create OTP entry
                var otpEntry = new ClinicOtpEntry
                {
                    Email = dto.Email,
                    OtpCode = otp,
                    CreatedAt = now,
                    ExpiryTime = now.AddMinutes(5)
                };

                await _clinicRepository.AddOtpAsync(otpEntry);
                await _clinicRepository.SaveChangesAsync();

                _logger.LogInformation("Clinic OTP {Otp} generated for Email {Email} at {Time}", otp, dto.Email, now);

                // Prepare email content
                var subject = "Complete Your Hfiles Clinic Registration";
                var body = _emailTemplateService.GenerateClinicOtpTemplate(dto.ClinicName, otp);

                // Send both email and WhatsApp - if either fails, rollback
                var emailTask = _emailService.SendEmailAsync(dto.Email, subject, body);
                var whatsappTask = _whatsappService.SendOtpAsync(otp, dto.PhoneNumber);

                try
                {
                    // Wait for both to complete
                    await Task.WhenAll(emailTask, whatsappTask);

                    await transaction.CommitAsync();
                    committed = true;

                    _logger.LogInformation("Clinic OTP successfully sent to both {Email} and {Phone}", dto.Email, dto.PhoneNumber);

                    return Ok(ApiResponseFactory.Success(new
                    {
                        dto.Email,
                        dto.PhoneNumber,
                        ExpiresInMinutes = 5
                    }, "OTP has been sent to your email and phone number."));
                }
                catch (Exception serviceEx)
                {
                    _logger.LogError(serviceEx, "Failed to send OTP via email or WhatsApp for {Email}, {Phone}", dto.Email, dto.PhoneNumber);

                    // Determine which service failed and return appropriate error
                    var emailException = emailTask.IsFaulted ? emailTask.Exception?.GetBaseException() : null;
                    var whatsappException = whatsappTask.IsFaulted ? whatsappTask.Exception?.GetBaseException() : null;

                    if (emailException != null && whatsappException != null)
                    {
                        return StatusCode(500, ApiResponseFactory.Fail("Failed to send OTP via both email and WhatsApp. Please try again later."));
                    }
                    else if (emailException != null)
                    {
                        return StatusCode(500, ApiResponseFactory.Fail("Failed to send OTP via email. Please check your email address and try again."));
                    }
                    else if (whatsappException != null)
                    {
                        // Handle specific WhatsApp exceptions
                        var whatsappError = GetWhatsAppErrorMessage(whatsappException);
                        return BadRequest(ApiResponseFactory.Fail(whatsappError));
                    }
                    else
                    {
                        return StatusCode(500, ApiResponseFactory.Fail("An unexpected error occurred while sending OTP."));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during clinic OTP generation for {Email}", dto.Email);
                return StatusCode(500, ApiResponseFactory.Fail("An unexpected error occurred while generating OTP. Please try again."));
            }
            finally
            {
                if (!committed && transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.RollbackAsync();
                    _logger.LogWarning("Transaction rolled back for clinic OTP generation");
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





        // Send OTP for clinic Reset Password
        [HttpPost("clinics/password-reset/request")]
        public async Task<IActionResult> RequestClinicPasswordReset([FromBody] PasswordResetRequest dto)
        {
            HttpContext.Items["Log-Category"] = "Authentication";
            _logger.LogInformation("Received password reset request for Email: {Email}", dto.Email);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            var clinicUser = await _clinicRepository.GetClinicByEmailAsync(dto.Email);
            if (clinicUser == null)
            {
                _logger.LogWarning("No clinic user found with Email {Email}", dto.Email);
                return NotFound(ApiResponseFactory.Fail("No clinic user found with this email."));
            }

            if (string.IsNullOrWhiteSpace(clinicUser.Email))
            {
                _logger.LogWarning("Clinic user Email is missing for {Email}", dto.Email);
                return StatusCode(500, ApiResponseFactory.Fail("Clinic user email is missing."));
            }

            var mainClinic = await _clinicRepository.GetMainClinicAsync(clinicUser.Id);
            if (mainClinic == null || string.IsNullOrWhiteSpace(mainClinic.Email))
            {
                _logger.LogWarning("Main clinic Email is missing for reference ID {RefId}", clinicUser.ClinicReference);
                return StatusCode(500, ApiResponseFactory.Fail("Main clinic email is missing."));
            }

            var now = DateTime.UtcNow;
            var otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            var otpEntry = new ClinicOtpEntry
            {
                Email = dto.Email,
                OtpCode = otp,
                CreatedAt = now,
                ExpiryTime = now.AddMinutes(OtpValidityMinutes)
            };

            var transaction = await _clinicRepository.BeginTransactionAsync();
            var committed = false;

            try
            {
                _clinicRepository.AddOtpEntry(otpEntry);
                await _clinicRepository.SaveChangesAsync();

                string recipientEmail = mainClinic.Email;
                // Set reset link based on environment
                string resetLink = _environment.IsDevelopment()
                    ? "http://localhost:3000/forgotPassword"
                    : "https://hfiles.co.in/forgotPassword";
                //: "https://clinicdemo.hfiles.co.in/forgotPassword";


                string emailBody = _emailTemplateService.GenerateClinicPasswordResetTemplate(
                    clinicUser.ClinicName, otp, OtpValidityMinutes, resetLink);

                await _emailService.SendEmailAsync(recipientEmail, $"Password Reset Request for {clinicUser.ClinicName}", emailBody);
                await transaction.CommitAsync();
                committed = true;

                _logger.LogInformation("Password reset OTP sent to {Email}", recipientEmail);

                return Ok(ApiResponseFactory.Success(new
                {
                    RecipientEmail = recipientEmail,
                    clinicUser.ClinicName
                }, $"Password reset link sent to {recipientEmail}."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error during clinic password reset for {Email}", dto.Email);
                return StatusCode(500, ApiResponseFactory.Fail($"An error occurred while sending the reset link: {ex.Message}"));
            }
            finally
            {
                if (!committed && transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.RollbackAsync();
                }
            }
        }





        // Send OTP for clinic Users Reset Password
        [HttpPost("clinics/users/password-reset/request")]
        public async Task<IActionResult> UsersRequestClinicPasswordReset(
        [FromBody] ClinicUserPasswordResetRequest dto)
        {
            HttpContext.Items["Log-Category"] = "Authentication";
            _logger.LogInformation("Received password reset request for User Email: {Email}, Clinic ID: {UserId}", dto.Email, dto.ClinicId);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            var userDetails = await _userRepository.GetVerifiedUserByEmailAsync(dto.Email);
            if (userDetails == null || string.IsNullOrWhiteSpace(userDetails.Email) || string.IsNullOrWhiteSpace(userDetails.FirstName))
            {
                _logger.LogWarning("No verified user found for {Email}", dto.Email);
                return NotFound(ApiResponseFactory.Fail("No user found with this email or email not verified."));
            }

            int userId = userDetails.Id;
            string? recipientEmail = null;
            string? userRole = null;

            var superAdmin = await _clinicSuperAdminRepository.GetMainSuperAdminAsync(userId, dto.ClinicId);
            if (superAdmin != null)
            {
                recipientEmail = userDetails.Email;
                userRole = "Super Admin";
            }
            else
            {
                var member = await _clinicMemberRepository.GetActiveMemberAsync(userId, dto.ClinicId);
                if (member != null)
                {
                    recipientEmail = userDetails.Email;
                    userRole = member.Role;
                }
            }

            if (recipientEmail == null)
            {
                _logger.LogWarning("No matching user found for Clinic ID {ClinicId}", dto.ClinicId);
                return NotFound(ApiResponseFactory.Fail("No matching user found for password reset."));
            }

            var clinic = await _clinicRepository.GetClinicByIdAsync(dto.ClinicId);
            if (clinic == null)
            {
                _logger.LogWarning("Clinic not found for ID {ClinicId}", dto.ClinicId);
                return NotFound(ApiResponseFactory.Fail("Clinic not found."));
            }

            var otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            var now = DateTime.UtcNow;
            var otpEntry = new ClinicOtpEntry
            {
                Email = dto.Email,
                OtpCode = otp,
                CreatedAt = now,
                ExpiryTime = now.AddMinutes(OtpValidityMinutes)
            };

            var transaction = await _clinicRepository.BeginTransactionAsync();
            var committed = false;

            try
            {
                _clinicRepository.AddOtpEntry(otpEntry);
                await _clinicRepository.SaveChangesAsync();

                // Set reset link based on environment
                string resetLink = _environment.IsDevelopment()
                    ? "http://localhost:3000/forgotPassword"
                    //: "https://clinicdemo.hfiles.co.in/forgotPassword";
                    : "https://hfiles.co.in/forgotPassword";
                string emailBody = _emailTemplateService.GenerateClinicUserPasswordResetTemplate(
                    userDetails.FirstName, clinic.ClinicName, otp, OtpValidityMinutes, resetLink);

                await _emailService.SendEmailAsync(recipientEmail, $"Password Reset Request for {userDetails.FirstName} {userDetails.LastName}", emailBody);
                await transaction.CommitAsync();
                committed = true;

                _logger.LogInformation("Password reset email sent to {Email}", recipientEmail);

                return Ok(ApiResponseFactory.Success(new
                {
                    RecipientEmail = recipientEmail,
                    UserRole = userRole,
                    clinic.ClinicName
                }, $"Password reset link sent to {recipientEmail} ({userRole})."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during clinic user password reset for {Email}", dto.Email);
                return StatusCode(500, ApiResponseFactory.Fail($"An error occurred while sending the reset link: {ex.Message}"));
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
