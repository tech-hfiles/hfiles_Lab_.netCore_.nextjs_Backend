using System.Runtime.InteropServices;
using HFiles_Backend.API.DTOs.Clinics;
using HFiles_Backend.API.Interfaces;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Labs;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Enums;
using HFiles_Backend.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HFiles_Backend.API.Controllers.Clinics
{
    [ApiController]
    [Route("api/clinic/member-records")]
    [Authorize]
    public class ClinicMemberRecordController : ControllerBase
    {
        private readonly IClinicMemberRecordService _service;
        private readonly S3StorageService _s3StorageService;
        private readonly ILogger<ClinicMemberRecordController> _logger;
        private readonly IClinicAuthorizationService _clinicAuthorizationService;
        private readonly IClinicRepository _clinicRepository;
        private readonly IUserRepository _userRepository;
        private readonly IClinicMemberRecordRepository _repository;

        public ClinicMemberRecordController(
            IClinicMemberRecordService service,
            S3StorageService s3StorageService,
            ILogger<ClinicMemberRecordController> logger,
            IClinicAuthorizationService clinicAuthorizationService,
            IClinicRepository clinicRepository,
            IUserRepository userRepository,
            IClinicMemberRecordRepository repository
           )
        {
            _service = service;
            _s3StorageService = s3StorageService;
            _logger = logger;
            _clinicAuthorizationService = clinicAuthorizationService;
            _clinicRepository = clinicRepository;
            _userRepository = userRepository;
            _repository = repository;
        }

        // ✅ MULTIPLE RECORDS UPLOAD
        [HttpPost("upload")]
        public async Task<IActionResult> UploadMemberRecords(
            [FromForm] UploadClinicMemberRecordsRequestDto request)
        {
            HttpContext.Items["Log-Category"] = "Clinic Member Records Upload";

            if (!ModelState.IsValid || request.Records == null || !request.Records.Any())
                return BadRequest(ApiResponseFactory.Fail("Invalid request. Files are required."));

            // Authorization check
            bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(request.ClinicId, User);
            if (!isAuthorized)
                return Unauthorized(ApiResponseFactory.Fail("You are not authorized to upload records for this clinic."));

            // Get clinic details
            var clinic = await _clinicRepository.GetByIdAsync(request.ClinicId);
            if (clinic == null)
                return NotFound(ApiResponseFactory.Fail($"Clinic with ID {request.ClinicId} not found."));

            // Get user details
            var user = await _userRepository.GetByIdAsync(request.UserId);
            if (user == null)
                return NotFound(ApiResponseFactory.Fail("User not found."));

            HttpContext.Items["Sent-To-UserId"] = user.Id;

            try
            {
                var uploadedRecordDetails = new List<string>();
                var recordResponseList = new List<UploadClinicMemberRecordResponseDto>();

                foreach (var recordItem in request.Records)
                {
                    var file = recordItem.File;

                    if (file == null || file.Length == 0)
                        return BadRequest(ApiResponseFactory.Fail("File is required."));

                    if (file.Length > 100 * 1024 * 1024)
                        return BadRequest(ApiResponseFactory.Fail("File size exceeds the 100MB limit."));

                    // 🔹 Create temp file
                    var extension = Path.GetExtension(file.FileName);
                    var fileName = $"{recordItem.ReportType.ToLower()}_clinic{request.ClinicId}_user{request.UserId}_{Guid.NewGuid()}{extension}";
                    var tempPath = Path.Combine(Path.GetTempPath(), fileName);

                    using (var stream = new FileStream(tempPath, FileMode.Create))
                        await file.CopyToAsync(stream);

                    // 🔹 Upload to S3
                    var s3Url = await _s3StorageService.UploadFileToS3(
                        tempPath,
                        $"clinic-member-records/{fileName}"
                    );

                    // 🔹 Delete temp file immediately after upload
                    System.IO.File.Delete(tempPath);

                    if (string.IsNullOrEmpty(s3Url))
                        return StatusCode(500, ApiResponseFactory.Fail("Failed to upload file to S3."));

                    // 🔹 Save DB record
                    var record = new ClinicMemberRecord
                    {
                        ClinicId = request.ClinicId,
                        UserId = request.UserId,
                        ReportName = recordItem.ReportName,
                        ReportType = recordItem.ReportType,
                        ReportUrl = s3Url,
                        FileSize = file.Length,
                        EpochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    };

                    await _repository.AddAsync(record);

                    uploadedRecordDetails.Add($"{recordItem.ReportName} ({recordItem.ReportType})");
                    recordResponseList.Add(new UploadClinicMemberRecordResponseDto
                    {
                        ReportName = recordItem.ReportName,
                        ReportType = recordItem.ReportType,
                        ReportUrl = s3Url,
                        FileSize = file.Length
                    });
                }

            
                var userName = $"{user.FirstName} {user.LastName}";
                var clinicName = clinic.ClinicName ?? "Clinic";

                // Response
                var response = new
                {
                    UserName = userName,
                    UserId = user.Id,
                    ClinicName = clinicName,
                    UploadedRecords = recordResponseList,
                    TotalRecords = recordResponseList.Count,
                    SentAt = DateTime.UtcNow,
                    NotificationMessage = $"{uploadedRecordDetails.Count} record(s) uploaded for {userName} by {clinicName}."
                };

                _logger.LogInformation(
                    "Uploaded {Count} records for Clinic ID {ClinicId}, User ID {UserId}",
                    uploadedRecordDetails.Count, request.ClinicId, request.UserId);

                return Ok(ApiResponseFactory.Success(response, "Records uploaded successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading records for Clinic ID {ClinicId}", request.ClinicId);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while uploading records."));
            }
        }

        // ✅ GET RECORDS
        [HttpGet("{userId}")]
        public async Task<IActionResult> Get(int userId)
        {
            HttpContext.Items["Log-Category"] = "Clinic Member Record";

            var clinicIdClaim = User.FindFirst("ClinicId")?.Value;
            if (clinicIdClaim == null || !int.TryParse(clinicIdClaim, out int currentClinicId))
            {
                return Unauthorized(ApiResponseFactory.Fail("Invalid or missing ClinicId claim."));
            }

            if (!await _clinicAuthorizationService.IsClinicAuthorized(currentClinicId, User))
            {
                return Unauthorized(ApiResponseFactory.Fail(
                    "Permission denied. You can only access records for your clinic or its branches."
                ));
            }

            try
            {
                var result = await _service.GetAsync(currentClinicId, userId);

                if (result == null || !result.Any())
                {
                    return NotFound(ApiResponseFactory.Fail(
                        "No records found for this user in the specified clinic."
                    ));
                }

                return Ok(ApiResponseFactory.Success(result, "Records retrieved successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving records");

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    ApiResponseFactory.Fail("An error occurred while retrieving records.")
                );
            }
        }
    }
}