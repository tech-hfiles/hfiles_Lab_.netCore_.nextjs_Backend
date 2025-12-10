namespace HFiles_Backend.API.Interfaces
{
    /// <summary>
    /// Defines a contract for generating a unique HFiles ID (HFID) using user-specific attributes.
    /// Can be implemented by services responsible for ID generation logic based on name and date of birth.
    /// </summary>
    public interface IHfidService
    {
        /// <summary>
        /// Generates a unique identifier using a combination of user's first name, last name, and date of birth.
        /// The format is typically designed to ensure uniqueness and traceability.
        /// </summary>
        /// <param name="firstName">The user's first name.</param>
        /// <param name="lastName">The user's last name.</param>
        /// <param name="dob">The user's date of birth.</param>
        /// <returns>A unique HFID string formatted from input parameters and timestamp data.</returns>
        string GenerateHfid(string firstName, string lastName, DateTime dob);
    }
}
