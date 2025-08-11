using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ApiJwtSettings = HFiles_Backend.API.Settings.JwtSettings;

namespace HFiles_Backend.API.Services
{
    public class JwtTokenService
    {
        private readonly ApiJwtSettings _jwtSettings;
        private readonly ILogger<JwtTokenService> _logger;

        public JwtTokenService(IOptions<ApiJwtSettings> jwtOptions, ILogger<JwtTokenService> logger)
        {
            _jwtSettings = jwtOptions.Value ?? throw new ArgumentNullException(nameof(jwtOptions), "JWT settings cannot be null.");
            _logger = logger ?? throw new ArgumentNullException(nameof(logger), "Logger cannot be null.");

            _logger.LogInformation("Initializing JWT Token Service with configuration.");

            if (string.IsNullOrWhiteSpace(_jwtSettings.Key))
                throw new ArgumentException("JWT secret key is missing or empty in configuration.");

            if (string.IsNullOrWhiteSpace(_jwtSettings.Issuer))
                throw new ArgumentException("JWT issuer is missing or empty in configuration.");

            if (string.IsNullOrWhiteSpace(_jwtSettings.Audience))
                throw new ArgumentException("JWT audience is missing or empty in configuration.");

            if (_jwtSettings.DurationInMinutes <= 0)
                throw new ArgumentException("JWT duration must be a positive number.");
        }

        public (string Token, string SessionId) GenerateToken(int userId, string email, int labAdminId, string role, int clinicAdminId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                    throw new ArgumentException("Email cannot be null or empty.", nameof(email));

                if (string.IsNullOrWhiteSpace(role))
                    throw new ArgumentException("Role cannot be null or empty.", nameof(role));

                var sessionId = Guid.NewGuid().ToString();
                var issuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var expiration = DateTimeOffset.UtcNow.AddMinutes(_jwtSettings.DurationInMinutes).ToUnixTimeSeconds();

                _logger.LogInformation("Generating JWT Token for UserID: {UserId}, Role: {UserRole}, SessionID: {SessionId}",
                    userId, role, sessionId);

                var claims = new List<Claim>
                {
                    new(JwtRegisteredClaimNames.Sub, email),
                    new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                    new("UserId", userId.ToString()),
                    new("LabAdminId", labAdminId.ToString()),
                    new("ClinicAdminId", clinicAdminId.ToString()),
                    new(ClaimTypes.Role, role),
                    new("SessionId", sessionId),
                    new("iat", issuedAt.ToString()),
                    new("exp", expiration.ToString())
                };

                var keyBytes = Encoding.UTF8.GetBytes(_jwtSettings.Key!);
                var key = new SymmetricSecurityKey(keyBytes);
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(claims),
                    Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.DurationInMinutes),
                    SigningCredentials = creds,
                    Issuer = _jwtSettings.Issuer,
                    Audience = _jwtSettings.Audience
                };

                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.CreateEncodedJwt(tokenDescriptor);

                _logger.LogInformation("JWT Token successfully generated for UserID: {UserId}, SessionID: {SessionId}",
                    userId, sessionId);

                return (jwtToken, sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token generation failed for UserID: {UserId}, Email: {Email}", userId, email);
                throw;
            }
        }
    }
}
