using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using HFiles_Backend.Domain.Interfaces;

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
                // Get the stored token
                var storedToken = await _tokenRepository.GetActiveTokenByClinicIdAsync(clinicId);
                if (storedToken == null)
                {
                    _logger.LogWarning("No stored token for Clinic ID {ClinicId}", clinicId);
                    return null;
                }

                // Get valid access token (auto-refreshes if needed)
                var accessToken = await _googleAuthService.GetValidAccessTokenAsync(clinicId);
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogWarning("No valid access token for Clinic ID {ClinicId}", clinicId);
                    return null;
                }

                // ✅ FIXED: Create UserCredential with proper token response
                var tokenResponse = new Google.Apis.Auth.OAuth2.Responses.TokenResponse
                {
                    AccessToken = accessToken,
                    RefreshToken = storedToken.RefreshToken,
                    ExpiresInSeconds = (long)(storedToken.TokenExpiry - DateTime.UtcNow).TotalSeconds,
                    TokenType = "Bearer",
                    Scope = storedToken.Scope
                };

                var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
                {
                    ClientSecrets = new ClientSecrets
                    {
                        ClientId = _configuration["GoogleOAuth:ClientId"],
                        ClientSecret = _configuration["GoogleOAuth:ClientSecret"]
                    },
                    Scopes = new[] { CalendarService.Scope.Calendar }
                });

                var credential = new UserCredential(flow, clinicId.ToString(), tokenResponse);

                // Create Calendar service
                var service = new CalendarService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "HFiles Clinic Portal"
                });

                return service;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Calendar service for Clinic ID {ClinicId}", clinicId);
                return null;
            }
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