using HFiles_Backend.API.DTOs.Clinics;
using HFiles_Backend.API.Interfaces;
using HFiles_Backend.API.Services;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Entities.Users;
using HFiles_Backend.Domain.Enums;
using HFiles_Backend.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;
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
        IEmailTemplateService emailTemplateService,
        S3StorageService s3StorageService,
        IClinicPatientRecordRepository clinicPatientRecordRepository) : ControllerBase
    {
        private readonly ILogger<ClinicSymptomDiaryController> _logger = logger;
        private readonly IClinicAuthorizationService _clinicAuthorizationService = clinicAuthorizationService;
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IClinicRepository _clinicRepository = clinicRepository;
        private readonly EmailService _emailService = emailService;
        private readonly IEmailTemplateService _emailTemplateService = emailTemplateService;
        private readonly S3StorageService _s3StorageService = s3StorageService;
        private readonly IClinicPatientRecordRepository _clinicPatientRecordRepository = clinicPatientRecordRepository;
                    
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
            var allowedExtensions = new[] { ".pdf", ".png", ".jpg", ".jpeg", ".xlsx" };
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
                    return BadRequest(ApiResponseFactory.Fail($"No email address found for this user. Kindly advice the user to update the email address to share symptom diary"));
                }

                HttpContext.Items["Sent-To-UserId"] = user.Id;

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
                //var notificationMessage = $"{clinicName} has sent you a symptom diary over your email {user.Email}. Kindly check your inbox.";

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
                    //notificationMessage = notificationMessage,
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




        [HttpPost("patient/records/upload/symptom-diary")]
        [Authorize]
        public async Task<IActionResult> UploadSymptomDiary(
    [FromForm] ClinicSymptomDiaryUploadRequest request)
        {
            HttpContext.Items["Log-Category"] = "Symptom Diary Upload";

            if (!ModelState.IsValid || request.File == null)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed for symptom diary upload. Errors: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            // File type validation
            var allowedExtensions = new[] { ".pdf", ".png", ".jpg", ".jpeg", ".xlsx" };
            var extension = Path.GetExtension(request.File.FileName)?.ToLowerInvariant();
            var fileSize = request.File.Length;

            if (fileSize > 10 * 1024 * 1024)
            {
                _logger.LogWarning("File size exceeds limit for symptom diary. Size: {Size}MB", fileSize / (1024 * 1024));
                return BadRequest(ApiResponseFactory.Fail("File size exceeds the 10MB limit."));
            }

            if (!allowedExtensions.Contains(extension) ||
                (!request.File.ContentType.StartsWith("application/") && !request.File.ContentType.StartsWith("image/")))
            {
                _logger.LogWarning("Invalid file type attempted: {FileName} with ContentType: {ContentType}",
                    request.File.FileName, request.File.ContentType);
                return BadRequest(ApiResponseFactory.Fail("Unsupported file type. Only PDF, JPG, JPEG, PNG, and XLSX files are allowed."));
            }

            bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(request.ClinicId, User);
            if (!isAuthorized)
            {
                _logger.LogWarning("Unauthorized symptom diary upload attempt for Clinic ID {ClinicId}", request.ClinicId);
                return Unauthorized(ApiResponseFactory.Fail("You are not authorized to upload records for this clinic."));
            }

            await using var transaction = await _clinicRepository.BeginTransactionAsync();
            bool committed = false;

            try
            {
                // Get clinic patient details
                var clinicPatient = await _clinicPatientRecordRepository.GetByIdAsync(request.PatientId);
                if (clinicPatient == null)
                    return NotFound(ApiResponseFactory.Fail("Clinic patient not found."));

                // Get user by HFID
                var user = await _userRepository.GetUserByHFIDAsync(clinicPatient.HFID);
                if (user == null)
                    return NotFound(ApiResponseFactory.Fail("User not found for provided HFID."));

                // Email validation - check if user has email
                if (string.IsNullOrWhiteSpace(user.Email))
                {
                    _logger.LogWarning("User email not found for HFID: {HFID}", clinicPatient.HFID);
                    return BadRequest(ApiResponseFactory.Fail($"No email address found for this user. Kindly advise the user to update the email address to receive symptom diary notifications."));
                }

                HttpContext.Items["Sent-To-UserId"] = user.Id;

                // Get clinic details
                var clinic = await _clinicRepository.GetByIdAsync(request.ClinicId);
                if (clinic == null)
                {
                    _logger.LogWarning("Clinic not found for Clinic ID: {ClinicId}", request.ClinicId);
                    return NotFound(ApiResponseFactory.Fail($"Clinic with ID {request.ClinicId} not found."));
                }

                var clinicName = clinic.ClinicName ?? "Clinic";

                var fileName = $"Symptom_Diary_{user.FirstName}_{user.LastName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}{extension}";
                var tempPath = Path.Combine(Path.GetTempPath(), fileName);

                using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    await request.File.CopyToAsync(stream);
                }

                var s3Url = await _s3StorageService.UploadFileToS3(tempPath, $"clinic-records/{fileName}");

                System.IO.File.Delete(tempPath);

                if (s3Url == null)
                {
                    _logger.LogError("Failed to upload symptom diary to S3 for Patient ID {PatientId}", request.PatientId);
                    return StatusCode(500, ApiResponseFactory.Fail("Failed to upload file to storage."));
                }

                var jsonData = System.Text.Json.JsonSerializer.Serialize(new { url = s3Url });

                var record = new ClinicPatientRecord
                {
                    ClinicId = request.ClinicId,
                    PatientId = request.PatientId,
                    ClinicVisitId = request.ClinicVisitId,
                    Type = RecordType.SymptomDiary,
                    JsonData = jsonData,
                    SendToPatient = true
                };

                await _clinicPatientRecordRepository.SaveAsync(record);

                // Save to UserReports with SpecialReports category
                var userReport = new UserReport
                {
                    UserId = user.Id,
                    ReportName = fileName,
                    ReportCategory = (int)ReportType.SpecialReport,
                    ReportUrl = s3Url,
                    EpochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    FileSize = Math.Round((decimal)fileSize / 1024, 2),
                    UploadedBy = "Clinic",
                    UserType = user.UserReference == 0 ? "Independent" : "Dependent",
                    DeletedBy = 0
                };

                await _userRepository.SaveAsync(userReport);

                // Prepare and send email notification with the same template as SendSymptomDiary
                var emailSubject = $"Symptom Diary from {clinicName}";
                var emailBody = _emailTemplateService.GenerateEmailBodySymptomDiary(user.FirstName, clinicName);

                // Create attachment from the uploaded file
                var attachments = new List<Attachment>();
                using (var memoryStream = new MemoryStream())
                {
                    await request.File.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;

                    var attachment = new Attachment(
                        memoryStream,
                        request.File.FileName,
                        request.File.ContentType
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
                    "Symptom diary uploaded and email sent to {Email} for HFID: {HFID} from Clinic ID: {ClinicId}",
                    user.Email, clinicPatient.HFID, request.ClinicId
                );

                await transaction.CommitAsync();
                committed = true;

                // Create user notification message
                var userNotificationMessage = $"{clinicName} has uploaded a symptom diary for you. Please check your email at {user.Email} to view it.";

                // Optional: Send in-app notification (uncomment if needed)
                //await _notificationService.SendNotificationAsync(new NotificationRequest
                //{
                //    UserId = user.Id,
                //    Title = "Symptom Diary Uploaded",
                //    Message = userNotificationMessage,
                //    Type = "SymptomDiary",
                //    ClinicId = request.ClinicId
                //});

                _logger.LogInformation("Uploaded symptom diary for Clinic ID {ClinicId}, Patient ID {PatientId}, User ID {UserId}",
                    request.ClinicId, request.PatientId, user.Id);

                var response = new
                {
                    FileUrl = s3Url,
                    FileName = request.File.FileName,
                    FileSizeMB = Math.Round((decimal)fileSize / (1024 * 1024), 2),
                    PatientName = clinicPatient.PatientName,
                    HFID = clinicPatient.HFID,
                    Email = user.Email,
                    ClinicName = clinicName,
                    Category = "Special Reports",
                    SavedToUserReports = true,
                    SentAt = DateTime.UtcNow,
                    UserNotificationMessage = userNotificationMessage
                };

                return Ok(ApiResponseFactory.Success(response, "Symptom diary uploaded successfully and sent via email."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during symptom diary upload for Clinic ID {ClinicId}", request.ClinicId);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while uploading symptom diary."));
            }
            finally
            {
                if (!committed && transaction.GetDbTransaction().Connection != null)
                    await transaction.RollbackAsync();
            }
        }


        // Upload Symptom Diary
        //[HttpPost("patient/records/upload/symptom-diary")]
        //[Authorize]
        //public async Task<IActionResult> UploadSymptomDiary(
        //    [FromForm] ClinicSymptomDiaryUploadRequest request)
        //{
        //    HttpContext.Items["Log-Category"] = "Symptom Diary Upload";

        //    if (!ModelState.IsValid || request.File == null)
        //    {
        //        var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
        //        _logger.LogWarning("Validation failed for symptom diary upload. Errors: {@Errors}", errors);
        //        return BadRequest(ApiResponseFactory.Fail(errors));
        //    }

        //    // File type validation
        //    var allowedExtensions = new[] { ".pdf", ".png", ".jpg", ".jpeg", ".xlsx" };
        //    var extension = Path.GetExtension(request.File.FileName)?.ToLowerInvariant();
        //    var fileSize = request.File.Length;

        //    if (fileSize > 10 * 1024 * 1024)
        //    {
        //        _logger.LogWarning("File size exceeds limit for symptom diary. Size: {Size}MB", fileSize / (1024 * 1024));
        //        return BadRequest(ApiResponseFactory.Fail("File size exceeds the 10MB limit."));
        //    }

        //    if (!allowedExtensions.Contains(extension) ||
        //        (!request.File.ContentType.StartsWith("application/") && !request.File.ContentType.StartsWith("image/")))
        //    {
        //        _logger.LogWarning("Invalid file type attempted: {FileName} with ContentType: {ContentType}",
        //            request.File.FileName, request.File.ContentType);
        //        return BadRequest(ApiResponseFactory.Fail("Unsupported file type. Only PDF, JPG, JPEG, and PNG files are allowed."));
        //    }

        //    bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(request.ClinicId, User);
        //    if (!isAuthorized)
        //    {
        //        _logger.LogWarning("Unauthorized symptom diary upload attempt for Clinic ID {ClinicId}", request.ClinicId);
        //        return Unauthorized(ApiResponseFactory.Fail("You are not authorized to upload records for this clinic."));
        //    }

        //    await using var transaction = await _clinicRepository.BeginTransactionAsync();
        //    bool committed = false;

        //    try
        //    {
        //        // Get clinic patient details
        //        var clinicPatient = await _clinicPatientRecordRepository.GetByIdAsync(request.PatientId);
        //        if (clinicPatient == null)
        //            return NotFound(ApiResponseFactory.Fail("Clinic patient not found."));

        //        // Get user by HFID
        //        var user = await _userRepository.GetUserByHFIDAsync(clinicPatient.HFID);
        //        if (user == null)
        //            return NotFound(ApiResponseFactory.Fail("User not found for provided HFID."));

        //        var fileName = $"Symptom_Diary_{user.FirstName + " " + user.LastName}.{extension}";
        //        var tempPath = Path.Combine(Path.GetTempPath(), fileName);

        //        using (var stream = new FileStream(tempPath, FileMode.Create))
        //        {
        //            await request.File.CopyToAsync(stream);
        //        }

        //        var s3Url = await _s3StorageService.UploadFileToS3(tempPath, $"clinic-records/{fileName}");

        //        System.IO.File.Delete(tempPath);

        //        if (s3Url == null)
        //        {
        //            _logger.LogError("Failed to upload symptom diary to S3 for Patient ID {PatientId}", request.PatientId);
        //            return StatusCode(500, ApiResponseFactory.Fail("Failed to upload file to storage."));
        //        }

        //        var jsonData = System.Text.Json.JsonSerializer.Serialize(new { url = s3Url });

        //        var record = new ClinicPatientRecord
        //        {
        //            ClinicId = request.ClinicId,
        //            PatientId = request.PatientId,
        //            ClinicVisitId = request.ClinicVisitId,
        //            Type = RecordType.SymptomDiary,
        //            JsonData = jsonData,
        //            SendToPatient = true
        //        };

        //        await _clinicPatientRecordRepository.SaveAsync(record);

        //        // Save to UserReports with SpecialReports category
        //        var userReport = new UserReport
        //        {
        //            UserId = user.Id,
        //            ReportName = fileName,
        //            ReportCategory = (int)ReportType.SpecialReport,
        //            ReportUrl = s3Url,
        //            EpochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        //            FileSize = Math.Round((decimal)fileSize / 1024, 2),
        //            UploadedBy = "Clinic",
        //            UserType = user.UserReference == 0 ? "Independent" : "Dependent",
        //            DeletedBy = 0
        //        };

        //        await _userRepository.SaveAsync(userReport);

        //        await transaction.CommitAsync();
        //        committed = true;

        //        _logger.LogInformation("Uploaded symptom diary for Clinic ID {ClinicId}, Patient ID {PatientId}, User ID {UserId}",
        //            request.ClinicId, request.PatientId, user.Id);

        //        return Ok(ApiResponseFactory.Success(new
        //        {
        //            FileUrl = s3Url,
        //            FileName = request.File.FileName,
        //            FileSizeMB = Math.Round((decimal)fileSize / (1024 * 1024), 2),
        //            PatientName = clinicPatient.PatientName,
        //            HFID = clinicPatient.HFID,
        //            Category = "Special Reports",
        //            SavedToUserReports = true
        //        }, "Symptom diary uploaded successfully."));
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error during symptom diary upload for Clinic ID {ClinicId}", request.ClinicId);
        //        return StatusCode(500, ApiResponseFactory.Fail("An error occurred while uploading symptom diary."));
        //    }
        //    finally
        //    {
        //        if (!committed && transaction.GetDbTransaction().Connection != null)
        //            await transaction.RollbackAsync();
        //    }
        //}
    }
}