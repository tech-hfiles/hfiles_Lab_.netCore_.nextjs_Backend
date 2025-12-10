using HFiles_Backend.Domain.Entities.Clinics;

namespace HFiles_Backend.Domain.Interfaces
{
    public interface IClinicGoogleTokenRepository
    {
        /// <summary>
        /// Get active Google token for a clinic
        /// </summary>
        Task<ClinicGoogleToken?> GetActiveTokenByClinicIdAsync(int clinicId);

        /// <summary>
        /// Save or update Google OAuth token
        /// </summary>
        Task SaveTokenAsync(ClinicGoogleToken token);

        /// <summary>
        /// Update existing token
        /// </summary>
        Task UpdateTokenAsync(ClinicGoogleToken token);

        /// <summary>
        /// Check if clinic has valid Google Calendar connection
        /// </summary>
        Task<bool> HasValidTokenAsync(int clinicId);

        /// <summary>
        /// Revoke/deactivate token for a clinic
        /// </summary>
        Task RevokeTokenAsync(int clinicId);

        /// <summary>
        /// Get all tokens that need refresh (expired or expiring soon)
        /// </summary>
        Task<List<ClinicGoogleToken>> GetTokensNeedingRefreshAsync();
    }
}
