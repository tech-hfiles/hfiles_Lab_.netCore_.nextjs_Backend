using HFiles_Backend.API.Interfaces;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Infrastructure.Data;
using iText.Commons.Actions.Contexts;
using Microsoft.EntityFrameworkCore;

namespace HFiles_Backend.API.Services
{
    public class TokenBlacklistService(AppDbContext context, ILogger<TokenBlacklistService> logger) : ITokenBlacklistService
    {
        private readonly AppDbContext _context = context;
        private readonly ILogger<TokenBlacklistService> _logger = logger;

        public async Task BlacklistTokenAsync(string sessionId, string reason)
        {
            try
            {
                var existingBlacklist = await _context.Set<BlacklistedToken>()
                    .FirstOrDefaultAsync(bt => bt.SessionId == sessionId);

                if (existingBlacklist != null)
                {
                    _logger.LogInformation("Token {SessionId} already blacklisted", sessionId);
                    return;
                }

                var blacklistedToken = new BlacklistedToken
                {
                    SessionId = sessionId,
                    UserId = 0, // Will be set by caller if needed
                    Reason = reason,
                    BlacklistedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(1) // Shorter expiry for individual tokens
                };

                _context.Set<BlacklistedToken>().Add(blacklistedToken);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Token {SessionId} blacklisted with reason: {Reason}", sessionId, reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to blacklist token {SessionId}", sessionId);
                throw;
            }
        }

        public async Task BlacklistAllUserTokensAsync(int userId, int clinicId, string reason)
        {
            try
            {
                // First, clean up any existing blacklist entries for this user
                await CleanupUserTokensAsync(userId);

                // Add new blacklist entry with shorter expiry time
                var blacklistedToken = new BlacklistedToken
                {
                    SessionId = $"USER_{userId}_ALL_TOKENS", // Special pattern for all user tokens
                    UserId = userId,
                    ClinicId = clinicId,
                    Reason = reason,
                    BlacklistedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(240) // Much shorter expiry - user can login again after 30 minutes if needed
                };

                _context.Set<BlacklistedToken>().Add(blacklistedToken);
                await _context.SaveChangesAsync();

                _logger.LogInformation("All tokens for user {UserId} blacklisted with reason: {Reason}", userId, reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to blacklist all tokens for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> IsTokenBlacklistedAsync(string sessionId)
        {
            try
            {
                // Check if specific session is blacklisted (and not expired)
                var isDirectBlacklisted = await _context.Set<BlacklistedToken>()
                    .AnyAsync(bt => bt.SessionId == sessionId && bt.ExpiresAt > DateTime.UtcNow);

                return isDirectBlacklisted;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check blacklist status for token {SessionId}", sessionId);
                return false; // Fail open - don't block users if there's a DB error
            }
        }

        public async Task<string?> GetBlacklistReasonAsync(string sessionId)
        {
            try
            {
                var blacklistedToken = await _context.Set<BlacklistedToken>()
                    .FirstOrDefaultAsync(bt => bt.SessionId == sessionId && bt.ExpiresAt > DateTime.UtcNow);

                return blacklistedToken?.Reason;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get blacklist reason for token {SessionId}", sessionId);
                return null;
            }
        }

        public async Task CleanupExpiredTokensAsync()
        {
            try
            {
                var expiredTokens = _context.BlacklistedTokens
                     .Where(t => t.ExpiresAt <= DateTime.UtcNow)
                     .ToList();

                if (expiredTokens.Any())
                {
                    _context.Set<BlacklistedToken>().RemoveRange(expiredTokens);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Cleaned up {Count} expired blacklisted tokens", expiredTokens.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup expired tokens");
            }
        }

        // New method to clean up specific user tokens
        public async Task CleanupUserTokensAsync(int userId)
        {
            try
            {
                var userTokens = await _context.Set<BlacklistedToken>()
                    .Where(bt => bt.UserId == userId || bt.SessionId == $"USER_{userId}_ALL_TOKENS")
                    .ToListAsync();

                if (userTokens.Any())
                {
                    _context.Set<BlacklistedToken>().RemoveRange(userTokens);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Cleaned up {Count} blacklisted tokens for user {UserId}", userTokens.Count, userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup tokens for user {UserId}", userId);
            }
        }

        // New method to remove blacklist entries after successful logout
        public async Task RemoveUserBlacklistAsync(int clinicId)
        {
            try
            {
                var tokens = _context.BlacklistedTokens
                         .Where(t => t.ClinicId == clinicId)
                         .ToList();

                _logger.LogInformation("Removed blacklist entries for user after successful logout");
                _context.BlacklistedTokens.RemoveRange(tokens);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove blacklist for user.");
            }
        }
    }
}