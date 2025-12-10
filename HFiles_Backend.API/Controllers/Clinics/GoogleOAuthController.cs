using HFiles_Backend.API.Interfaces;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HFiles_Backend.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GoogleOAuthController : ControllerBase
    {
        private readonly IGoogleAuthService _googleAuthService;
        private readonly IClinicRepository _clinicRepository;
        private readonly IClinicAuthorizationService _clinicAuthorizationService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GoogleOAuthController> _logger;

        public GoogleOAuthController(
            IGoogleAuthService googleAuthService,
            IClinicRepository clinicRepository,
            IClinicAuthorizationService clinicAuthorizationService,
            IConfiguration configuration,
            ILogger<GoogleOAuthController> logger)
        {
            _googleAuthService = googleAuthService;
            _clinicRepository = clinicRepository;
            _clinicAuthorizationService = clinicAuthorizationService;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Initiate Google Calendar OAuth connection
        /// </summary>
        [HttpGet("connect/{clinicId}")]
        [Authorize]
        public async Task<IActionResult> ConnectGoogleCalendar([FromRoute] int clinicId)
        {
            try
            {
                // Verify clinic authorization
                bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
                if (!isAuthorized)
                {
                    _logger.LogWarning("Unauthorized Google Calendar connection attempt for Clinic ID {ClinicId}", clinicId);
                    return Unauthorized(ApiResponseFactory.Fail("You are not authorized to connect Google Calendar for this clinic."));
                }

                // Check if clinic exists
                var clinicExists = await _clinicRepository.ExistsAsync(clinicId);
                if (!clinicExists)
                {
                    return NotFound(ApiResponseFactory.Fail("Clinic not found."));
                }

                // Build redirect URI
                var redirectUri = $"{_configuration["AppSettings:BaseUrl"]}/api/GoogleOAuth/callback";

                // Generate authorization URL
                var authorizationUrl = _googleAuthService.GetAuthorizationUrl(clinicId, redirectUri);

                _logger.LogInformation("Generated Google OAuth URL for Clinic ID {ClinicId}", clinicId);

                return Ok(ApiResponseFactory.Success(new
                {
                    authorizationUrl,
                    message = "Redirect user to this URL to authorize Google Calendar access"
                }, "Authorization URL generated successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating Google Calendar connection for Clinic ID {ClinicId}", clinicId);
                return StatusCode(500, ApiResponseFactory.Fail("Error initiating Google Calendar connection."));
            }
        }

        /// <summary>
        /// OAuth callback endpoint - Google redirects here after user authorization
        /// </summary>
        [HttpGet("callback")]
        public async Task<IActionResult> OAuthCallback([FromQuery] string code, [FromQuery] string state, [FromQuery] string? error)
        {
            try
            {
                // Check if user denied access
                if (!string.IsNullOrEmpty(error))
                {
                    _logger.LogWarning("Google OAuth authorization denied: {Error}", error);
                    return BadRequest(ApiResponseFactory.Fail($"Authorization denied: {error}"));
                }

                if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
                {
                    return BadRequest(ApiResponseFactory.Fail("Invalid callback parameters."));
                }

                // Extract clinic ID from state
                if (!int.TryParse(state, out int clinicId))
                {
                    return BadRequest(ApiResponseFactory.Fail("Invalid clinic ID in state parameter."));
                }

                // Build redirect URI (must match the one used in authorization)
                var redirectUri = $"{_configuration["AppSettings:BaseUrl"]}/api/GoogleOAuth/callback";

                // Exchange code for tokens
                var success = await _googleAuthService.ExchangeCodeForTokensAsync(code, redirectUri, clinicId);

                if (!success)
                {
                    _logger.LogError("Failed to exchange authorization code for tokens for Clinic ID {ClinicId}", clinicId);
                    return StatusCode(500, ApiResponseFactory.Fail("Failed to complete Google Calendar authorization."));
                }

                _logger.LogInformation("Successfully connected Google Calendar for Clinic ID {ClinicId}", clinicId);

                // In production, redirect to a success page in your frontend
                var frontendSuccessUrl = $"{_configuration["AppSettings:FrontendUrl"]}/dashboard";
                return Redirect(frontendSuccessUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing OAuth callback");
                return StatusCode(500, ApiResponseFactory.Fail("Error processing authorization callback."));
            }
        }

        /// <summary>
        /// Check Google Calendar connection status for a clinic
        /// </summary>
        [HttpGet("status/{clinicId}")]
        [Authorize]
        public async Task<IActionResult> GetConnectionStatus([FromRoute] int clinicId)
        {
            try
            {
                bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
                if (!isAuthorized)
                {
                    return Unauthorized(ApiResponseFactory.Fail("You are not authorized to view this clinic's connection status."));
                }

                var isConnected = await _googleAuthService.IsConnectedAsync(clinicId);

                return Ok(ApiResponseFactory.Success(new
                {
                    clinicId,
                    isConnected,
                    message = isConnected
                        ? "Google Calendar is connected and active"
                        : "Google Calendar is not connected"
                }, "Connection status retrieved successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking connection status for Clinic ID {ClinicId}", clinicId);
                return StatusCode(500, ApiResponseFactory.Fail("Error checking connection status."));
            }
        }

        /// <summary>
        /// Disconnect/revoke Google Calendar access for a clinic
        /// </summary>
        [HttpPost("disconnect/{clinicId}")]
        [Authorize]
        public async Task<IActionResult> DisconnectGoogleCalendar([FromRoute] int clinicId)
        {
            try
            {
                bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
                if (!isAuthorized)
                {
                    _logger.LogWarning("Unauthorized Google Calendar disconnection attempt for Clinic ID {ClinicId}", clinicId);
                    return Unauthorized(ApiResponseFactory.Fail("You are not authorized to disconnect Google Calendar for this clinic."));
                }

                var success = await _googleAuthService.RevokeAccessAsync(clinicId);

                if (!success)
                {
                    return StatusCode(500, ApiResponseFactory.Fail("Failed to disconnect Google Calendar."));
                }

                _logger.LogInformation("Disconnected Google Calendar for Clinic ID {ClinicId}", clinicId);

                return Ok(ApiResponseFactory.Success(new
                {
                    clinicId,
                    message = "Google Calendar has been disconnected"
                }, "Google Calendar disconnected successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disconnecting Google Calendar for Clinic ID {ClinicId}", clinicId);
                return StatusCode(500, ApiResponseFactory.Fail("Error disconnecting Google Calendar."));
            }
        }

        /// <summary>
        /// Manually refresh access token (for testing/debugging)
        /// </summary>
        [HttpPost("refresh/{clinicId}")]
        [Authorize]
        public async Task<IActionResult> RefreshToken([FromRoute] int clinicId)
        {
            try
            {
                bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
                if (!isAuthorized)
                {
                    return Unauthorized(ApiResponseFactory.Fail("You are not authorized to refresh tokens for this clinic."));
                }

                var success = await _googleAuthService.RefreshAccessTokenAsync(clinicId);

                if (!success)
                {
                    return StatusCode(500, ApiResponseFactory.Fail("Failed to refresh access token."));
                }

                _logger.LogInformation("Manually refreshed token for Clinic ID {ClinicId}", clinicId);

                return Ok(ApiResponseFactory.Success(new
                {
                    clinicId,
                    message = "Access token refreshed successfully"
                }, "Token refreshed successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token for Clinic ID {ClinicId}", clinicId);
                return StatusCode(500, ApiResponseFactory.Fail("Error refreshing token."));
            }
        }
    }
}