using HFiles_Backend.API.DTOs.Clinics;
using HFiles_Backend.API.Interfaces;
using HFiles_Backend.Application.Common;
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
        private readonly ILogger<ClinicMemberRecordController> _logger;
        private readonly IClinicAuthorizationService _clinicAuthorizationService;

        public ClinicMemberRecordController(
            IClinicMemberRecordService service,
            ILogger<ClinicMemberRecordController> logger,
            IClinicAuthorizationService clinicAuthorizationService)
        {
            _service = service;
            _logger = logger;
            _clinicAuthorizationService = clinicAuthorizationService;
        }


        [HttpPost]
        public async Task<IActionResult> Upload([FromForm] UploadClinicMemberRecordRequestDto request)
        {
            HttpContext.Items["Log-Category"] = "Clinic Member Record";

            _logger.LogInformation(
                "Upload request received. ClinicId: {ClinicId}, UserId: {UserId}, ReportName: {ReportName}, ReportType: {ReportType}",
                request.ClinicId,
                request.UserId,
                request.ReportName,
                request.ReportType
            );

            if (request.File == null || request.File.Length == 0)
            {
                return BadRequest(ApiResponseFactory.Fail("File is required."));
            }

            // ✅ Authorization check still valid
            if (!await _clinicAuthorizationService.IsClinicAuthorized(request.ClinicId, User))
            {
                return Unauthorized(ApiResponseFactory.Fail(
                    "Permission denied. You can only manage your clinic or its branches."
                ));
            }

            try
            {
                var result = await _service.UploadAsync(
                    request.ClinicId,
                    request.UserId,
                    request.ReportName,
                    request.ReportType,
                    request.File
                );

                var responseDto = new UploadClinicMemberRecordResponseDto
                {
                    ReportName = result.ReportName,
                    ReportUrl = result.ReportUrl,
                    ReportType = result.ReportType,
                    FileSize = result.FileSize
                };

                return Ok(ApiResponseFactory.Success(responseDto, "File uploaded successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Upload failed");

                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
           

            return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    ApiResponseFactory.Fail("An error occurred while uploading the file.")
                );
            }
        }



        [HttpGet("{userId}")]
        public async Task<IActionResult> Get(int userId)
        {
            HttpContext.Items["Log-Category"] = "Clinic Member Record";

            var clinicIdClaim = User.FindFirst("ClinicId")?.Value;
            if (clinicIdClaim == null || !int.TryParse(clinicIdClaim, out int currentClinicId))
            {
                return Unauthorized(ApiResponseFactory.Fail("Invalid or missing ClinicId claim."));
            }

            _logger.LogInformation(
                "Get records request received. ClinicId: {ClinicId}, UserId: {UserId}",
                currentClinicId,
                userId
            );

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
                _logger.LogError(
                    ex,
                    "Error retrieving records. ClinicId: {ClinicId}, UserId: {UserId}",
                    currentClinicId,
                    userId
                );

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    ApiResponseFactory.Fail("An error occurred while retrieving records.")
                );
            }
        }
    }
}
