using System.Security.Claims;

namespace HFiles_Backend.API.Interfaces
{
    public interface IClinicAuthorizationService
    {
        /// <summary>
        /// Checks if the current user is authorized to access the specified clinic.
        /// </summary>
        /// <param name="clinicId">Target clinic ID.</param>
        /// <param name="user">Authenticated user principal.</param>
        /// <returns>True if authorized, false otherwise.</returns>
        Task<bool> IsClinicAuthorized(int clinicId, ClaimsPrincipal user);
    }
}
