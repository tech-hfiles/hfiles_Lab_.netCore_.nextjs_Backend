using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using HFiles_Backend.Domain.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace HFiles_Backend.API.Services
{
    public class GoogleCalendarService : IGoogleCalendarService
    {
        private readonly IGoogleAuthService _googleAuthService;
        private readonly IClinicGoogleTokenRepository _tokenRepository;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GoogleCalendarService> _logger;

        public GoogleCalendarService(
            IGoogleAuthService googleAuthService,
            IClinicGoogleTokenRepository tokenRepository,
            IConfiguration configuration,
            ILogger<GoogleCalendarService> logger)
        {
            _googleAuthService = googleAuthService;
            _tokenRepository = tokenRepository;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string?> CreateAppointmentAsync(
            int clinicId,
            string patientName,
            string clinicName,
            DateTime appointmentDate,
            TimeSpan appointmentTime,
            string phoneNumber)
        {
            try
            {
                var service = await GetCalendarServiceAsync(clinicId);
                if (service == null)
                {
                    _logger.LogWarning("Cannot create appointment - Clinic ID {ClinicId} not connected to Google Calendar", clinicId);
                    return null;
                }

                // ⭐ TIMEZONE FIX: Create DateTimeOffset in India timezone
                var istTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
                var localDateTime = appointmentDate.Date.Add(appointmentTime);

                // Convert to DateTimeOffset with IST offset
                var appointmentDateTimeOffset = new DateTimeOffset(localDateTime, istTimeZone.GetUtcOffset(localDateTime));
                var appointmentEndTimeOffset = appointmentDateTimeOffset.AddMinutes(30);

                var newEvent = new Event
                {
                    Summary = $"Appointment: {patientName}",
                    Description = $"Clinic: {clinicName}\nPatient: {patientName}\nPhone: {phoneNumber}",
                    Start = new EventDateTime
                    {
                        DateTimeDateTimeOffset = appointmentDateTimeOffset, // ✅ Fixed: Use DateTimeDateTimeOffset
                        TimeZone = "Asia/Kolkata"
                    },
                    End = new EventDateTime
                    {
                        DateTimeDateTimeOffset = appointmentEndTimeOffset, // ✅ Fixed: Use DateTimeDateTimeOffset
                        TimeZone = "Asia/Kolkata"
                    },
                    ColorId = "9", // Blue color
                    Reminders = new Event.RemindersData
                    {
                        UseDefault = false,
                        Overrides = new[]
                        {
                            new EventReminder { Method = "email", Minutes = 24 * 60 },
                            new EventReminder { Method = "popup", Minutes = 30 }
                        }
                    }
                };

                var token = await _tokenRepository.GetActiveTokenByClinicIdAsync(clinicId);
                var calendarId = token?.CalendarId ?? "primary";

                var request = service.Events.Insert(newEvent, calendarId);
                var createdEvent = await request.ExecuteAsync();

                _logger.LogInformation(
                    "Created Google Calendar event {EventId} for Clinic ID {ClinicId}, Patient: {PatientName}",
                    createdEvent.Id, clinicId, patientName);

                return createdEvent.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error creating Google Calendar event for Clinic ID {ClinicId}, Patient: {PatientName}",
                    clinicId, patientName);
                return null;
            }
        }

        public async Task<bool> UpdateAppointmentAsync(
            int clinicId,
            string eventId,
            string patientName,
            string clinicName,
            DateTime appointmentDate,
            TimeSpan appointmentTime,
            string phoneNumber)
        {
            try
            {
                var service = await GetCalendarServiceAsync(clinicId);
                if (service == null)
                {
                    _logger.LogWarning("Cannot update appointment - Clinic ID {ClinicId} not connected to Google Calendar", clinicId);
                    return false;
                }

                var token = await _tokenRepository.GetActiveTokenByClinicIdAsync(clinicId);
                var calendarId = token?.CalendarId ?? "primary";

                var existingEvent = await service.Events.Get(calendarId, eventId).ExecuteAsync();
                if (existingEvent == null)
                {
                    _logger.LogWarning("Event {EventId} not found", eventId);
                    return false;
                }

                // ⭐ TIMEZONE FIX: Create DateTimeOffset in India timezone
                var istTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
                var localDateTime = appointmentDate.Date.Add(appointmentTime);

                var appointmentDateTimeOffset = new DateTimeOffset(localDateTime, istTimeZone.GetUtcOffset(localDateTime));
                var appointmentEndTimeOffset = appointmentDateTimeOffset.AddMinutes(30);

                existingEvent.Summary = $"Appointment: {patientName}";
                existingEvent.Description = $"Clinic: {clinicName}\nPatient: {patientName}\nPhone: {phoneNumber}";
                existingEvent.Start = new EventDateTime
                {
                    DateTimeDateTimeOffset = appointmentDateTimeOffset, // ✅ Fixed
                    TimeZone = "Asia/Kolkata"
                };
                existingEvent.End = new EventDateTime
                {
                    DateTimeDateTimeOffset = appointmentEndTimeOffset, // ✅ Fixed
                    TimeZone = "Asia/Kolkata"
                };

                var request = service.Events.Update(existingEvent, calendarId, eventId);
                await request.ExecuteAsync();

                _logger.LogInformation(
                    "Updated Google Calendar event {EventId} for Clinic ID {ClinicId}",
                    eventId, clinicId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error updating Google Calendar event {EventId} for Clinic ID {ClinicId}",
                    eventId, clinicId);
                return false;
            }
        }

        public async Task<bool> DeleteAppointmentAsync(int clinicId, string eventId)
        {
            try
            {
                var service = await GetCalendarServiceAsync(clinicId);
                if (service == null)
                {
                    _logger.LogWarning("Cannot delete appointment - Clinic ID {ClinicId} not connected to Google Calendar", clinicId);
                    return false;
                }

                var token = await _tokenRepository.GetActiveTokenByClinicIdAsync(clinicId);
                var calendarId = token?.CalendarId ?? "primary";

                var request = service.Events.Delete(calendarId, eventId);
                await request.ExecuteAsync();

                _logger.LogInformation(
                    "Deleted Google Calendar event {EventId} for Clinic ID {ClinicId}",
                    eventId, clinicId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error deleting Google Calendar event {EventId} for Clinic ID {ClinicId}",
                    eventId, clinicId);
                return false;
            }
        }

        public async Task<bool> CancelAppointmentAsync(int clinicId, string eventId)
        {
            try
            {
                var service = await GetCalendarServiceAsync(clinicId);
                if (service == null)
                {
                    _logger.LogWarning("Cannot cancel appointment - Clinic ID {ClinicId} not connected to Google Calendar", clinicId);
                    return false;
                }

                var token = await _tokenRepository.GetActiveTokenByClinicIdAsync(clinicId);
                var calendarId = token?.CalendarId ?? "primary";

                var existingEvent = await service.Events.Get(calendarId, eventId).ExecuteAsync();
                if (existingEvent == null)
                {
                    _logger.LogWarning("Event {EventId} not found", eventId);
                    return false;
                }

                // Mark as cancelled
                existingEvent.Status = "cancelled";
                existingEvent.Summary = "[CANCELLED] " + existingEvent.Summary;
                existingEvent.ColorId = "11"; // Red color for cancelled

                var request = service.Events.Update(existingEvent, calendarId, eventId);
                await request.ExecuteAsync();

                _logger.LogInformation(
                    "Cancelled Google Calendar event {EventId} for Clinic ID {ClinicId}",
                    eventId, clinicId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error cancelling Google Calendar event {EventId} for Clinic ID {ClinicId}",
                    eventId, clinicId);
                return false;
            }
        }

        public async Task<object?> GetAppointmentAsync(int clinicId, string eventId)
        {
            try
            {
                var service = await GetCalendarServiceAsync(clinicId);
                if (service == null)
                {
                    _logger.LogWarning("Cannot get appointment - Clinic ID {ClinicId} not connected to Google Calendar", clinicId);
                    return null;
                }

                var token = await _tokenRepository.GetActiveTokenByClinicIdAsync(clinicId);
                var calendarId = token?.CalendarId ?? "primary";

                var request = service.Events.Get(calendarId, eventId);
                var eventData = await request.ExecuteAsync();

                return new
                {
                    eventData.Id,
                    eventData.Summary,
                    eventData.Description,
                    Start = eventData.Start?.DateTimeDateTimeOffset, // ✅ Fixed
                    End = eventData.End?.DateTimeDateTimeOffset, // ✅ Fixed
                    eventData.Status,
                    eventData.HtmlLink
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error getting Google Calendar event {EventId} for Clinic ID {ClinicId}",
                    eventId, clinicId);
                return null;
            }
        }

        #region Private Helper Methods

        /// <summary>
        /// Get authenticated Google Calendar service for a clinic
        /// ✅ FIXED: Using modern UserCredential approach instead of obsolete GoogleCredential
        /// </summary>
        private async Task<CalendarService?> GetCalendarServiceAsync(int clinicId)
        {
            try
            {
                var storedToken = await _tokenRepository.GetActiveTokenByClinicIdAsync(clinicId);
                if (storedToken == null)
                {
                    _logger.LogWarning("No stored token for Clinic ID {ClinicId}", clinicId);
                    return null;
                }

                var accessToken = await _googleAuthService.GetValidAccessTokenAsync(clinicId);
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogWarning("No valid access token for Clinic ID {ClinicId}", clinicId);
                    return null;
                }

                // DECRYPT REFRESH TOKEN
                var decryptedRefreshToken = DecryptToken(storedToken.RefreshToken);

                // ⭐ FIX: Check if refresh token is NULL/empty
                if (string.IsNullOrEmpty(decryptedRefreshToken))
                {
                    _logger.LogError("❌ CRITICAL: Refresh token is NULL/empty for Clinic ID {ClinicId}. User must reconnect calendar!", clinicId);
                    return null;
                }

                _logger.LogInformation("✅ RefreshToken exists for Clinic {ClinicId} (length: {Length})", clinicId, decryptedRefreshToken.Length);

                // BUILD FLOW WITH CLIENT SECRETS
                var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
                {
                    ClientSecrets = new ClientSecrets
                    {
                        ClientId = _configuration["GoogleOAuth:ClientId"],
                        ClientSecret = _configuration["GoogleOAuth:ClientSecret"]
                    },
                    Scopes = new[] { CalendarService.Scope.Calendar }
                });

                // BUILD TOKEN RESPONSE
                var token = new Google.Apis.Auth.OAuth2.Responses.TokenResponse
                {
                    AccessToken = accessToken,
                    RefreshToken = decryptedRefreshToken, // ✅ This must NOT be null
                    ExpiresInSeconds = (long)(storedToken.TokenExpiry - DateTime.UtcNow).TotalSeconds,
                    IssuedUtc = DateTime.UtcNow.AddHours(-1),
                    TokenType = "Bearer",
                    Scope = storedToken.Scope
                };

                // CREATE CREDENTIAL
                var credential = new UserCredential(flow, clinicId.ToString(), token);

                var service = new CalendarService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "HFiles Clinic Portal"
                });

                return service;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error creating Calendar service for Clinic ID {ClinicId}", clinicId);
                return null;
            }
        }

        private string DecryptToken(string cipherText)
        {
            var encryptionKey = _configuration["Security:EncryptionKey"]
                ?? throw new InvalidOperationException("Encryption key not configured");

            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(encryptionKey.PadRight(32)[..32]);
            aes.IV = new byte[16]; // Use same IV as encryption

            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream(Convert.FromBase64String(cipherText));
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);
            return sr.ReadToEnd();
        }

        public async Task<string?> GetCalendarEmbedUrlAsync(int clinicId)
        {
            try
            {
                var token = await _tokenRepository.GetActiveTokenByClinicIdAsync(clinicId);
                if (token == null || !token.IsActive)
                {
                    _logger.LogWarning("No active Google Calendar connection for Clinic ID {ClinicId}", clinicId);
                    return null;
                }

                var calendarId = token.CalendarId ?? "primary";

                if (calendarId == "primary")
                {
                    var service = await GetCalendarServiceAsync(clinicId);
                    if (service == null)
                    {
                        _logger.LogWarning("Cannot get calendar service for Clinic ID {ClinicId}", clinicId);
                        return null;
                    }

                    var calendarListRequest = service.CalendarList.List();
                    var calendarList = await calendarListRequest.ExecuteAsync();

                    var primaryCalendar = calendarList.Items.FirstOrDefault(c => c.Primary == true);
                    if (primaryCalendar != null)
                    {
                        calendarId = primaryCalendar.Id;
                    }
                }

                var encodedCalendarId = Uri.EscapeDataString(calendarId);
                var embedUrl = $"https://calendar.google.com/calendar/embed?src={encodedCalendarId}&ctz=Asia/Kolkata";

                _logger.LogInformation("Generated calendar embed URL for Clinic ID {ClinicId}", clinicId);

                return embedUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating calendar embed URL for Clinic ID {ClinicId}", clinicId);
                return null;
            }
        }

        #endregion
    }
}