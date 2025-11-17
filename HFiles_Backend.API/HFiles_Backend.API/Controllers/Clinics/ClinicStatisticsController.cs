using HFiles_Backend.API.Interfaces;
using HFiles_Backend.API.Services;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;

namespace HFiles_Backend.API.Controllers.Clinics
{
    [ApiController]
    [Route("api/clinic")]
    [Authorize]
    public class ClinicStatisticsController(
        ClinicStatisticsRepository statisticsRepository,
        IClinicAuthorizationService clinicAuthorizationService,
        ILogger<ClinicStatisticsController> logger,
        IMemoryCache cache,
        IClinicStatisticsCacheService cacheService,
        IUserRepository userRepository) : ControllerBase
    {
        private readonly ClinicStatisticsRepository _statisticsRepository = statisticsRepository;
        private readonly IClinicAuthorizationService _clinicAuthorizationService = clinicAuthorizationService;
        private readonly ILogger<ClinicStatisticsController> _logger = logger;
        private readonly IMemoryCache _cache = cache;
        private readonly IClinicStatisticsCacheService _cacheService = cacheService;
        private readonly IUserRepository _userRepository = userRepository;





        /// <summary>
        /// Get comprehensive statistics for a clinic including registrations, revenue, appointments, and demographics
        /// </summary>
        /// <param name="clinicId">The clinic ID to fetch statistics for</param>
        /// <param name="startDate">Optional start date filter in dd-MM-yyyy format</param>
        /// <param name="endDate">Optional end date filter in dd-MM-yyyy format</param>
        /// <returns>Complete statistics dashboard data</returns>
        [HttpGet("{clinicId:int}/statistics")]
        public async Task<IActionResult> GetClinicStatistics(
             [FromRoute] int clinicId,
             [FromQuery] string? startDate,
             [FromQuery] string? endDate)
        {
            HttpContext.Items["Log-Category"] = "Clinic Statistics";

            if (clinicId <= 0)
            {
                _logger.LogWarning("Invalid clinic ID provided: {ClinicId}", clinicId);
                return BadRequest(ApiResponseFactory.Fail("Clinic ID must be a positive integer."));
            }

            bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
            if (!isAuthorized)
            {
                _logger.LogWarning("Unauthorized statistics access attempt for Clinic ID {ClinicId}", clinicId);
                return Unauthorized(ApiResponseFactory.Fail("You are not authorized to view statistics for this clinic."));
            }

            DateTime? start = null;
            DateTime? end = null;

            if (!string.IsNullOrEmpty(startDate))
            {
                if (!DateTime.TryParseExact(startDate, "dd-MM-yyyy", null, DateTimeStyles.None, out var parsedStart))
                {
                    _logger.LogWarning("Invalid startDate format: {StartDate}", startDate);
                    return BadRequest(ApiResponseFactory.Fail("Invalid startDate format. Expected dd-MM-yyyy."));
                }
                start = parsedStart;
            }

            if (!string.IsNullOrEmpty(endDate))
            {
                if (!DateTime.TryParseExact(endDate, "dd-MM-yyyy", null, DateTimeStyles.None, out var parsedEnd))
                {
                    _logger.LogWarning("Invalid endDate format: {EndDate}", endDate);
                    return BadRequest(ApiResponseFactory.Fail("Invalid endDate format. Expected dd-MM-yyyy."));
                }
                end = parsedEnd;
            }

            try
            {
                var cacheKey = $"clinic_stats_{clinicId}_{startDate}_{endDate}";

                if (!_cache.TryGetValue(cacheKey, out var statistics))
                {
                    statistics = await _statisticsRepository.GetClinicStatisticsAsync(clinicId, start, end);

                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromMinutes(5))
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(15));

                    _cache.Set(cacheKey, statistics, cacheOptions);
                    _cacheService.TrackCacheKey(cacheKey);
                }

                _logger.LogInformation("Successfully fetched statistics for Clinic ID {ClinicId}", clinicId);
                return Ok(ApiResponseFactory.Success(statistics, "Statistics fetched successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching statistics for Clinic ID {ClinicId}", clinicId);
                return StatusCode(500, ApiResponseFactory.Fail("An unexpected error occurred while retrieving clinic statistics."));
            }
        }






