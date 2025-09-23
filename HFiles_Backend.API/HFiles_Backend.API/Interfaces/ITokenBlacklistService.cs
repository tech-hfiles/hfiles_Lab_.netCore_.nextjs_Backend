namespace HFiles_Backend.API.Interfaces
{
    public interface ITokenBlacklistService
    {
        Task BlacklistTokenAsync(string sessionId, string reason);
        Task BlacklistAllUserTokensAsync(int userId, string reason);
        Task<bool> IsTokenBlacklistedAsync(string sessionId);
        Task<string?> GetBlacklistReasonAsync(string sessionId);
        Task CleanupExpiredTokensAsync();
        Task CleanupUserTokensAsync(int userId);        // New method
        Task RemoveUserBlacklistAsync(int userId);      // New method
    }
}
