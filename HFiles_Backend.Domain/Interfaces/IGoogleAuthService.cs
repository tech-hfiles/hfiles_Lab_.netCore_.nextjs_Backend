namespace HFiles_Backend.Domain.Interfaces
{
    public interface IGoogleAuthService
    {
        /// <summary>
        /// Generate Google OAuth authorization URL for clinic to connect their calendar
        /// </summary>
        string GetAuthorizationUrl(int clinicId, string redirectUri);

        /// <summary>
        /// Exchange authorization code for access and refresh tokens
        /// </summary>
        Task<bool> ExchangeCodeForTokensAsync(string code, string redirectUri, int clinicId);

        /// <summary>
        /// Refresh an expired access token using refresh token
        /// </summary>
        Task<bool> RefreshAccessTokenAsync(int clinicId);

        /// <summary>
        /// Get valid access token for a clinic (refresh if needed)
        /// </summary>
        Task<string?> GetValidAccessTokenAsync(int clinicId);

        /// <summary>
        /// Revoke Google Calendar access for a clinic
        /// </summary>
        Task<bool> RevokeAccessAsync(int clinicId);

        /// <summary>
        /// Check if clinic has valid Google Calendar connection
        /// </summary>
        Task<bool> IsConnectedAsync(int clinicId);
    }
}
