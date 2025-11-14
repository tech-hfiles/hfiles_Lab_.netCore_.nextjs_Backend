using HFiles_Backend.API.Interfaces;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HFiles_Backend.API.Services
{
    /// <summary>
    /// Provides country-related services, such as retrieving dialing codes.
    /// Implements <see cref="ICountryService"/> for standardized access.
    /// </summary>
    public class CountryService(AppDbContext context, ILogger<CountryService> logger) : ICountryService
    {
        // Injected database context for accessing country data
        private readonly AppDbContext _context = context;

        // Logger instance for structured logging and diagnostics
        private readonly ILogger<CountryService> _logger = logger;

        /// <summary>
        /// Asynchronously retrieves a list of country dialing codes from the database.
        /// Each entry includes the ISO code and international dialing code.
        /// </summary>
        /// <returns>
        /// A list of <see cref="CountryDialingCode"/> objects representing country codes and ISO codes.
        /// </returns>
        public async Task<List<CountryDialingCode>> GetAllDialingCodesAsync()
        {
            // Log the start of the operation for observability
            _logger.LogInformation("Starting dialing code fetch from 'countrylist2'.");

            try
            {
                // Query the 'countrylist2' table without EF change tracking for performance
                var dialingCodes = await _context.countrylist2
                    .AsNoTracking()
                    .Select(c => new CountryDialingCode
                    {
                        // Map database 'dialingcode' field to response model
                        DialingCode = c.dialingcode,

                        // Map ISO country code
                        Country = c.isocode
                    })
                    .ToListAsync(); // Execute query asynchronously

                // Log successful retrieval with total count
                _logger.LogInformation("Retrieved {Count} dialing codes.", dialingCodes.Count);

                // Return the constructed result
                return dialingCodes;
            }
            catch (Exception ex)
            {
                // Log any error that occurred during query execution
                _logger.LogError(ex, "Failed to fetch dialing codes.");

                // Re-throw as an application-specific exception with root cause attached
                throw new ApplicationException("Unable to retrieve country dialing codes.", ex);
            }
        }
    }
}
