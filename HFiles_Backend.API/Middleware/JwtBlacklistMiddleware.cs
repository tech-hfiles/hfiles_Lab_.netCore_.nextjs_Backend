// JwtBlacklistMiddleware.cs
using HFiles_Backend.API.Interfaces;
using HFiles_Backend.Application.Common;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace HFiles_Backend.API.Middleware
{
    public class JwtBlacklistMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<JwtBlacklistMiddleware> _logger;

        public JwtBlacklistMiddleware(RequestDelegate next, ILogger<JwtBlacklistMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ITokenBlacklistService tokenBlacklistService)
        {
            try
            {
                // Skip blacklist check for login endpoints and public endpoints
                if (ShouldSkipBlacklistCheck(context.Request.Path))
                {
                    await _next(context);
                    return;
                }

                // Check if user is authenticated
                if (context.User.Identity?.IsAuthenticated == true)
                {
                    var sessionId = context.User.FindFirst("SessionId")?.Value;
                    var userIdClaim = context.User.FindFirst("UserId")?.Value;

                    if (!string.IsNullOrEmpty(sessionId))
                    {
                        // Declare userId variable at the beginning of this scope
                        int userId = 0;
                        bool userIdParsed = int.TryParse(userIdClaim, out userId);

                        // Check if this specific session is blacklisted
                        var isBlacklisted = await tokenBlacklistService.IsTokenBlacklistedAsync(sessionId);

                        // If not directly blacklisted, check if all user tokens are blacklisted
                        if (!isBlacklisted && userIdParsed)
                        {
                            isBlacklisted = await tokenBlacklistService.IsTokenBlacklistedAsync($"USER_{userId}_ALL_TOKENS");
                        }

                        if (isBlacklisted)
                        {
                            string? reason = null;

                            // First try to get reason for specific session
                            reason = await tokenBlacklistService.GetBlacklistReasonAsync(sessionId);

                            // If no reason found for session and userId is valid, check for user-level blacklist reason
                            if (string.IsNullOrEmpty(reason) && userIdParsed)
                            {
                                reason = await tokenBlacklistService.GetBlacklistReasonAsync($"USER_{userId}_ALL_TOKENS");
                            }

                            _logger.LogWarning("Blacklisted token access attempt. SessionId: {SessionId}, UserId: {UserId}, Reason: {Reason}",
                                sessionId, userIdClaim, reason);

                            context.Response.StatusCode = 401;
                            context.Response.ContentType = "application/json";

                            var errorResponse = ApiResponseFactory.Fail(new List<string>
                            {
                                GetLogoutMessage(reason ?? "token_blacklisted")
                            });

                            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(errorResponse));
                            return;
                        }
                    }
                }

                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in JWT blacklist middleware");
                await _next(context);
            }
        }

        private static bool ShouldSkipBlacklistCheck(PathString path)
        {
            var skipPaths = new[]
             {
                "/api/clinics/users/login",        // ✅ Uncomment this
                "/api/clinics/super-admins",
                "/api/clinics/signup",             // Add signup paths
                "/api/clinics/login",              // Add generic login
                "/api/auth",                       // Add auth paths
                "/api/health",
                "/api/public"
            };

            return skipPaths.Any(skipPath => path.Value?.StartsWith(skipPath, StringComparison.OrdinalIgnoreCase) == true);
        }

        private static string GetLogoutMessage(string reason)
        {
            return reason switch
            {
                "super_admin_promotion" => "You have been demoted from Super Admin. Please login again with your new Admin credentials.",
                "promoted_to_super_admin" => "Congratulations! You have been promoted to Super Admin. Please login again with your new credentials.",
                "role_changed" => "Your role has been updated. Please login again for security purposes.",
                "security_logout" => "You have been logged out for security reasons. Please login again.",
                _ => "Your session has expired. Please login again."
            };
        }
    }
}