using HFiles_Backend.API.Interfaces;
using HFiles_Backend.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace HFiles_Backend.API.Controllers.Clinics;

/// <summary>
/// API controller for retrieving country-related information, such as dialing codes and country ISO3 names.
/// </summary>
[Route("api/")]
[ApiController]
public class CountryController(ICountryService countryService, ILogger<CountryController> logger) : ControllerBase
{
    /// <summary>
    /// Service responsible for country-related business logic and data retrieval.
    /// </summary>
    private readonly ICountryService _countryService = countryService;

    /// <summary>
    /// Logger instance for logging country-related operations.
    /// </summary>
    private readonly ILogger<CountryController> _logger = logger;





    /// <summary>
    /// Retrieves a list of international dialing codes for countries.
    /// </summary>
    /// <returns>
    /// An <see cref="IActionResult"/> containing a list of country dialing codes if available, 
    /// or a not found response if none are found.
    /// </returns>
    /// <response code="200">Dialing codes retrieved successfully.</response>
    /// <response code="404">No dialing codes were found.</response>
    /// <response code="500">Internal server error occurred while fetching dialing codes.</response>
    [HttpGet("country/dialing-codes")]
    public async Task<IActionResult> GetDialingCodes()
    {
        // Log the start of dialing code retrieval
        _logger.LogInformation("Fetching country dialing codes...");

        // Attempt to fetch dialing codes from service
        var result = await _countryService.GetAllDialingCodesAsync();

        // Check if dialing codes are returned
        if (result is not { Count: > 0 })
        {
            _logger.LogWarning("No dialing codes found.");
            return NotFound(ApiResponseFactory.Fail("No dialing codes found."));
        }

        // Log and return successful response
        _logger.LogInformation("Fetched {Count} dialing codes.", result.Count);
        return Ok(ApiResponseFactory.Success(result, "Dialing codes fetched successfully."));
    }
}
