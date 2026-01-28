using HFiles_Backend.API.Interfaces;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Clinics.Profile;
using HFiles_Backend.Application.DTOs.Users;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace HFiles_Backend.API.Controllers.Clinics
{
    [Route("api/")]
    [ApiController]
    public class ClinicProfileController(
        ILogger<ClinicProfileController> logger,
        IClinicRepository clinicRepository,
        IClinicSuperAdminRepository clinicSuperAdminRepository,
        IClinicMemberRepository clinicMemberRepository,
        IClinicAuthorizationService clinicAuthorizationService,
        IUserRepository userRepository,
        IWebHostEnvironment env,
        S3StorageService s3Service,
       IClinicPatientRecordRepository clinicPatientRecordRepository
        ) : ControllerBase
    {
        private readonly ILogger<ClinicProfileController> _logger = logger;
        private readonly IClinicRepository _clinicRepository = clinicRepository;
        private readonly IClinicSuperAdminRepository _clinicSuperAdminRepository = clinicSuperAdminRepository;
        private readonly IClinicMemberRepository _clinicMemberRepository = clinicMemberRepository;
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IWebHostEnvironment _env = env;
        private readonly S3StorageService _s3Service = s3Service;
        private readonly IClinicAuthorizationService _clinicAuthorizationService = clinicAuthorizationService;
        private readonly IClinicPatientRecordRepository _clinicPatientRecordRepository = clinicPatientRecordRepository;





        // Clinic Profile Update
        [HttpPatch("clinics/update")]
        [Authorize(Policy = "SuperAdminOrAdminPolicy")]
        public async Task<IActionResult> UpdateClinicProfile([FromForm] ClinicProfileUpdate dto, IFormFile? ProfilePhoto)
        {
            HttpContext.Items["Log-Category"] = "Clinic Management";
            _logger.LogInformation("UpdateClinicProfile started for ClinicId: {ClinicId}", dto.ClinicId);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                _logger.LogWarning("Model validation failed for ClinicId: {ClinicId}. Errors: {Errors}", dto.ClinicId, string.Join(", ", errors));
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            var transaction = await _clinicRepository.BeginTransactionAsync();

            try
            {
                var clinic = await _clinicRepository.GetByIdAsync(dto.ClinicId);
                if (clinic == null)
                {
                    _logger.LogWarning("Clinic not found for ID: {ClinicId}", dto.ClinicId);
                    return NotFound(ApiResponseFactory.Fail($"Clinic with ID {dto.ClinicId} not found."));
                }

                if (!string.IsNullOrWhiteSpace(dto.Address))
                {
                    clinic.Address = dto.Address;
                    _logger.LogInformation("Updated address for ClinicId: {ClinicId}", dto.ClinicId);
                }

                if (ProfilePhoto is { Length: > 0 })
                {
                    var tempFolder = Path.Combine(_env.WebRootPath ?? "wwwroot", "temp-profiles");
                    if (!Directory.Exists(tempFolder)) Directory.CreateDirectory(tempFolder);

                    DateTime createdAt = DateTimeOffset.FromUnixTimeSeconds(clinic.CreatedAtEpoch).UtcDateTime;
                    string formattedTime = createdAt.ToString("dd-MM-yyyy_HH-mm-ss");
                    string fileName = $"{Path.GetFileNameWithoutExtension(ProfilePhoto.FileName)}_{formattedTime}{Path.GetExtension(ProfilePhoto.FileName)}";
                    var tempFilePath = Path.Combine(tempFolder, fileName);

                    try
                    {
                        await using (var stream = new FileStream(tempFilePath, FileMode.Create))
                        {
                            await ProfilePhoto.CopyToAsync(stream);
                        }

                        string s3Key = $"profiles/{fileName}";
                        var s3Url = await _s3Service.UploadFileToS3(tempFilePath, s3Key);
                        clinic.ProfilePhoto = s3Url;

                        _logger.LogInformation("Profile photo updated for ClinicId: {ClinicId}. File saved: {FileName}", dto.ClinicId, fileName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error uploading profile photo for ClinicId: {ClinicId}", dto.ClinicId);
                        return StatusCode(500, ApiResponseFactory.Fail("Failed to upload profile photo."));
                    }
                    finally
                    {
                        try
                        {
                            if (System.IO.File.Exists(tempFilePath))
                                System.IO.File.Delete(tempFilePath);
                        }
                        catch (Exception cleanupEx)
                        {
                            _logger.LogWarning(cleanupEx, "Failed to clean up temp file: {Path}", tempFilePath);
                        }
                    }
                }

                _clinicRepository.Update(clinic);
                await _clinicRepository.SaveChangesAsync();
                await transaction.CommitAsync();

                var response = new
                {
                    ClinicId = clinic.Id,
                    UpdatedAddress = clinic.Address,
                    UpdatedProfilePhoto = clinic.ProfilePhoto,
                    NotificationContext = new
                    {
                        Address = clinic.Address,
                        ProfilePhotoUrl = clinic.ProfilePhoto,
                        UpdatedAt = DateTime.UtcNow.ToString("dd-MM-yyyy HH:mm:ss")
                    },
                    NotificationMessage = $"Clinic profile updated" +
                          $"{(string.IsNullOrWhiteSpace(clinic.Address) ? "" : $" with new address: {clinic.Address}")}" +
                          $"{(string.IsNullOrWhiteSpace(clinic.ProfilePhoto) ? "." : $" and new profile photo uploaded.")}"
                };

                _logger.LogInformation("Profile update completed successfully for ClinicId: {ClinicId}", dto.ClinicId);
                return Ok(ApiResponseFactory.Success(response, "Profile updated successfully."));

            }
            finally
            {
                if (transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.RollbackAsync();
                }
            }
        }





        // Patient Details 
        [HttpGet("patient/details")]
        public async Task<IActionResult> GetPatientDetailsByHfId([FromQuery] string hfId)
        {
            if (string.IsNullOrWhiteSpace(hfId))
                return BadRequest(ApiResponseFactory.Fail("HFID is required."));

            var user = await _userRepository.GetUserByHFIDAsync(hfId);
            if (user == null)
            {
                _logger.LogInformation("No user found for HFID {HfId}", hfId);
                return Ok(ApiResponseFactory.Success(new PatientDetailsResponse(), "No patient found."));
            }

            // Fetch the amount due from the last receipt
            // var amountDue = await _clinicPatientRecordRepository.GetLastReceiptAmountDueByHfIdAsync(hfId);

            var amountDue = await _clinicPatientRecordRepository.GetTotalAmountDueByHfIdAsync(hfId);

            var response = new PatientDetailsResponse
            {
                PatientId = user.Id,
                FullName = $"{user.FirstName} {user.LastName}".Trim(),
                Gender = user.Gender,
                DOB = user.DOB,
                BloodGroup = user.BloodGroup,
                HfId = user.HfId,
                ProfileURL = user.ProfilePhoto,
                PhoneNumber = user.PhoneNumber,
                City = user.City,
                State = user.State,
                AmountDue = amountDue,  // NEW: Include amount due from last receipt
                Email = user.Email,
                CountryCallingCode = user.CountryCallingCode,
				UserReference = user.UserReference.ToString(),
			};

            _logger.LogInformation("Fetched patient details for HFID {HfId} with AmountDue: {AmountDue}",
                hfId, amountDue);

            return Ok(ApiResponseFactory.Success(response, "Patient Details fetched successfully."));
        }





        // Clinic Notifications
        [HttpGet("clinics/{clinicId}/notification")]
        [Authorize(Policy = "SuperAdminOrAdminPolicy")]
        public async Task<IActionResult> GetClinicNotification(
            [FromRoute][Range(1, int.MaxValue)] int clinicId,
            [FromQuery] int? timeframe,
            [FromQuery] string? startDate,
            [FromQuery] string? endDate,
            [FromServices] ClinicRepository clinicRepository)
        {
            HttpContext.Items["Log-Category"] = "Notification Management";
            _logger.LogInformation("Fetching notifications for Clinic ID: {ClinicId}, Timeframe: {Timeframe}, StartDate: {StartDate}, EndDate: {EndDate}",
                clinicId, timeframe, startDate, endDate);

            try
            {
                // Authorization check for Clinic
                if (!await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User).ConfigureAwait(false))
                {
                    _logger.LogWarning("Unauthorized access attempt to Clinic ID: {ClinicId}", clinicId);
                    return Unauthorized(ApiResponseFactory.Fail("Unauthorized access."));
                }

                long currentEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                long epochStart, epochEnd;

                // If custom date range provided
                if (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate))
                {
                    if (!DateTimeOffset.TryParseExact(startDate, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDateParsed) ||
                        !DateTimeOffset.TryParseExact(endDate, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDateParsed))
                    {
                        _logger.LogWarning("Invalid date format received: StartDate={StartDate}, EndDate={EndDate}", startDate, endDate);
                        return BadRequest(ApiResponseFactory.Fail("Invalid date format. Please use DD/MM/YYYY."));
                    }

                    epochStart = startDateParsed.ToUnixTimeSeconds();
                    epochEnd = endDateParsed.AddHours(23).AddMinutes(59).AddSeconds(59).ToUnixTimeSeconds();
                }
                else
                {
                    // Handle timeframe filter
                    epochStart = timeframe switch
                    {
                        1 => currentEpoch - 86400,    // last 24h
                        2 => currentEpoch - 604800,   // last 7d
                        3 => currentEpoch - 2592000,  // last 30d
                        _ => 0                        // all
                    };
                    epochEnd = currentEpoch;
                }

                // Fetch via repository
                var notifications = await clinicRepository.GetClinicNotificationsAsync(clinicId, epochStart, epochEnd);

                return Ok(ApiResponseFactory.Success(notifications, "Clinic notifications fetched successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve notifications for Clinic ID: {ClinicId}", clinicId);
                return StatusCode(500, ApiResponseFactory.Fail("Unexpected error occurred while fetching clinic notifications."));
            }
        }
    }
}
