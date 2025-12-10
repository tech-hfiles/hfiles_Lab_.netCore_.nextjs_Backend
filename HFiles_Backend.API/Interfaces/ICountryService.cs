using HFiles_Backend.Application.Common;

namespace HFiles_Backend.API.Interfaces
{
    /// <summary>
    /// Defines a contract for retrieving country-specific dialing codes used in mobile number formatting.
    /// Typically implemented by a service that interacts with a static or dynamic data source.
    /// </summary>
    public interface ICountryService
    {
        /// <summary>
        /// Asynchronously fetches all available country dialing codes.
        /// Each entry contains a numeric dialing prefix and corresponding ISO country identifier.
        /// </summary>
        /// <returns>
        /// A list of <see cref="CountryDialingCode"/> objects containing country metadata.
        /// </returns>
        Task<List<CountryDialingCode>> GetAllDialingCodesAsync();
    }
}
