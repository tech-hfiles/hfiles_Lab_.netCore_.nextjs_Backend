using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
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

                var appointmentDateTime = appointmentDate.Date.Add(appointmentTime);
                var appointmentEndTime = appointmentDateTime.AddMinutes(30); // Default 30-minute slots

                var newEvent = new Event
                {
                    Summary = $"Appointment: {patientName}",
                    Description = $"Clinic: {clinicName}\nPatient: {patientName}\nPhone: {phoneNumber}",
                    Start = new EventDateTime
                    {
                        DateTime = appointmentDateTime,
                        TimeZone = "Asia/Kolkata" // Use appropriate timezone
                    },
                    End = new EventDateTime
                    {
                        DateTime = appointmentEndTime,
                        TimeZone = "Asia/Kolkata"
                    },
                    ColorId = "9", // Blue color
                    Reminders = new Event.RemindersData
                    {
                        UseDefault = false,
                        Overrides = new[]
                        {
                            new EventReminder { Method = "email", Minutes = 24 * 60 }, // 1 day before
                            new EventReminder { Method = "popup", Minutes = 30 }       // 30 mins before
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

                // Get existing event
                var existingEvent = await service.Events.Get(calendarId, eventId).ExecuteAsync();
                if (existingEvent == null)
                {
                    _logger.LogWarning("Event {EventId} not found", eventId);
                    return false;
                }

                var appointmentDateTime = appointmentDate.Date.Add(appointmentTime);
                var appointmentEndTime = appointmentDateTime.AddMinutes(30);

                // Update event details
                existingEvent.Summary = $"Appointment: {patientName}";
                existingEvent.Description = $"Clinic: {clinicName}\nPatient: {patientName}\nPhone: {phoneNumber}";
                existingEvent.Start = new EventDateTime
                {
                    DateTime = appointmentDateTime,
                    TimeZone = "Asia/Kolkata"
                };
                existingEvent.End = new EventDateTime
                {
                    DateTime = appointmentEndTime,
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

                // Get existing event
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
                    Start = eventData.Start?.DateTime,
                    End = eventData.End?.DateTime,
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
        /// </summary>
        private async Task<CalendarService?> GetCalendarServiceAsync(int clinicId)
        {
            try
            {
                // Get valid access token (auto-refreshes if needed)
                var accessToken = await _googleAuthService.GetValidAccessTokenAsync(clinicId);
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogWarning("No valid access token for Clinic ID {ClinicId}", clinicId);
                    return null;
                }

                // Create credential from access token
                var credential = GoogleCredential.FromAccessToken(accessToken);

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

                // Get the calendar ID (usually "primary" for the user's main calendar)
                var calendarId = token.CalendarId ?? "primary";

                // If it's the primary calendar, we need to get the actual email
                if (calendarId == "primary")
                {
                    // Get the calendar service to fetch calendar details
                    var service = await GetCalendarServiceAsync(clinicId);
                    if (service == null)
                    {
                        _logger.LogWarning("Cannot get calendar service for Clinic ID {ClinicId}", clinicId);
                        return null;
                    }

                    // Get calendar list to find the primary calendar email
                    var calendarListRequest = service.CalendarList.List();
                    var calendarList = await calendarListRequest.ExecuteAsync();

                    var primaryCalendar = calendarList.Items.FirstOrDefault(c => c.Primary == true);
                    if (primaryCalendar != null)
                    {
                        calendarId = primaryCalendar.Id;
                    }
                }

                // URL encode the calendar ID
                var encodedCalendarId = Uri.EscapeDataString(calendarId);

                // Generate embeddable Google Calendar URL
                // This URL can be used in an iframe or opened directly
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
