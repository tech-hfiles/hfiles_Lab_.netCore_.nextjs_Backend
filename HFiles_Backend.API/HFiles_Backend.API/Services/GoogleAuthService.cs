using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace HFiles_Backend.API.Services
{
    public class GoogleAuthService : IGoogleAuthService
    {
        private readonly IConfiguration _configuration;
        private readonly IClinicGoogleTokenRepository _tokenRepository;
        private readonly ILogger<GoogleAuthService> _logger;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string[] _scopes = { "https://www.googleapis.com/auth/calendar" };

        public GoogleAuthService(
         IConfiguration configuration,
         IClinicGoogleTokenRepository tokenRepository,
         ILogger<GoogleAuthService> logger)
        {
            _configuration = configuration;
            _tokenRepository = tokenRepository;
            _logger = logger;

            // ⭐ FIX: Use correct configuration path
            _clientId = _configuration["GoogleOAuth:ClientId"]  // Changed from Google: to GoogleOAuth
                ?? throw new InvalidOperationException("Google ClientId not configured");
            _clientSecret = _configuration["GoogleOAuth:ClientSecret"]  // Changed from Google: to GoogleOAuth
                ?? throw new InvalidOperationException("Google ClientSecret not configured");
        }

        public string GetAuthorizationUrl(int clinicId, string redirectUri)
        {
            try
            {
                var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
                {
                    ClientSecrets = new ClientSecrets
                    {
                        ClientId = _clientId,
                        ClientSecret = _clientSecret
                    },
                    Scopes = _scopes
                });

                var codeRequestUrl = flow.CreateAuthorizationCodeRequest(redirectUri);
                codeRequestUrl.State = clinicId.ToString(); // Pass clinic ID in state

                var authorizationUrl = codeRequestUrl.Build().ToString();

                _logger.LogInformation("Generated authorization URL for Clinic ID {ClinicId}", clinicId);
                return authorizationUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating authorization URL for Clinic ID {ClinicId}", clinicId);
                throw;
            }
        }

        public async Task<bool> ExchangeCodeForTokensAsync(string code, string redirectUri, int clinicId)
        {
            try
            {
                var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
                {
                    ClientSecrets = new ClientSecrets
                    {
                        ClientId = _clientId,
                        ClientSecret = _clientSecret
                    },
                    Scopes = _scopes
                });

                var tokenResponse = await flow.ExchangeCodeForTokenAsync(
                    userId: clinicId.ToString(),
                    code: code,
                    redirectUri: redirectUri,
                    CancellationToken.None);

                if (tokenResponse == null)
                {
                    _logger.LogWarning("Failed to exchange code for tokens for Clinic ID {ClinicId}", clinicId);
                    return false;
                }

                // Encrypt tokens before storing
                var encryptedAccessToken = EncryptToken(tokenResponse.AccessToken);
                var encryptedRefreshToken = EncryptToken(tokenResponse.RefreshToken);

                var googleToken = new ClinicGoogleToken
                {
                    ClinicId = clinicId,
                    AccessToken = encryptedAccessToken,
                    RefreshToken = encryptedRefreshToken,
                    TokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresInSeconds ?? 3600),
                    Scope = string.Join(" ", _scopes),
                    TokenType = tokenResponse.TokenType,
                    CalendarId = "primary"
                };

                await _tokenRepository.SaveTokenAsync(googleToken);

                _logger.LogInformation("Successfully exchanged code and saved tokens for Clinic ID {ClinicId}", clinicId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exchanging code for tokens for Clinic ID {ClinicId}", clinicId);
                return false;
            }
        }

        public async Task<bool> RefreshAccessTokenAsync(int clinicId)
        {
            try
            {
                var token = await _tokenRepository.GetActiveTokenByClinicIdAsync(clinicId);
                if (token == null)
                {
                    _logger.LogWarning("No token found for Clinic ID {ClinicId}", clinicId);
                    return false;
                }

                // Decrypt refresh token
                var decryptedRefreshToken = DecryptToken(token.RefreshToken);

                var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
                {
                    ClientSecrets = new ClientSecrets
                    {
                        ClientId = _clientId,
                        ClientSecret = _clientSecret
                    },
                    Scopes = _scopes
                });

                var tokenResponse = await flow.RefreshTokenAsync(
                    clinicId.ToString(),
                    decryptedRefreshToken,
                    CancellationToken.None);

                if (tokenResponse == null)
                {
                    _logger.LogWarning("Failed to refresh token for Clinic ID {ClinicId}", clinicId);
                    return false;
                }

                // Update token with new access token
                token.AccessToken = EncryptToken(tokenResponse.AccessToken);
                token.TokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresInSeconds ?? 3600);

                await _tokenRepository.UpdateTokenAsync(token);

                _logger.LogInformation("Successfully refreshed token for Clinic ID {ClinicId}", clinicId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token for Clinic ID {ClinicId}", clinicId);
                return false;
            }
        }

        public async Task<string?> GetValidAccessTokenAsync(int clinicId)
        {
            try
            {
                var token = await _tokenRepository.GetActiveTokenByClinicIdAsync(clinicId);
                if (token == null)
                {
                    _logger.LogWarning("No token found for Clinic ID {ClinicId}", clinicId);
                    return null;
                }

                // Check if token is expired or expiring soon (within 5 minutes)
                if (token.TokenExpiry <= DateTime.UtcNow.AddMinutes(5))
                {
                    _logger.LogInformation("Token expired or expiring soon for Clinic ID {ClinicId}, refreshing...", clinicId);
                    var refreshed = await RefreshAccessTokenAsync(clinicId);

                    if (!refreshed)
                    {
                        return null;
                    }

                    // Get updated token
                    token = await _tokenRepository.GetActiveTokenByClinicIdAsync(clinicId);
                    if (token == null)
                    {
                        return null;
                    }
                }

                // Decrypt and return access token
                return DecryptToken(token.AccessToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting valid access token for Clinic ID {ClinicId}", clinicId);
                return null;
            }
        }

        public async Task<bool> RevokeAccessAsync(int clinicId)
        {
            try
            {
                var token = await _tokenRepository.GetActiveTokenByClinicIdAsync(clinicId);
                if (token == null)
                {
                    _logger.LogWarning("No token to revoke for Clinic ID {ClinicId}", clinicId);
                    return false;
                }

                // Revoke token with Google
                var decryptedAccessToken = DecryptToken(token.AccessToken);
                using var httpClient = new HttpClient();
                var response = await httpClient.PostAsync(
                    $"https://oauth2.googleapis.com/revoke?token={decryptedAccessToken}",
                    null);

                // Mark token as inactive in database
                await _tokenRepository.RevokeTokenAsync(clinicId);

                _logger.LogInformation("Successfully revoked access for Clinic ID {ClinicId}", clinicId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking access for Clinic ID {ClinicId}", clinicId);
                return false;
            }
        }

        public async Task<bool> IsConnectedAsync(int clinicId)
        {
            return await _tokenRepository.HasValidTokenAsync(clinicId);
        }

        #region Token Encryption/Decryption

        /// <summary>
        /// Encrypt token before storing in database
        /// Uses AES-256 encryption
        /// </summary>
        private string EncryptToken(string plainText)
        {
            var encryptionKey = _configuration["Security:EncryptionKey"]
                ?? throw new InvalidOperationException("Encryption key not configured");

            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(encryptionKey.PadRight(32).Substring(0, 32));
            aes.IV = new byte[16]; // Use a proper IV in production

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var msEncrypt = new MemoryStream();
            using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
            using (var swEncrypt = new StreamWriter(csEncrypt))
            {
                swEncrypt.Write(plainText);
            }

            return Convert.ToBase64String(msEncrypt.ToArray());
        }

        /// <summary>
        /// Decrypt token retrieved from database
        /// </summary>
        private string DecryptToken(string cipherText)
        {
            var encryptionKey = _configuration["Security:EncryptionKey"]
                ?? throw new InvalidOperationException("Encryption key not configured");

            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(encryptionKey.PadRight(32).Substring(0, 32));
            aes.IV = new byte[16]; // Use the same IV used in encryption

            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var msDecrypt = new MemoryStream(Convert.FromBase64String(cipherText));
            using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using var srDecrypt = new StreamReader(csDecrypt);

            return srDecrypt.ReadToEnd();
        }
        #endregion
    }
}