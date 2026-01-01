using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using HFiles_Backend.API.DTOs.Clinics;
using HFiles_Backend.API.Interfaces;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Labs;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Enums;
using HFiles_Backend.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
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

            // Get clinic member details (instead of user)
            var clinicMember = await _repository.GetClinicMemberByIdAsync(request.ClinicMemberId);
            if (clinicMember == null)
                return NotFound(ApiResponseFactory.Fail("Clinic member not found."));

            // Verify clinic member belongs to the clinic
            if (clinicMember.ClinicId != request.ClinicId)
                return BadRequest(ApiResponseFactory.Fail("Clinic member does not belong to this clinic."));

            HttpContext.Items["Sent-To-ClinicMemberId"] = clinicMember.Id;

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
                    var fileName = $"specialreport_clinic{request.ClinicId}_member{request.ClinicMemberId}_{Guid.NewGuid()}{extension}"; // ✅ FIXED
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
                        ClinicMemberId = request.ClinicMemberId,
                        UserId = clinicMember.UserId,
                        ReportName = recordItem.ReportName,
                        ReportType = "Special Report", // ✅ Already correct
                        ReportUrl = s3Url,
                        FileSize = file.Length,
                        EpochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    };

                    await _repository.AddAsync(record);

                    uploadedRecordDetails.Add($"{recordItem.ReportName} (Special Report)"); // ✅ FIXED
                    recordResponseList.Add(new UploadClinicMemberRecordResponseDto
                    {
                        ReportName = recordItem.ReportName,
                        ReportType = "Special Report", // ✅ FIXED
                        ReportUrl = s3Url,
                        FileSize = file.Length
                    });
                }


                var clinicName = clinic.ClinicName ?? "Clinic";

                // Response
                var response = new
                {
                    ClinicMemberId = clinicMember.Id,
                    UserId = clinicMember.UserId,
                    ClinicName = clinicName,
                    UploadedRecords = recordResponseList,
                    TotalRecords = recordResponseList.Count,
                    SentAt = DateTime.UtcNow,
                    NotificationMessage = $"{uploadedRecordDetails.Count} document(s) uploaded for  by {clinicName}."
                };

                _logger.LogInformation(
                    "Uploaded {Count} documents for Clinic ID {ClinicId}, Clinic Member ID {ClinicMemberId}",
                    uploadedRecordDetails.Count, request.ClinicId, request.ClinicMemberId);

                return Ok(ApiResponseFactory.Success(response, "Documents uploaded successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading records for Clinic ID {ClinicId}", request.ClinicId);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while uploading records."));
            }
        }

        [HttpGet("{clinicMemberId}")]
        public async Task<IActionResult> Get(int clinicMemberId)
        {
            HttpContext.Items["Log-Category"] = "Clinic Member Record";
            try
            {
                // Verify clinic member exists
                var clinicMember = await _repository.GetClinicMemberByIdAsync(clinicMemberId);
                if (clinicMember == null)
                {
                    return NotFound(ApiResponseFactory.Fail("Clinic member not found."));
                }

                // Get records by clinic member ID (only specific columns)
                var result = await _repository.GetRecordsByClinicMemberAsync(clinicMemberId);

                if (result == null || !result.Any())
                {
                    return NotFound(ApiResponseFactory.Fail(
                        "No Documents found for this clinic member."
                    ));
                }

                // Map to response DTO with only required fields
                var response = result.Select(r => new
                {
                    Id = r.Id,
                    ClinicId = r.ClinicId,
                    UserId = r.UserId,
                    ReportName = r.ReportName,
                    ReportUrl = r.ReportUrl,
                    ReportType = r.ReportType,
                    FileSize = r.FileSize,
                    DeletedBy = r.DeletedBy,
                    EpochTime = r.EpochTime,
                    ClinicMemberId = r.ClinicMemberId
                });

                return Ok(ApiResponseFactory.Success(response, "Documents retrieved successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Document for clinic member {ClinicMemberId}", clinicMemberId);

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    ApiResponseFactory.Fail("An error occurred while retrieving Document.")
                );
            }
        }

        [HttpPut("{recordId}")]
        public async Task<IActionResult> UpdateRecordName(
     int recordId,
     [FromBody] UpdateClinicMemberRecordNameDto request)
        {
            HttpContext.Items["Log-Category"] = "Clinic Member Record Rename";

            try
            {
                // 🔍 Get existing record
                var record = await _repository.GetByIdAsync(recordId);
                if (record == null)
                    return NotFound(ApiResponseFactory.Fail("Document not found."));

                // ❌ ReportName is required for this API
                if (string.IsNullOrWhiteSpace(request.ReportName))
                    return BadRequest(ApiResponseFactory.Fail("Report name is required."));

                // ✅ ONLY rename
                record.ReportName = request.ReportName.Trim();

                await _repository.UpdateAsync(record);

                // ✅ Response (same structure as before)
                var response = new ClinicMemberRecordDto
                {
                    Id = record.Id,
                    ClinicId = record.ClinicId,
                    UserId = record.UserId,
                    ReportName = record.ReportName,
                    ReportUrl = record.ReportUrl,
                    ReportType = record.ReportType,
                    FileSize = record.FileSize,
                    EpochTime = record.EpochTime
                };

                _logger.LogInformation(
                    "Renamed Document {RecordId} in Clinic {ClinicId}",
                    recordId, record.ClinicId);

                return Ok(ApiResponseFactory.Success(
                    response,
                    "Document name updated successfully."
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error renaming Document {RecordId}", recordId);
                return StatusCode(
                    500,
                    ApiResponseFactory.Fail("An error occurred while updating the document.")
                );
            }
        }




        [HttpPut("{recordId}/soft-delete")]
        [Authorize]
        public async Task<IActionResult> SoftDeleteRecord(int recordId)
        {
            HttpContext.Items["Log-Category"] = "Clinic Member Record Soft Delete";

            try
            {
                var record = await _repository.GetByIdAsync(recordId);
                if (record == null)
                    return NotFound(ApiResponseFactory.Fail("Document not found."));

                // ✅ FIX: READ UserId EXACTLY AS JWT SENDS IT
                var userIdClaim = User.FindFirst("UserId")?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                    return Unauthorized(ApiResponseFactory.Fail("UserId not found in token."));

                record.DeletedBy = int.Parse(userIdClaim);

                await _repository.UpdateAsync(record);

                return Ok(ApiResponseFactory.Success<object>(
                    null,
                    "Document deleted successfully."
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Document {RecordId}", recordId);
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    ApiResponseFactory.Fail("An error occurred while deleting the record.")
                );
            }
        }
    }
}