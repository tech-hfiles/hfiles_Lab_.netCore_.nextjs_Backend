using HFiles_Backend.API.Interfaces;
using HFiles_Backend.Application.Common;
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
        IMemoryCache cache) : ControllerBase
    {
        private readonly ClinicStatisticsRepository _statisticsRepository = statisticsRepository;
        private readonly IClinicAuthorizationService _clinicAuthorizationService = clinicAuthorizationService;
        private readonly ILogger<ClinicStatisticsController> _logger = logger;
        private readonly IMemoryCache _cache = cache;





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

            // Input validation
            if (clinicId <= 0)
            {
                _logger.LogWarning("Invalid clinic ID provided: {ClinicId}", clinicId);
                return BadRequest(ApiResponseFactory.Fail("Clinic ID must be a positive integer."));
            }

            // Authorization check
            bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
            if (!isAuthorized)
            {
                _logger.LogWarning("Unauthorized statistics access attempt for Clinic ID {ClinicId}", clinicId);
                return Unauthorized(ApiResponseFactory.Fail("You are not authorized to view statistics for this clinic."));
            }

            // Parse date filters
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
                // Create cache key
                var cacheKey = $"clinic_stats_{clinicId}_{startDate}_{endDate}";

                // Try to get from cache
                if (!_cache.TryGetValue(cacheKey, out var statistics))
                {
                    // Fetch statistics
                    statistics = await _statisticsRepository.GetClinicStatisticsAsync(clinicId, start, end);

                    // Cache for 5 minutes
                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromMinutes(5))
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(15));

                    _cache.Set(cacheKey, statistics, cacheOptions);
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





        /// <summary>
        /// Clear statistics cache for a specific clinic (useful after data updates)
        /// </summary>
        [HttpPost("{clinicId:int}/statistics/clear-cache")]
        public IActionResult ClearStatisticsCache([FromRoute] int clinicId)
        {
            HttpContext.Items["Log-Category"] = "Clinic Statistics Cache";

            try
            {
                // Remove all cache entries for this clinic
                var cacheKeyPattern = $"clinic_stats_{clinicId}_";

                _logger.LogInformation("Cache cleared for Clinic ID {ClinicId}", clinicId);

                return Ok(ApiResponseFactory.Success("Cache cleared successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cache for Clinic ID {ClinicId}", clinicId);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while clearing cache."));
            }
        }
    }
}
