using HFiles_Backend.API.Interfaces;

namespace HFiles_Backend.API.Services
{
    /// <summary>
    /// Provides a unique identifier (HFID) generation based on personal details.
    /// Implements <see cref="IHfidService"/> to support consistent ID creation logic.
    /// </summary>
    public class HfidService(ILogger<HfidService> logger) : IHfidService
    {
        // Injected logger instance for structured logging throughout the service
        private readonly ILogger<HfidService> _logger = logger;

        /// <summary>
        /// Generates a unique HFID string using first name, last name, date of birth, and current epoch time.
        /// The format is: HF{DOB_MMddyy}{NamePart}{Last4Epoch}
        /// Example: HF062795JOH1234
        /// </summary>
        /// <param name="firstName">User's first name</param>
        /// <param name="lastName">User's last name</param>
        /// <param name="dob">Date of birth</param>
        /// <returns>Generated HFID string</returns>
        /// <exception cref="ArgumentException">Thrown when any input is invalid</exception>
        public string GenerateHfid(string firstName, string lastName, DateTime dob)
        {
            // Log the incoming parameters for traceability
            _logger.LogInformation("Generating HFID for {FirstName} {LastName} with DOB: {DOB}", firstName, lastName, dob);

            // Validate that first name is not null or empty
            if (string.IsNullOrWhiteSpace(firstName))
                throw new ArgumentException("First name cannot be null or empty");

            // Validate that last name is not null or empty
            if (string.IsNullOrWhiteSpace(lastName))
                throw new ArgumentException("Last name cannot be null or empty");

            // Ensure date of birth is not in the future
            if (dob > DateTime.Today)
                throw new ArgumentException("Date of birth cannot be in the future");

            // Combine trimmed first and last name for length calculation
            string fullName = firstName.Trim() + lastName.Trim();

            // Extract up to 3 characters from uppercase name for uniqueness
            string namePart = (firstName.Trim().ToUpper() + lastName.Trim().ToUpper())
                .Substring(0, Math.Min(3, fullName.Length));

            // Format date of birth as MMddyy string
            string dobPart = dob.ToString("MMddyy");

            // Get current epoch time in seconds
            long epochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Use last 4 digits of epoch time for uniqueness
            string last4Epoch = (epochTime % 10000).ToString("D4");

            // Construct the final HFID string
            string hfid = $"HF{dobPart}{namePart}{last4Epoch}";

            // Log the generated HFID
            _logger.LogInformation("Generated HFID: {Hfid}", hfid);

            // Return the final result
            return hfid;
        }
    }
}
