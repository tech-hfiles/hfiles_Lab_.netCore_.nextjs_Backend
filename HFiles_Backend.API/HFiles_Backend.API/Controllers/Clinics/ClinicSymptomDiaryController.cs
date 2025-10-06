using HFiles_Backend.API.DTOs.Clinics;
using HFiles_Backend.API.Interfaces;
using HFiles_Backend.API.Services;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mail;

namespace HFiles_Backend.API.Controllers.Clinics
{
    [Route("api/clinic")]
    [ApiController]
    public class ClinicSymptomDiaryController(
        ILogger<ClinicSymptomDiaryController> logger,
        IClinicAuthorizationService clinicAuthorizationService,
        IUserRepository userRepository,
        IClinicRepository clinicRepository,
        EmailService emailService,
        IEmailTemplateService emailTemplateService) : ControllerBase
    {
        private readonly ILogger<ClinicSymptomDiaryController> _logger = logger;
        private readonly IClinicAuthorizationService _clinicAuthorizationService = clinicAuthorizationService;
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IClinicRepository _clinicRepository = clinicRepository;
        private readonly EmailService _emailService = emailService;
        private readonly IEmailTemplateService _emailTemplateService = emailTemplateService;





        [HttpPost("send/symptom-diary")]
        [Authorize]
        public async Task<IActionResult> SendSymptomDiary([FromForm] SendSymptomDiaryRequest request)
        {
            HttpContext.Items["Log-Category"] = "Clinic Symptom Diary";

            // Validation
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed for symptom diary. Errors: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            if (request.SymptomDiaryFile == null || request.SymptomDiaryFile.Length == 0)
            {
                _logger.LogWarning("Symptom diary file is missing or empty for HFID: {HFID}", request.HFID);
                return BadRequest(ApiResponseFactory.Fail("Symptom diary file is required."));
            }

            // File validation
            var allowedExtensions = new[] { ".pdf", ".png", ".jpg", ".jpeg" };
            var extension = Path.GetExtension(request.SymptomDiaryFile.FileName)?.ToLowerInvariant();
            var maxFileSize = 10 * 1024 * 1024; // 10 MB

            if (!allowedExtensions.Contains(extension))
            {
                _logger.LogWarning("Invalid file type: {Extension} for HFID: {HFID}", extension, request.HFID);
                return BadRequest(ApiResponseFactory.Fail("Only PDF, DOC, DOCX, XLS, and XLSX files are allowed."));
            }

            if (request.SymptomDiaryFile.Length > maxFileSize)
            {
                _logger.LogWarning("File size exceeds limit. Size: {Size}MB for HFID: {HFID}",
                    request.SymptomDiaryFile.Length / (1024 * 1024), request.HFID);
                return BadRequest(ApiResponseFactory.Fail("File size must not exceed 10MB."));
            }

            // Authorization check
            bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(request.ClinicId, User);
            if (!isAuthorized)
            {
                _logger.LogWarning("Unauthorized symptom diary send attempt for Clinic ID {ClinicId}", request.ClinicId);
                return Unauthorized(ApiResponseFactory.Fail("You are not authorized to send symptom diaries for this clinic."));
            }

            try
            {
                // Get user by HFID
                var user = await _userRepository.GetUserByHFIDAsync(request.HFID);
                if (user == null)
                {
                    _logger.LogWarning("User not found for HFID: {HFID}", request.HFID);
                    return NotFound(ApiResponseFactory.Fail($"User with HFID {request.HFID} not found."));
                }

                if (string.IsNullOrWhiteSpace(user.Email))
                {
                    _logger.LogWarning("User email not found for HFID: {HFID}", request.HFID);
                    return BadRequest(ApiResponseFactory.Fail($"No email address found for user with HFID {request.HFID}."));
                }

                HttpContext.Items["Sent-To-UserId"] = user.Id ;

                // Get clinic details
                var clinic = await _clinicRepository.GetByIdAsync(request.ClinicId);
                if (clinic == null)
                {
                    _logger.LogWarning("Clinic not found for Clinic ID: {ClinicId}", request.ClinicId);
                    return NotFound(ApiResponseFactory.Fail($"Clinic with ID {request.ClinicId} not found."));
                }

                var clinicName = clinic.ClinicName ?? "Clinic";

                // Prepare email
                var emailSubject = $"Symptom Diary from {clinicName}";
                var emailBody = _emailTemplateService.GenerateEmailBodySymptomDiary(user.FirstName, clinicName);

                // Create attachment
                var attachments = new List<Attachment>();
                using (var memoryStream = new MemoryStream())
                {
                    await request.SymptomDiaryFile.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;

                    var attachment = new Attachment(
                        memoryStream,
                        request.SymptomDiaryFile.FileName,
                        request.SymptomDiaryFile.ContentType
                    );
                    attachments.Add(attachment);

                    // Send email
                    await _emailService.SendEmailWithAttachmentsAsync(
                        user.Email,
                        emailSubject,
                        emailBody,
                        attachments
                    );
                }

                _logger.LogInformation(
                    "Symptom diary sent successfully to {Email} for HFID: {HFID} from Clinic ID: {ClinicId}",
                    user.Email, request.HFID, request.ClinicId
                );
                // Create user notification message
                var userNotificationMessage = $"{clinicName} has sent you a symptom diary to fill out. Please check your email at {user.Email} to complete it.";
                // Send notification
                var notificationMessage = $"{clinicName} has sent you a symptom diary over your email {user.Email}. Kindly check your inbox.";

                //await _notificationService.SendNotificationAsync(new NotificationRequest
                //{
                //    UserId = user.Id,
                //    Title = "Symptom Diary Received",
                //    Message = notificationMessage,
                //    Type = "SymptomDiary",
                //    ClinicId = request.ClinicId
                //});

                _logger.LogInformation(
                    "Notification sent to User ID: {UserId} for symptom diary from Clinic ID: {ClinicId}",
                    user.Id, request.ClinicId
                );

                var response = new
                {
                    HFID = request.HFID,
                    PatientName = $"{user.FirstName} {user.LastName}".Trim(),
                    Email = user.Email,
                    ClinicName = clinicName,
                    FileName = request.SymptomDiaryFile.FileName,
                    FileSizeMB = Math.Round((decimal)request.SymptomDiaryFile.Length / (1024 * 1024), 2),
                    SentAt = DateTime.UtcNow,
                    notificationMessage = notificationMessage,
                    UserNotificationMessage = userNotificationMessage
                };

                return Ok(ApiResponseFactory.Success(response, "Symptom diary sent successfully via email and notification sent."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending symptom diary for HFID: {HFID}", request.HFID);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while sending the symptom diary."));
            }
        }
    }
}