using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using HFiles_Backend.API.DTOs.Labs;
using HFiles_Backend.API.Services;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Labs;
using HFiles_Backend.Domain.Entities.Labs;
using HFiles_Backend.Domain.Entities.Users;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sprache;


namespace HFiles_Backend.API.Controllers.Labs
{
    [Route("api/")]
    [ApiController]
    [Authorize]
    public class LabUserReportController(
    AppDbContext context,
    IWebHostEnvironment env,
    LabAuthorizationService labAuthorizationService,
    EmailService emailService,
    ILogger<LabUserReportController> logger,
    S3StorageService s3Service) : ControllerBase
    {
        private readonly AppDbContext _context = context;
        private readonly IWebHostEnvironment _env = env;
        private readonly LabAuthorizationService _labAuthorizationService = labAuthorizationService;
        private readonly EmailService _emailService = emailService;
        private readonly ILogger<LabUserReportController> _logger = logger;
        private readonly S3StorageService _s3Service = s3Service;


        // Function to get username from Token
        private async Task<string?> ResolveUsernameFromClaims(HttpContext httpContext, AppDbContext dbContext)
        {
            var role = httpContext.User.FindFirst(ClaimTypes.Role)?.Value;
            var adminIdStr = httpContext.User.FindFirst("LabAdminId")?.Value;

            if (!int.TryParse(adminIdStr, out var adminId)) return null;

            if (role == "Super Admin")
            {
                var superAdmin = await dbContext.LabSuperAdmins.FirstOrDefaultAsync(sa => sa.Id == adminId);
                if (superAdmin != null)
                {
                    var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == superAdmin.UserId && u.DeletedBy == 0);
                    return $"Super Admin {user?.FirstName} {user?.LastName}".Trim();
                }
            }

            if (role == "Admin" || role == "Member")
            {
                var member = await dbContext.LabMembers.FirstOrDefaultAsync(m => m.Id == adminId);
                if (member != null)
                {
                    var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == member.UserId && u.DeletedBy == 0);
                    return $"{member.Role} {user?.FirstName} {user?.LastName}".Trim();
                }
            }

