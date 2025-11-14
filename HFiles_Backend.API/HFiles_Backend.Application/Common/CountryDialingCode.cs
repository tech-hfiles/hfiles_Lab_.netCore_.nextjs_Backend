namespace HFiles_Backend.Application.Common
{
    /// <summary>
    /// Represents a country and its corresponding international dialing code.
    /// Used primarily for phone number formatting, validation, and country selection in user interfaces.
    /// </summary>
    public class CountryDialingCode
    {
        /// <summary>
        /// The international dialing code associated with the country.
        /// Typically includes a plus sign followed by digits (e.g., "+91" for India).
        /// May be null if data is incomplete or not loaded.
        /// </summary>
        public string? DialingCode { get; set; }

        /// <summary>
        /// The name of the country associated with the dialing code.
        /// Can be a full country name (e.g., "United States") and may be null if not available.
        /// </summary>
        public string? Country { get; set; }
    }
}