        [HttpPost("{clinicId:int}/statistics/clear-cache")]
        [Authorize]
        public async Task<IActionResult> ClearStatisticsCache([FromRoute] int clinicId)
        {
            HttpContext.Items["Log-Category"] = "Clinic Statistics Cache";

            bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
            if (!isAuthorized)
            {
                return Unauthorized(ApiResponseFactory.Fail("You are not authorized to clear cache for this clinic."));
            }

            try
            {
                _cacheService.InvalidateClinicStatistics(clinicId);
                _logger.LogInformation("Cache cleared for Clinic ID {ClinicId}", clinicId);
                return Ok(ApiResponseFactory.Success("Cache cleared successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cache for Clinic ID {ClinicId}", clinicId);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while clearing cache."));
            }
        }




        /// <summary>
        /// Gets statistics about new users for a clinic within a date range.
        /// Returns both count and detailed information about each new user.
        /// </summary>
        /// <param name="clinicId">The clinic ID to get statistics for</param>
        /// <param name="startDate">Start date in format dd-MM-yyyy</param>
        /// <param name="endDate">End date in format dd-MM-yyyy</param>
        /// <returns>Statistics including count and detailed user list</returns>
        [HttpGet("clinic/{clinicId}/new-users-stats")]
        public async Task<IActionResult> GetNewClinicUsersStats(
            [FromRoute] int clinicId,
            [FromQuery] string startDate,
            [FromQuery] string endDate,
            [FromServices] UserRepository userRepository1)
        {
            HttpContext.Items["Log-Category"] = "Clinic User Statistics";

            // Validate clinic ID
            if (clinicId <= 0)
            {
                _logger.LogWarning("Invalid clinic ID provided: {ClinicId}", clinicId);
                return BadRequest(ApiResponseFactory.Fail("Clinic ID must be a positive integer."));
            }

            // Authorization check
            //if (!await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User))
            //{
            //    _logger.LogWarning(
            //        "Unauthorized access attempt to clinic {ClinicId} statistics",
            //        clinicId);
            //    return Unauthorized(ApiResponseFactory.Fail("Not authorized to view this clinic's statistics."));
            //}

            // Validate and parse start date
            if (!DateTime.TryParseExact(
                startDate,
                "dd-MM-yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var start))
            {
                _logger.LogWarning("Invalid start date format: {StartDate}", startDate);
                return BadRequest(ApiResponseFactory.Fail("Invalid startDate format. Expected dd-MM-yyyy."));
            }

            // Validate and parse end date
            if (!DateTime.TryParseExact(
                endDate,
                "dd-MM-yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var end))
            {
                _logger.LogWarning("Invalid end date format: {EndDate}", endDate);
                return BadRequest(ApiResponseFactory.Fail("Invalid endDate format. Expected dd-MM-yyyy."));
            }

            // Validate date range
            if (end < start)
            {
                _logger.LogWarning(
                    "Invalid date range: start {StartDate} is after end {EndDate}",
                    start, end);
                return BadRequest(ApiResponseFactory.Fail("End date must be after start date."));
            }

            try
            {
                // Verify clinic exists
                var clinicExists = await _userRepository.ExistsAsync(clinicId);
                if (!clinicExists)
                {
                    _logger.LogWarning("Clinic not found: {ClinicId}", clinicId);
                    return NotFound(ApiResponseFactory.Fail("Clinic not found."));
                }

                // Get statistics (end date is exclusive, so add 1 day)
                var endDateExclusive = end.AddDays(1);

                _logger.LogInformation(
                    "Fetching new user statistics for clinic {ClinicId} from {StartDate} to {EndDate}",
                    clinicId, start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd"));

                // Get count
                var count = await _userRepository.GetNewClinicUsersCountAsync(
                    clinicId,
                    start,
                    endDateExclusive);

                // Get detailed information
                var details = await userRepository1.GetNewClinicUsersDetailedAsync(
                    clinicId,
                    start,
                    endDateExclusive);

                // Get clinic information
                var clinic = await _userRepository.GetClinicByIdAsync(clinicId);

                // Build response
                var response = new
                {
                    ClinicId = clinicId,
                    ClinicName = clinic?.ClinicName ?? "Unknown",
                    StartDate = start.ToString("dd-MM-yyyy"),
                    EndDate = end.ToString("dd-MM-yyyy"),
                    TotalNewUsers = count,
                    Users = details.Select(u => new
                    {
                        u.UserId,
                        u.HFID,
                        u.FullName,
                        u.PhoneNumber,
                        u.Email,
                        FirstVisitDate = u.FirstVisitDate.ToString("dd-MM-yyyy"),
                        UserCreatedDate = u.UserCreatedDate.ToString("dd-MM-yyyy"),
                        DaysSinceFirstVisit = (DateTime.Today - u.FirstVisitDate.Date).Days
                    }).ToList()
                };

                _logger.LogInformation(
                    "Successfully retrieved statistics for clinic {ClinicId}: {Count} new users found",
                    clinicId, count);

                return Ok(ApiResponseFactory.Success(
                    response,
                    $"Found {count} new user{(count == 1 ? "" : "s")} for the specified period."));
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error retrieving user statistics for clinic {ClinicId}",
                    clinicId);
                return StatusCode(500, ApiResponseFactory.Fail(
                    "An unexpected error occurred while retrieving statistics."));
            }
        }
    }
}
