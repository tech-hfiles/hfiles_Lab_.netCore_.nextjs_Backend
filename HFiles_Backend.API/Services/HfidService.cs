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
        /// Spaces in names are removed before processing (e.g., "Dr Ayush" becomes "DRAYUSH")
        /// </summary>
        /// <param name="firstName">User's first name</param>
        /// <param name="lastName">User's last name</param>
        /// <param name="dob">Date of birth</param>
        /// <returns>Generated HFID string</returns>
        /// <exception cref="ArgumentException">Thrown when any input is invalid</exception>
        public string GenerateHfid(string firstName, string lastName, DateTime dob)
        {
            // Log the incoming parameters for traceability
            _logger.LogInformation("Generating HFID for {FirstName} {LastName} with DOB: {DOB}",
                firstName, lastName, dob);

            // Validate that first name is not null or empty
            if (string.IsNullOrWhiteSpace(firstName))
                throw new ArgumentException("First name cannot be null or empty");

            // Validate that last name is not null or empty
            if (string.IsNullOrWhiteSpace(lastName))
                throw new ArgumentException("Last name cannot be null or empty");

            // Ensure date of birth is not in the future
            if (dob > DateTime.Today)
                throw new ArgumentException("Date of birth cannot be in the future");

            // Remove all spaces and special characters from names
            string cleanFirstName = RemoveSpacesAndSpecialChars(firstName.Trim());
            string cleanLastName = RemoveSpacesAndSpecialChars(lastName.Trim());

            // Combine cleaned names
            string fullName = cleanFirstName + cleanLastName;

            // Validate that we have some characters to work with after cleaning
            if (string.IsNullOrWhiteSpace(fullName))
                throw new ArgumentException("Names must contain at least one valid character");

            // Extract up to 3 characters from uppercase combined name for uniqueness
            string namePart = fullName.ToUpper().Substring(0, Math.Min(3, fullName.Length));

            // Format date of birth as MMddyy string
            string dobPart = dob.ToString("MMddyy");

            // Get current epoch time in seconds
            long epochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Use last 4 digits of epoch time for uniqueness
            string last4Epoch = (epochTime % 10000).ToString("D4");

            // Construct the final HFID string
            string hfid = $"HF{dobPart}{namePart}{last4Epoch}";

            // Log the generated HFID
            _logger.LogInformation("Generated HFID: {Hfid} from cleaned names: {FirstName} + {LastName}",
                hfid, cleanFirstName, cleanLastName);

            // Return the final result
            return hfid;
        }
		/// <summary>
		/// Generates a unique HFID for a child user, linked to their parent's HFID.
		/// The format is: HFCH{DOB_MMddyy}{NamePart}{ParentRef}
		/// Example: HFCH031015EMM1234 (where 1234 is from parent's HFID)
		/// This creates a clear parent-child relationship while maintaining uniqueness.
		/// </summary>
		/// <param name="parentHfid">Parent's HFID for reference linking</param>
		/// <param name="childFirstName">Child's first name</param>
		/// <param name="childLastName">Child's last name</param>
		/// <param name="childDob">Child's date of birth</param>
		/// <returns>Generated child HFID string</returns>
		/// <exception cref="ArgumentException">Thrown when any input is invalid</exception>
		public string GenerateChildHfid(string parentHfid, string childFirstName, string childLastName, DateTime childDob)
		{
			// Log the incoming parameters
			_logger.LogInformation(
				"Generating Child HFID for {ChildFirstName} {ChildLastName} with DOB: {ChildDOB}, Parent HFID: {ParentHfid}",
				childFirstName, childLastName, childDob, parentHfid);

			// Validate parent HFID
			if (string.IsNullOrWhiteSpace(parentHfid))
				throw new ArgumentException("Parent HFID cannot be null or empty");

			// Validate child first name
			if (string.IsNullOrWhiteSpace(childFirstName))
				throw new ArgumentException("Child first name cannot be null or empty");

			// Validate child last name
			if (string.IsNullOrWhiteSpace(childLastName))
				throw new ArgumentException("Child last name cannot be null or empty");

			// Ensure child DOB is not in the future
			if (childDob > DateTime.Today)
				throw new ArgumentException("Child date of birth cannot be in the future");

			// Validate child is actually a child (under 18)
			var age = DateTime.Today.Year - childDob.Year;
			if (childDob > DateTime.Today.AddYears(-age)) age--;
			if (age >= 18)
				throw new ArgumentException("Child must be under 18 years old");

			// Remove all spaces and special characters from names
			string cleanChildFirstName = RemoveSpacesAndSpecialChars(childFirstName.Trim());
			string cleanChildLastName = RemoveSpacesAndSpecialChars(childLastName.Trim());

			// Combine cleaned names
			string childFullName = cleanChildFirstName + cleanChildLastName;

			// Validate that we have some characters to work with after cleaning
			if (string.IsNullOrWhiteSpace(childFullName))
				throw new ArgumentException("Child names must contain at least one valid character");

			// Extract up to 3 characters from uppercase combined name for uniqueness
			string childNamePart = childFullName.ToUpper().Substring(0, Math.Min(3, childFullName.Length));

			// Format child date of birth as MMddyy string
			string childDobPart = childDob.ToString("MMddyy");

			// Extract last 4 characters from parent HFID as reference
			// This creates a link between parent and child HFIDs
			string parentRef = parentHfid.Length >= 4
				? parentHfid.Substring(parentHfid.Length - 4)
				: parentHfid.PadLeft(4, '0');

			// Construct the final child HFID string
			// Format: HFCH{DOB_MMddyy}{NamePart}{ParentRef}
			string childHfid = $"HFCH{childDobPart}{childNamePart}{parentRef}";

			// Log the generated child HFID
			_logger.LogInformation(
				"Generated Child HFID: {ChildHfid} from cleaned names: {FirstName} + {LastName}, linked to parent: {ParentHfid}",
				childHfid, cleanChildFirstName, cleanChildLastName, parentHfid);

			// Return the final result
			return childHfid;
		}


		/// <summary>
		/// Removes spaces, dots, and other common special characters from a name string.
		/// Preserves alphabetic characters only.
		/// </summary>
		/// <param name="input">The input string to clean</param>
		/// <returns>Cleaned string with only alphabetic characters</returns>
		private static string RemoveSpacesAndSpecialChars(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // Remove spaces, dots, commas, hyphens, and other common special characters
            // Keep only letters (supports international characters)
            return new string(input.Where(c => char.IsLetter(c)).ToArray());
        }
    }
}