            return null;
        }


        // Method to Map Report Type
        private static int GetReportTypeValue(string? reportType)
        {
            return reportType?.ToLower() switch
            {
                "lab report" => 3,
                "dental report" => 4,
                "immunization" => 5,
                "medications/prescription" => 6,
                "radiology" => 7,
                "opthalmology" => 8,
                "special report" => 9,
                "invoices/mediclaim insurance" => 10,
                _ => 0
            };
        }


        // Method to Reverse Map the Report Type
        private static string ReverseReportTypeMapping(int reportTypeId)
        {
            return reportTypeId switch
            {
                3 => "Lab Report",
                4 => "Dental Report",
                5 => "Immunization",
                6 => "Medications/Prescription",
                7 => "Radiology",
                8 => "Ophthalmology",
                9 => "Special Report",
                10 => "Invoices/Mediclaim Insurance",
                _ => "Unknown Report Type"
            };
        }





        // Upload single/batch lab reports of muliple users
        [HttpPost("labs/reports/upload")]
        [RequestSizeLimit(500_000_000)]
        public async Task<IActionResult> UploadReports([FromForm] UserReportBatchUpload dto)
        {
            HttpContext.Items["Log-Category"] = "Lab Management";

            _logger.LogInformation("Received request to upload lab reports.");

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                _logger.LogWarning("Invalid model state during report upload: {Errors}", string.Join(", ", errors));
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            using var tx = await _context.Database.BeginTransactionAsync();

            try
            {
                var labIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
                if (labIdClaim == null || !int.TryParse(labIdClaim.Value, out int labId))
                {
                    _logger.LogWarning("Missing or invalid LabId claim during report upload.");
                    return Unauthorized(ApiResponseFactory.Fail("Invalid or missing LabId claim."));
                }

                if (!await _labAuthorizationService.IsLabAuthorized(labId, User))
                {
                    _logger.LogWarning("Unauthorized attempt to upload report by LabId: {LabId}", labId);
                    return Unauthorized(ApiResponseFactory.Fail("Permission denied. You can only create/modify/delete data for your main lab or its branches."));
                }

                if (dto.Entries == null || dto.Entries.Count == 0)
                {
                    _logger.LogWarning("No entries provided in the report upload payload.");
                    return BadRequest(ApiResponseFactory.Fail("No entries provided in the payload."));
                }

                string tempFolder = Path.Combine(_env.WebRootPath ?? "wwwroot", "temp-reports");
                if (!Directory.Exists(tempFolder)) Directory.CreateDirectory(tempFolder);

                var entryResults = new List<object>();
                int successfulUploads = 0;

                foreach (var entry in dto.Entries)
                {
                    _logger.LogInformation("Processing entry for HFID: {HFID}, Email: {Email}", entry.HFID, entry.Email);

                    if (entry.ReportFiles == null || entry.ReportTypes == null)
                    {
                        entryResults.Add(new { entry.HFID, entry.Email, Status = "Failed", Reason = "ReportFiles or ReportTypes missing", BranchLabId = entry.BranchId });
                        _logger.LogWarning("ReportFiles or ReportTypes missing for HFID: {HFID}", entry.HFID);
                        continue;
                    }

                    if (entry.ReportFiles.Count != entry.ReportTypes.Count)
                    {
                        entryResults.Add(new
                        {
                            entry.HFID,
                            entry.Email,
                            Status = "Failed",
                            Reason = $"Mismatch between files ({entry.ReportFiles.Count}) and report types ({entry.ReportTypes.Count})",
                            BranchLabId = entry.BranchId
                        });
                        _logger.LogWarning("Mismatch between files and types for HFID: {HFID}", entry.HFID);
                        continue;
                    }

                    if (entry.ReportFiles.Count == 0)
                    {
                        entryResults.Add(new { entry.HFID, entry.Email, Status = "Failed", Reason = "No report files provided", BranchLabId = entry.BranchId });
                        _logger.LogWarning("No files provided for HFID: {HFID}", entry.HFID);
                        continue;
                    }

                    var userDetails = await _context.Set<Domain.Entities.Users.User>()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => u.HfId == entry.HFID && u.Email == entry.Email);

                    if (userDetails == null)
                    {
                        entryResults.Add(new { entry.HFID, entry.Email, Status = "Failed", Reason = "HFID and Email do not match any user", BranchLabId = entry.BranchId });
                        _logger.LogWarning("No matching user for HFID: {HFID} and Email: {Email}", entry.HFID, entry.Email);
                        continue;
                    }

                    int userId = userDetails.Id;
                    string uploadType = userDetails.UserReference == 0 ? "Independent" : "Dependent";
                    long epoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    var uploadedFiles = new List<object>();

                    for (int i = 0; i < entry.ReportFiles.Count; i++)
                    {
                        var file = entry.ReportFiles[i];
                        var reportType = entry.ReportTypes[i];

                        if (file == null || file.Length == 0)
                            continue;

                        var fileName = $"{Path.GetFileNameWithoutExtension(file.FileName)}{Path.GetExtension(file.FileName)}";
                        var tempFilePath = Path.Combine(tempFolder, fileName);

                        using (var stream = new FileStream(tempFilePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        var s3Key = $"reports/{fileName}";
                        var s3Url = await _s3Service.UploadFileToS3(tempFilePath, s3Key);

                        if (s3Url == null)
                            return StatusCode(500, ApiResponseFactory.Fail("S3 URL not generated"));

                        if (System.IO.File.Exists(tempFilePath)) System.IO.File.Delete(tempFilePath);

                        _logger.LogInformation("Saved file {FileName} for UserId: {UserId}", fileName, userId);

                        var epochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                        var userReport = new UserReport
                        {
                            UserId = userId,
                            ReportName = Path.GetFileNameWithoutExtension(file.FileName),
                            ReportUrl = s3Url,
                            ReportCategory = GetReportTypeValue(reportType),
                            EpochTime = epochTime,
                            FileSize = (decimal)Math.Round(file.Length / 1024.0, 2),
                            UserType = uploadType,
                            UploadedBy = "Lab",
                            LabId = labId,
                            DeletedBy = 0
                        };

                        _context.UserReports.Add(userReport);
                        await _context.SaveChangesAsync();

                        var labUserReport = new LabUserReports
                        {
                            UserId = userId,
                            LabId = labId,
                            BranchId = entry.BranchId,
                            Name = entry.Name,
                            EpochTime = epoch
                        };

                        _context.LabUserReports.Add(labUserReport);
                        await _context.SaveChangesAsync();

                        userReport.LabUserReportId = labUserReport.Id;
                        _context.UserReports.Update(userReport);
                        await _context.SaveChangesAsync();

                        uploadedFiles.Add(new { FileName = fileName, ReportType = reportType });
                    }

                    if (uploadedFiles.Any())
                    {
                        successfulUploads++;

                        string? createdByName = await ResolveUsernameFromClaims(HttpContext, _context);
                        if (string.IsNullOrWhiteSpace(createdByName))
                            return Unauthorized(ApiResponseFactory.Fail("Unable to resolve creator identity."));

                        var reportNames = uploadedFiles
                            .Select(f => f.GetType().GetProperty("FileName")?.GetValue(f)?.ToString())
                            .Where(name => !string.IsNullOrWhiteSpace(name))
                            .ToList();

                        string reportNameList = string.Join(", ", reportNames);
                        string reportCountText = uploadedFiles.Count == 1
                            ? $"1 report ({reportNameList})"
                            : $"{uploadedFiles.Count} reports ({reportNameList})";

                        string notificationMessage = $"{reportCountText} uploaded for patient {userDetails.FirstName} {userDetails.LastName} (HFID: {entry.HFID}) by {createdByName}";


                        var notificationContext = new
                        {
                            UploadedFor = new
                            {
                                HFID = entry.HFID,
                                Email = entry.Email,
                                UserName = $"{userDetails.FirstName} {userDetails.LastName}".Trim()
                            },
                            UploadedBy = createdByName,
                            LabId = labId,
                            BranchId = entry.BranchId
                        };

                        entryResults.Add(new
                        {
                            entry.HFID,
                            entry.Email,
                            BranchLabId = entry.BranchId,
                            Status = "Success",
                            UploadedFiles = uploadedFiles,
                            Message = "Reports uploaded successfully.",
                            NotificationContext = notificationContext,
                            NotificationMessage = notificationMessage
                        });

                        _logger.LogInformation("Uploaded {Count} files for HFID: {HFID}", uploadedFiles.Count, entry.HFID);
                    }
                    else
                    {
                        entryResults.Add(new { entry.HFID, entry.Email, Status = "Failed", Reason = "Valid user, but no report files were uploaded", BranchLabId = entry.BranchId });
                        _logger.LogWarning("Valid user {HFID}, but no files uploaded", entry.HFID);
                    }
                }

                await tx.CommitAsync();

                if (successfulUploads == 0)
                {
                    _logger.LogWarning("No reports uploaded successfully. All entries failed.");
                    return BadRequest(ApiResponseFactory.Fail(entryResults, "No reports were uploaded. All entries failed."));
                }

                if (successfulUploads < dto.Entries.Count)
                {
                    _logger.LogInformation("Partial success: {SuccessCount} of {Total} entries uploaded successfully.", successfulUploads, dto.Entries.Count);
                    return StatusCode(202, ApiResponseFactory.PartialSuccess(entryResults, "Some reports uploaded successfully. Others failed."));
                }

                _logger.LogInformation("All reports uploaded successfully for LabId: {LabId}", labId);
                return Ok(ApiResponseFactory.Success(entryResults, "All reports uploaded successfully."));
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Unexpected error occurred during report upload.");
                return StatusCode(500, ApiResponseFactory.Fail($"An unexpected error occurred: {ex.Message}"));
            }
        }







        // Fetch All Reports of Selected User
        [HttpGet("labs/reports/{userId}")]
        public async Task<IActionResult> GetLabUserReportsByUserId([FromRoute] int userId, [FromQuery] string? reportType)
        {
            HttpContext.Items["Log-Category"] = "Lab Management";
            _logger.LogInformation("Fetching lab reports for UserId: {UserId}", userId);

            var labIdStr = User.FindFirst("UserId")?.Value;
            if (!int.TryParse(labIdStr, out int labId))
            {
                _logger.LogWarning("Invalid LabId claim.");
                return Unauthorized(ApiResponseFactory.Fail("Invalid or missing LabId claim."));
            }

            try
            {
                if (!await _labAuthorizationService.IsLabAuthorized(labId, User).ConfigureAwait(false))
                {
                    _logger.LogWarning("Unauthorized access for LabId {LabId} to fetch reports for UserId {UserId}", labId, userId);
                    return Unauthorized(ApiResponseFactory.Fail("Unauthorized lab access."));
                }

                var currentLab = await _context.LabSignups
                    .AsNoTracking()
                    .FirstOrDefaultAsync(l => l.Id == labId)
                    .ConfigureAwait(false);
                if (currentLab == null)
                {
                    _logger.LogWarning("Lab not found for LabId {LabId}", labId);
                    return NotFound(ApiResponseFactory.Fail($"LabId {labId} not found."));
                }

                var userDetails = await _context.Set<Domain.Entities.Users.User>()
                    .AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => new
                    {
                        u.Id,
                        u.HfId,
                        u.FirstName,
                        u.LastName,
                        u.Email,
                        u.ProfilePhoto
                    })
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);

                if (userDetails == null)
                {
                    _logger.LogWarning("User not found for UserId {UserId}", userId);
                    return NotFound(ApiResponseFactory.Fail($"UserId {userId} not found."));
                }

                string fullName = $"{userDetails.FirstName} {userDetails.LastName}".Trim();

                var relatedLabIds = currentLab.LabReference == 0
                    ? await _context.LabSignups.Where(l => l.LabReference == labId).Select(l => l.Id).ToListAsync().ConfigureAwait(false)
                    : await _context.LabSignups.Where(l => l.LabReference == currentLab.LabReference).Select(l => l.Id).ToListAsync().ConfigureAwait(false);

                relatedLabIds.Add(currentLab.LabReference == 0 ? labId : currentLab.LabReference);

                _logger.LogInformation("Related Lab IDs: {Labs}", string.Join(",", relatedLabIds));

                await using var tx = await _context.Database.BeginTransactionAsync().ConfigureAwait(false);

                var userReports = await _context.UserReports
                    .AsNoTracking()
                    .Where(ur => ur.UserId == userId
                            && ur.LabId.HasValue
                            && relatedLabIds.Contains(ur.LabId.Value)
                            && ur.UploadedBy == "Lab")
                    .ToListAsync()
                    .ConfigureAwait(false);

                var labUserReports = await _context.LabUserReports
                    .AsNoTracking()
                    .Where(lr => lr.UserId == userId && relatedLabIds.Contains(lr.LabId))
                    .ToDictionaryAsync(lr => lr.Id, lr => lr)
                    .ConfigureAwait(false);

                if (!userReports.Any() && labUserReports.Count == 0)
                    return NotFound(ApiResponseFactory.Fail($"No reports found for UserId {userId} in related labs."));

                var branchIds = labUserReports.Values.Select(l => l.BranchId).Distinct().ToList();
                var branchNamesDict = await _context.LabSignups
                    .AsNoTracking()
                    .Where(l => branchIds.Contains(l.Id))
                    .ToDictionaryAsync(l => l.Id, l => l.LabName)
                    .ConfigureAwait(false);

                var labUserReportIds = userReports
                    .Where(u => u.LabUserReportId.HasValue)
                    .Select(u => u.LabUserReportId!.Value)
                    .Distinct()
                    .ToList();

                var latestResends = await _context.LabResendReports
                    .AsNoTracking()
                    .Where(r => labUserReportIds.Contains(r.LabUserReportId))
                    .GroupBy(r => r.LabUserReportId)
                    .Select(g => new
                    {
                        g.Key,
                        LatestResendEpochTime = g.Max(x => x.ResendEpochTime)
                    })
                    .ToDictionaryAsync(g => g.Key, g => g.LatestResendEpochTime)
                    .ConfigureAwait(false);

                var firstSentEpoch = labUserReports.Values.Min(l => l.EpochTime > 0 ? l.EpochTime : (long?)null);
                var lastSentEpoch = labUserReports.Values.Max(l => l.EpochTime > 0 ? l.EpochTime : (long?)null);

                string firstSentDate = firstSentEpoch.HasValue ? DateTimeOffset.FromUnixTimeSeconds(firstSentEpoch.Value).UtcDateTime.ToString("dd-MM-yyyy") : "No Reports";
                string lastSentDate = lastSentEpoch.HasValue ? DateTimeOffset.FromUnixTimeSeconds(lastSentEpoch.Value).UtcDateTime.ToString("dd-MM-yyyy") : "No Reports";

                var responseData = userReports
                    .Select(ur =>
                    {
                        int lurId = ur.LabUserReportId ?? 0;
                        labUserReports.TryGetValue(lurId, out var lur);
                        var epoch = lur?.EpochTime ?? 0;
                        var branch = lur?.BranchId ?? 0;
                        string created = epoch > 0 ? DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime.ToString("dd-MM-yyyy") : "";
                        string branchName = branchNamesDict.TryGetValue(branch, out string? name) ? name ?? "Unknown Branch" : currentLab.LabName ?? "Unknown Lab";
                        string resendDate = latestResends.TryGetValue(lurId, out long r) && r > 0
                            ? DateTimeOffset.FromUnixTimeSeconds(r).UtcDateTime.ToString("dd-MM-yyyy")
                            : "Not Resend";

                        return new
                        {
                            ur.Id,
                            filename = ur.ReportName,
                            fileURL = ur.ReportUrl,
                            labName = currentLab.LabName,
                            reportType = ReverseReportTypeMapping(ur.ReportCategory),
                            branchName,
                            epochTime = epoch,
                            createdDate = created,
                            LabUserReportId = lurId,
                            resendDate
                        };
                    })
                    .Where(r => string.IsNullOrEmpty(reportType) || r.reportType == reportType)
                    .ToList();

                await tx.CommitAsync().ConfigureAwait(false);

                int reportCount = responseData.Count;
                _logger.LogInformation("Fetched {Count} reports for UserId: {UserId}", reportCount, userId);

                return Ok(ApiResponseFactory.Success(new
                {
                    ReportCounts = reportCount,
                    UserDetails = new
                    {
                        UserId = userId,
                        HFID = userDetails.HfId,
                        FullName = fullName,
                        Email = userDetails.Email,
                        UserImage = string.IsNullOrEmpty(userDetails.ProfilePhoto) ? "No Image Available" : userDetails.ProfilePhoto,
                        FirstSentReportDate = firstSentDate,
                        LastSentReportDate = lastSentDate
                    },
                    Reports = responseData
                }, "Reports fetched successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching reports for UserId {UserId}", userId);
                return StatusCode(500, ApiResponseFactory.Fail("An unexpected error occurred. Please contact support."));
            }
        }




        // Fetch All Distinct Users for All Dates
        [HttpGet("labs/reports/all")]
        public async Task<IActionResult> GetLabUserReports()
        {
            HttpContext.Items["Log-Category"] = "Lab Management";
            _logger.LogInformation("Request to get lab user reports.");

            try
            {
                var labIdStr = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
                if (!int.TryParse(labIdStr, out int labId))
                {
                    _logger.LogWarning("Missing or invalid LabId claim.");
                    return Unauthorized(ApiResponseFactory.Fail("Invalid or missing LabId claim."));
                }

                if (!await _labAuthorizationService.IsLabAuthorized(labId, User).ConfigureAwait(false))
                {
                    _logger.LogWarning("Unauthorized access for LabId {LabId}.", labId);
                    return Unauthorized(ApiResponseFactory.Fail("Unauthorized access to lab data."));
                }

                await using var tx = await _context.Database.BeginTransactionAsync().ConfigureAwait(false);

                var latestReports = await _context.LabUserReports
                    .AsNoTracking()
                    .Where(lur => lur.LabId == labId)
                    .GroupBy(lur => lur.UserId)
                    .Select(group => group.OrderByDescending(lur => lur.EpochTime).First())
                    .ToListAsync()
                    .ConfigureAwait(false);

                if (!latestReports.Any())
                {
                    _logger.LogInformation("No lab user reports found for LabId {LabId}.", labId);
                    return NotFound(ApiResponseFactory.Fail($"No reports found for LabId {labId}."));
                }

                var userIds = latestReports.Select(r => r.UserId).Distinct().ToList();

                var userDetailsDict = await _context.Users
                    .AsNoTracking()
                    .Where(u => userIds.Contains(u.Id))
                    .ToDictionaryAsync(
                        u => u.Id,
                        u => new
                        {
                            HFID = u.HfId,
                            Name = $"{u.FirstName} {u.LastName}".Trim(),
                            UserId = u.Id
                        })
                    .ConfigureAwait(false);

                var reportIdsDict = await _context.UserReports
                    .AsNoTracking()
                    .Where(ur => userIds.Contains(ur.UserId) && ur.LabId == labId)
                    .GroupBy(ur => ur.UserId)
                    .Select(g => new
                    {
                        UserId = g.Key,
                        g.OrderByDescending(ur => ur.EpochTime).First().ReportCategory
                    })
                    .ToDictionaryAsync(x => x.UserId, x => x.ReportCategory)
                    .ConfigureAwait(false);

                var responseData = latestReports
                    .Select(report =>
                    {
                        if (!userDetailsDict.TryGetValue(report.UserId, out var userDetail))
                            return null;

                        reportIdsDict.TryGetValue(report.UserId, out int reportId);

                        return new
                        {
                            userDetail.HFID,
                            userDetail.Name,
                            userDetail.UserId,
                            ReportType = ReverseReportTypeMapping(reportId),
                            Date = DateTimeOffset.FromUnixTimeSeconds(report.EpochTime).UtcDateTime.ToString("dd-MM-yyyy")
                        };
                    })
                    .Where(x => x != null)
                    .ToList();

                await tx.CommitAsync().ConfigureAwait(false);

                _logger.LogInformation("Fetched {Count} lab user reports for LabId {LabId}.", responseData.Count, labId);
                return Ok(ApiResponseFactory.Success(responseData, "Reports fetched successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching lab user reports.");
                return StatusCode(500, ApiResponseFactory.Fail($"Unexpected error: {ex.Message}"));
            }
        }







        // Fetch All Distinct Users Based on Selection of Date
        [HttpGet("labs/{labId}/reports")]
        public async Task<IActionResult> GetLabUserReports([FromRoute] int labId, [FromQuery] string? startDate, [FromQuery] string? endDate, CancellationToken cancellationToken = default)
        {
            HttpContext.Items["Log-Category"] = "Lab Management";
            _logger.LogInformation("Received request to get reports for LabId: {LabId}, StartDate: {StartDate}, EndDate: {EndDate}", labId, startDate, endDate);

            try
            {
                if (labId <= 0)
                {
                    _logger.LogWarning("Invalid LabId provided: {LabId}", labId);
                    return BadRequest(ApiResponseFactory.Fail("Invalid LabId."));
                }

                var labIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
                if (labIdClaim == null || !int.TryParse(labIdClaim.Value, out int loggedInLabId))
                {
                    _logger.LogWarning("Missing or invalid LabId claim.");
                    return Unauthorized(ApiResponseFactory.Fail("Invalid or missing LabId claim."));
                }

                if (!await _labAuthorizationService.IsLabAuthorized(labId, User))
                {
                    _logger.LogWarning("Unauthorized access attempt by LabId: {LoggedInLabId} for LabId: {TargetLabId}", loggedInLabId, labId);
                    return Unauthorized(ApiResponseFactory.Fail("Permission denied. You can only access your main lab or its branches."));
                }

                long startEpoch, endEpoch;
                if (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate))
                {
                    if (!DateTime.TryParseExact(startDate, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var selectedStartDate) ||
                        !DateTime.TryParseExact(endDate, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var selectedEndDate))
                    {
                        _logger.LogWarning("Invalid date format. StartDate: {StartDate}, EndDate: {EndDate}", startDate, endDate);
                        return BadRequest(ApiResponseFactory.Fail("Invalid date format. Use dd/MM/yyyy for both start and end dates."));
                    }

                    startEpoch = new DateTimeOffset(selectedStartDate.Date).ToUnixTimeSeconds();
                    endEpoch = new DateTimeOffset(selectedEndDate.Date.AddDays(1).AddTicks(-1)).ToUnixTimeSeconds();
                }
                else
                {
                    var today = DateTime.UtcNow.Date;
                    var yesterday = today.AddDays(-1);
                    startEpoch = new DateTimeOffset(yesterday).ToUnixTimeSeconds();
                    endEpoch = new DateTimeOffset(today.AddDays(1).AddTicks(-1)).ToUnixTimeSeconds();

                    _logger.LogInformation("Default date range applied: {StartEpoch} to {EndEpoch}", startEpoch, endEpoch);
                }

                var reportsQuery = _context.LabUserReports
                    .AsNoTracking()
                    .Where(lur => (lur.LabId == labId && lur.BranchId == 0) || lur.BranchId == labId);

                var allReports = await reportsQuery
                    .GroupBy(lur => lur.UserId)
                    .Select(g => g.OrderByDescending(r => r.EpochTime).First())
                    .ToListAsync(cancellationToken);

                var filteredReports = allReports
                    .Where(lur => lur.EpochTime >= startEpoch && lur.EpochTime <= endEpoch)
                    .ToList();

                var PatientReports = allReports.Count;

                if (filteredReports.Count == 0)
                {
                    _logger.LogInformation("No reports found in given date range for LabId: {LabId}", labId);
                    return NotFound(ApiResponseFactory.Fail($"No reports found in the past 48 hours."));
                }

                var userIds = filteredReports.Select(lr => lr.UserId).Distinct().ToList();

                var userDetailsDict = await _context.Set<Domain.Entities.Users.User>()
                    .AsNoTracking()
                    .Where(u => userIds.Contains(u.Id))
                    .ToDictionaryAsync(
                        u => u.Id,
                        u => new
                        {
                            HFID = u.HfId,
                            Name = $"{u.FirstName} {u.LastName}".Trim(),
                            UserId = u.Id
                        },
                        cancellationToken
                    );

                var reportIdsDict = await _context.UserReports
                    .AsNoTracking()
                    .Where(ur => userIds.Contains(ur.UserId) && ur.LabId == labId)
                    .GroupBy(ur => ur.UserId)
                    .Select(g => new
                    {
                        UserId = g.Key,
                        ReportCategory = g.OrderByDescending(ur => ur.EpochTime).First().ReportCategory
                    })
                    .ToDictionaryAsync(x => x.UserId, x => x.ReportCategory, cancellationToken);

                var responseData = filteredReports
                    .Select(report =>
                    {
                        if (!userDetailsDict.TryGetValue(report.UserId, out var userDetail))
                            return null;

                        reportIdsDict.TryGetValue(report.UserId, out int reportCategory);

                        return new
                        {
                            report.Id,
                            userDetail.HFID,
                            userDetail.Name,
                            userDetail.UserId,
                            ReportType = ReverseReportTypeMapping(reportCategory),
                            Date = DateTimeOffset.FromUnixTimeSeconds(report.EpochTime).UtcDateTime.ToString("dd-MM-yyyy")
                        };
                    })
                    .Where(x => x != null)
                    .ToList();

                _logger.LogInformation("Returning {Count} filtered reports for LabId: {LabId}", responseData.Count, labId);

                var response = new { PatientReports, responseData };
                return Ok(ApiResponseFactory.Success(response, "Reports fetched successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while fetching reports for LabId: {LabId}", labId);
                return StatusCode(500, ApiResponseFactory.Fail($"An unexpected error occurred: {ex.Message}"));
            }
        }






        // Resend Reports using LabUserReportID
        [HttpPost("labs/reports/resend")]
        public async Task<IActionResult> ResendReport([FromBody] ResendReport dto)
        {
            HttpContext.Items["Log-Category"] = "Lab Management";
            _logger.LogInformation("ResendReport request received with {Count} IDs", dto?.Ids?.Count ?? 0);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Model validation failed: {Errors}", string.Join(", ", errors));
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            if (dto?.Ids == null || dto.Ids.Count == 0)
            {
                _logger.LogWarning("No LabUserReport IDs provided for resending.");
                return BadRequest(ApiResponseFactory.Fail("No LabUserReport IDs provided for resending."));
            }

            try
            {
                var labIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
                if (labIdClaim == null || !int.TryParse(labIdClaim.Value, out int labId))
                {
                    _logger.LogWarning("Invalid or missing LabId claim.");
                    return Unauthorized(ApiResponseFactory.Fail("Invalid or missing LabId claim."));
                }

                if (!await _labAuthorizationService.IsLabAuthorized(labId, User))
                {
                    _logger.LogWarning("Unauthorized attempt to resend reports by LabId: {LabId}", labId);
                    return Unauthorized(ApiResponseFactory.Fail("Permission denied."));
                }

                var loggedInLab = await _context.LabSignups.AsNoTracking().FirstOrDefaultAsync(l => l.Id == labId);
                if (loggedInLab == null)
                {
                    _logger.LogWarning("Lab not found for LabId: {LabId}", labId);
                    return BadRequest(ApiResponseFactory.Fail("Lab not found."));
                }

                int mainLabId = loggedInLab.LabReference == 0 ? labId : loggedInLab.LabReference;

                var branchIds = await _context.LabSignups
                    .Where(l => l.LabReference == mainLabId)
                    .Select(l => l.Id)
                    .ToListAsync();

                branchIds.Add(mainLabId);

                _logger.LogInformation("Resending reports from MainLabId: {MainLabId}, BranchIds: {BranchIds}", mainLabId, string.Join(",", branchIds));

                var validLabUserReports = await _context.LabUserReports
                    .Where(lur => dto.Ids.Contains(lur.Id))
                    .ToDictionaryAsync(lur => lur.Id);

                var userReports = await _context.UserReports
                    .Where(ur => validLabUserReports.Keys.Contains(ur.LabUserReportId ?? 0) && ur.UploadedBy == "Lab")
                    .ToDictionaryAsync(ur => ur.LabUserReportId!.Value);

                var successReports = new List<object>();
                var failedReports = new List<object>();
                var resendEntries = new List<LabResendReports>();
                var reportLogs = new List<NotificationResponse>();

                long currentEpochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                using var transaction = await _context.Database.BeginTransactionAsync();

                foreach (var id in dto.Ids)
                {
                    if (!validLabUserReports.TryGetValue(id, out var labUserReport) || !branchIds.Contains(labUserReport.LabId))
                    {
                        failedReports.Add(new { Id = id, Status = "Failed", Reason = "Invalid report or unauthorized branch." });

                        reportLogs.Add(new NotificationResponse
                        {
                            LabUserReportId = id,
                            Success = false,
                            Status = "Failed",
                            Reason = "Invalid report or unauthorized branch.",
                            BranchLabId = labUserReport?.LabId
                        });

                        continue;
                    }

                    if (!userReports.TryGetValue(id, out var userReport))
                    {
                        failedReports.Add(new { Id = id, Status = "Failed", Reason = "User report not found." });

                        reportLogs.Add(new NotificationResponse
                        {
                            LabUserReportId = id,
                            Success = false,
                            Status = "Failed",
                            Reason = "User report not found.",
                            BranchLabId = labUserReport.LabId
                        });

                        continue;
                    }

                    labUserReport.Resend += 1;
                    _context.LabUserReports.Update(labUserReport);

                    resendEntries.Add(new LabResendReports
                    {
                        LabUserReportId = id,
                        ResendEpochTime = currentEpochTime
                    });

                    int? branchLabId = labUserReport.LabId != labId ? labUserReport.LabId : null;

                    successReports.Add(new
                    {
                        Id = id,
                        Status = "Success",
                        NewResendCount = labUserReport.Resend,
                        labUserReport.EpochTime
                    });

                    string? createdByName = await ResolveUsernameFromClaims(HttpContext, _context);
                    if (string.IsNullOrWhiteSpace(createdByName))
                        return Unauthorized(ApiResponseFactory.Fail("Unable to resolve creator identity."));

                    var notificationMessage = $"Report {userReport.ReportName} successfully resent by {createdByName}.";

                    reportLogs.Add(new NotificationResponse
                    {
                        LabUserReportId = id,
                        ResendReportName = userReport.ReportName,
                        ResendReportType = ReverseReportTypeMapping(userReport.ReportCategory),
                        NewResendCount = labUserReport.Resend,
                        Success = true,
                        Status = "Success",
                        BranchLabId = branchLabId,
                        NotificationMessage = notificationMessage
                    });
                }

                var notificationSummaries = reportLogs
                .Select(x => new
                {
                    x.NotificationMessage,
                }).ToList();

                var labBranchId = reportLogs
                .Select(x => new
                {
                    x.BranchLabId,
                }).ToList();

                if (resendEntries.Any())
                {
                    await _context.LabResendReports.AddRangeAsync(resendEntries);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Resend operation complete. Success: {SuccessCount}, Failed: {FailedCount}", successReports.Count, failedReports.Count);
                HttpContext.Items["PerReportLogs"] = reportLogs;

                var result = new
                {
                    Success = successReports,
                    Failed = failedReports,
                    notificationMessage = notificationSummaries,
                    labBranchId = labBranchId
                };


                return failedReports.Count switch
                {
                    0 => Ok(ApiResponseFactory.Success(result, "All reports resent successfully.")),
                    _ when successReports.Count == 0 => BadRequest(ApiResponseFactory.Fail(result, "All reports resend operations failed.")),
                    _ => Ok(ApiResponseFactory.PartialSuccess(result, "Some reports were resent successfully, others failed."))
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred during resend operation.");
                return StatusCode(500, ApiResponseFactory.Fail("An unexpected error occurred. Please contact support."));
            }
        }







        // Fetch Daily/Weekly/Monthly/Custom Dates Notifications (NOT IN USE)
        [HttpGet("labs/{labId}/notifications")]
        public async Task<IActionResult> GetLabNotifications(
        [FromRoute] int labId,
        [FromQuery] int? timeframe,
        [FromQuery] string? startDate,
        [FromQuery] string? endDate)
        {
            HttpContext.Items["Log-Category"] = "Lab Management";

            _logger.LogInformation("GetLabNotifications called for LabId: {LabId}, Timeframe: {Timeframe}, StartDate: {StartDate}, EndDate: {EndDate}", labId, timeframe, startDate, endDate);

            try
            {
                if (!await _labAuthorizationService.IsLabAuthorized(labId, User))
                {
                    _logger.LogWarning("Unauthorized access attempt for LabId: {LabId}", labId);
                    return Unauthorized(ApiResponseFactory.Fail("Permission denied. You can only create/modify/delete data for your main lab or its branches."));
                }

                var labAdminIdClaim = User.Claims.FirstOrDefault(c => c.Type == "LabAdminId");
                var roleClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);

                if (labAdminIdClaim == null || !int.TryParse(labAdminIdClaim.Value, out int labAdminId))
                {
                    _logger.LogWarning("Missing or invalid LabAdminId claim.");
                    return Unauthorized(ApiResponseFactory.Fail("Invalid or missing LabAdminId claim."));
                }

                if (roleClaim == null)
                {
                    _logger.LogWarning("Missing Role claim for LabAdminId: {LabAdminId}", labAdminId);
                    return Unauthorized(ApiResponseFactory.Fail("Invalid or missing Role claim."));
                }

                string role = roleClaim.Value;
                int? userId = null;

                if (role == "Super Admin")
                {
                    userId = await _context.LabSuperAdmins
                        .Where(a => a.Id == labAdminId)
                        .Select(a => (int?)a.UserId)
                        .FirstOrDefaultAsync();
                }
                else if (role == "Admin" || role == "Member")
                {
                    userId = await _context.LabMembers
                        .Where(m => m.LabId == labId && m.Id == labAdminId)
                        .Select(m => (int?)m.UserId)
                        .FirstOrDefaultAsync();
                }

                if (userId == null)
                {
                    _logger.LogWarning("UserId resolution failed for LabAdminId: {LabAdminId}, Role: {Role}", labAdminId, role);
                    return Unauthorized(ApiResponseFactory.Fail("Invalid access for the given role."));
                }

                long currentEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                long epochStart, epochEnd;

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
                    epochStart = timeframe switch
                    {
                        1 => currentEpoch - 86400,
                        2 => currentEpoch - 604800,
                        3 => currentEpoch - 2592000,
                        _ => currentEpoch - 86400
                    };
                    epochEnd = currentEpoch;
                }

                var recentReports = await _context.LabUserReports
                    .Where(r => r.LabId == labId && r.EpochTime >= epochStart && r.EpochTime <= epochEnd)
                    .ToListAsync();

                if (!recentReports.Any())
                {
                    _logger.LogInformation("No reports found for LabId: {LabId} in the given time range.", labId);
                    return NotFound(ApiResponseFactory.Fail("No reports found for the selected timeframe or date range."));
                }

                var reportIds = recentReports.Select(r => r.Id).ToList();

                var userReports = await _context.UserReports
                    .Where(ur => ur.LabUserReportId != null && reportIds.Contains(ur.LabUserReportId.Value))
                    .ToListAsync();

                var userIds = userReports.Select(ur => ur.UserId).Distinct().ToList();

                var userDetailsDict = await _context.Users
                    .Where(ud => userIds.Contains(ud.Id))
                    .ToDictionaryAsync(ud => ud.Id, ud => ud.FirstName);

                var labAdminUser = await _context.Users
                    .Where(ud => ud.Id == userId)
                    .Select(ud => ud.FirstName)
                    .FirstOrDefaultAsync() ?? "Unknown Admin";

                var notifications = userReports
                    .Select(ur =>
                    {
                        var labReport = recentReports.FirstOrDefault(lr => lr.Id == ur.LabUserReportId);
                        return new
                        {
                            ReportType = ReverseReportTypeMapping(ur.ReportCategory),
                            SentTo = userDetailsDict.TryGetValue(ur.UserId, out var userName) ? userName : "Unknown User",
                            SentBy = labAdminUser,
                            ElapsedMinutes = labReport != null ? (currentEpoch - labReport.EpochTime) / 60 : int.MaxValue
                        };
                    })
                    .OrderBy(n => n.ElapsedMinutes)
                    .ToList();

                _logger.LogInformation("Notifications fetched successfully for LabId: {LabId}. Total: {Count}", labId, notifications.Count);
                return Ok(ApiResponseFactory.Success(notifications, "Notifications fetched successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred in GetLabNotifications for LabId: {LabId}", labId);
                return StatusCode(500, ApiResponseFactory.Fail($"An unexpected error occurred: {ex.Message}"));
            }
        }





        // New Notifications 
        [HttpGet("labs/{labId}/notification")]
        [Authorize(Policy = "SuperAdminOrAdminPolicy")]
        public async Task<IActionResult> GetLabNotification(
            [FromRoute][Range(1, int.MaxValue)] int labId,
            [FromQuery] int? timeframe,
            [FromQuery] string? startDate,
            [FromQuery] string? endDate)
        {
            HttpContext.Items["Log-Category"] = "Notification Management";
            _logger.LogInformation("Fetching notifications for Lab ID: {LabId}, Timeframe: {Timeframe}, StartDate: {StartDate}, EndDate: {EndDate}", labId, timeframe, startDate, endDate);

            try
            {
                if (!await _labAuthorizationService.IsLabAuthorized(labId, User).ConfigureAwait(false))
                {
                    _logger.LogWarning("Unauthorized access attempt to Lab ID: {LabId}", labId);
                    return Unauthorized(ApiResponseFactory.Fail("Unauthorized access."));
                }

                long currentEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                long epochStart, epochEnd;

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
                    epochStart = timeframe switch
                    {
                        1 => currentEpoch - 86400,
                        2 => currentEpoch - 604800,
                        3 => currentEpoch - 2592000,
                        _ => 0
                    };
                    epochEnd = currentEpoch;
                }

                var notifications = await _context.LabAuditLogs
                     .AsNoTracking()
                     .Where(n => n.LabId == labId &&
                                 n.Timestamp >= epochStart &&
                                 n.Timestamp <= epochEnd)
                     .OrderByDescending(n => n.Timestamp)
                    .Select(n => new
                    {
                        n.Id,
                        n.LabId,
                        n.UserRole,
                        n.EntityName,
                        n.Category,
                        n.Timestamp,
                        n.Notifications,
                        ElapsedMinutes = (currentEpoch - n.Timestamp) / 60
                    })

                     .ToListAsync()
                     .ConfigureAwait(false);


                return Ok(ApiResponseFactory.Success(notifications, "Notifications fetched successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve notifications for Lab ID: {LabId}", labId);
                return StatusCode(500, ApiResponseFactory.Fail("Unexpected error occurred while fetching notifications."));
            }
        }

    }
}

