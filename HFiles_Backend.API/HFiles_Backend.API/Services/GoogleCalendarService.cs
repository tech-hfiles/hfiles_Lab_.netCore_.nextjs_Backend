using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using HFiles_Backend.API.Interfaces;
using HFiles_Backend.Domain.Interfaces;

namespace HFiles_Backend.API.Services
{
    public class GoogleCalendarService(
        ILogger<GoogleCalendarService> logger,
        IClinicRepository clinicRepository) : IGoogleCalendarService
    {
        private readonly ILogger<GoogleCalendarService> _logger = logger;
        private readonly IClinicRepository _clinicRepository = clinicRepository;
        private readonly Dictionary<int, CalendarService> _calendarServices = new();

        private async Task<CalendarService?> GetCalendarServiceForClinic(int clinicId)
        {
            // Return cached service if available
            if (_calendarServices.ContainsKey(clinicId))
                return _calendarServices[clinicId];

            try
            {
                var clinic = await _clinicRepository.GetClinicByIdAsync(clinicId);

                if (clinic == null || string.IsNullOrEmpty(clinic.GoogleCredentialsPath))
                {
                    _logger.LogWarning("Clinic {ClinicId} has no Google Calendar configured", clinicId);
                    return null;
                }

                // Check if credentials file exists
                if (!File.Exists(clinic.GoogleCredentialsPath))
                {
                    _logger.LogWarning("Credentials file not found for Clinic {ClinicId}: {Path}",
                        clinicId, clinic.GoogleCredentialsPath);
                    return null;
                }

                GoogleCredential credential;
                using (var stream = new FileStream(clinic.GoogleCredentialsPath, FileMode.Open, FileAccess.Read))
                {
                    credential = GoogleCredential.FromStream(stream)
                        .CreateScoped(CalendarService.Scope.Calendar);
                }

                var calendarService = new CalendarService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = $"HFiles Clinic - {clinic.ClinicName}",
                });

                // Cache the service
                _calendarServices[clinicId] = calendarService;

                _logger.LogInformation("Google Calendar Service initialized for Clinic {ClinicId}", clinicId);
                return calendarService;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Google Calendar Service for Clinic {ClinicId}", clinicId);
                return null;
            }
        }

        public async Task<string?> CreateAppointmentAsync(
            int clinicId,
            string patientName,
            string clinicName,
            DateTime appointmentDate,
            TimeSpan appointmentTime,
            string? phoneNumber = null,
            int durationMinutes = 30)
        {
            try
            {
                var calendarService = await GetCalendarServiceForClinic(clinicId);
                if (calendarService == null)
                    return null;

                var clinic = await _clinicRepository.GetClinicByIdAsync(clinicId);
                var calendarId = clinic?.GoogleCalendarId ?? "primary";

                var startDateTime = appointmentDate.Date.Add(appointmentTime);
                var endDateTime = startDateTime.AddMinutes(durationMinutes);
                var istOffset = TimeSpan.FromHours(5.5);

                var description = $"Patient: {patientName}";
                if (!string.IsNullOrEmpty(phoneNumber))
                    description += $"\nPhone: {phoneNumber}";

                var calendarEvent = new Event
                {
                    Summary = $"Appointment: {patientName}",
                    Location = clinicName,
                    Description = description,
                    Start = new EventDateTime
                    {
                        DateTimeDateTimeOffset = new DateTimeOffset(startDateTime, istOffset),
                        TimeZone = "Asia/Kolkata"
                    },
                    End = new EventDateTime
                    {
                        DateTimeDateTimeOffset = new DateTimeOffset(endDateTime, istOffset),
                        TimeZone = "Asia/Kolkata"
                    },
                    Reminders = new Event.RemindersData
                    {
                        UseDefault = false,
                        Overrides = new[]
                        {
                            new EventReminder { Method = "email", Minutes = 24 * 60 },
                            new EventReminder { Method = "popup", Minutes = 60 }
                        }
                    },
                    ColorId = "9"
                };

                var request = calendarService.Events.Insert(calendarEvent, calendarId);
                var createdEvent = await request.ExecuteAsync();

                _logger.LogInformation(
                    "Google Calendar event created for Clinic {ClinicId}. EventId: {EventId}, Patient: {PatientName}",
                    clinicId, createdEvent.Id, patientName);

                return createdEvent.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create Google Calendar event for Clinic {ClinicId}, Patient: {PatientName}",
                    clinicId, patientName);
                return null;
            }
        }

        public async Task<bool> UpdateAppointmentAsync(
            int clinicId,
            string eventId,
            DateTime newDate,
            TimeSpan newTime,
            int durationMinutes = 30)
        {
            try
            {
                var calendarService = await GetCalendarServiceForClinic(clinicId);
                if (calendarService == null)
                    return false;

                var clinic = await _clinicRepository.GetClinicByIdAsync(clinicId);
                var calendarId = clinic?.GoogleCalendarId ?? "primary";

                var existingEvent = await calendarService.Events
                    .Get(calendarId, eventId)
                    .ExecuteAsync();

                if (existingEvent == null)
                {
                    _logger.LogWarning("Event not found: {EventId} for Clinic {ClinicId}", eventId, clinicId);
                    return false;
                }

                var startDateTime = newDate.Date.Add(newTime);
                var endDateTime = startDateTime.AddMinutes(durationMinutes);
                var istOffset = TimeSpan.FromHours(5.5);

                existingEvent.Start = new EventDateTime
                {
                    DateTimeDateTimeOffset = new DateTimeOffset(startDateTime, istOffset),
                    TimeZone = "Asia/Kolkata"
                };

                existingEvent.End = new EventDateTime
                {
                    DateTimeDateTimeOffset = new DateTimeOffset(endDateTime, istOffset),
                    TimeZone = "Asia/Kolkata"
                };

                var request = calendarService.Events.Update(existingEvent, calendarId, eventId);
                await request.ExecuteAsync();

                _logger.LogInformation("Google Calendar event updated for Clinic {ClinicId}. EventId: {EventId}",
                    clinicId, eventId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update Google Calendar event for Clinic {ClinicId}: {EventId}",
                    clinicId, eventId);
                return false;
            }
        }

        public async Task<bool> CancelAppointmentAsync(int clinicId, string eventId)
        {
            try
            {
                var calendarService = await GetCalendarServiceForClinic(clinicId);
                if (calendarService == null)
                    return false;

                var clinic = await _clinicRepository.GetClinicByIdAsync(clinicId);
                var calendarId = clinic?.GoogleCalendarId ?? "primary";

                var existingEvent = await calendarService.Events
                    .Get(calendarId, eventId)
                    .ExecuteAsync();

                if (existingEvent == null)
                    return false;

                existingEvent.Status = "cancelled";
                existingEvent.Summary = $"[CANCELLED] {existingEvent.Summary}";

                var request = calendarService.Events.Update(existingEvent, calendarId, eventId);
                await request.ExecuteAsync();

                _logger.LogInformation("Google Calendar event cancelled for Clinic {ClinicId}. EventId: {EventId}",
                    clinicId, eventId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel Google Calendar event for Clinic {ClinicId}: {EventId}",
                    clinicId, eventId);
                return false;
            }
        }

        public async Task<bool> DeleteAppointmentAsync(int clinicId, string eventId)
        {
            try
            {
                var calendarService = await GetCalendarServiceForClinic(clinicId);
                if (calendarService == null)
                    return false;

                var clinic = await _clinicRepository.GetClinicByIdAsync(clinicId);
                var calendarId = clinic?.GoogleCalendarId ?? "primary";

                var request = calendarService.Events.Delete(calendarId, eventId);
                await request.ExecuteAsync();

                _logger.LogInformation("Google Calendar event deleted for Clinic {ClinicId}. EventId: {EventId}",
                    clinicId, eventId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete Google Calendar event for Clinic {ClinicId}: {EventId}",
                    clinicId, eventId);
                return false;
            }
        }

        public async Task<string?> GetCalendarEmbedUrlAsync(int clinicId)
        {
            try
            {
                var clinic = await _clinicRepository.GetClinicByIdAsync(clinicId);

                if (clinic == null || string.IsNullOrEmpty(clinic.GoogleCalendarId))
                    return null;

                // Generate URL that adds calendar to user's Google Calendar
                var encodedCalendarId = Uri.EscapeDataString(clinic.GoogleCalendarId);

                // This URL will prompt them to add the calendar to their account
                return $"https://calendar.google.com/calendar/u/0/r?cid={encodedCalendarId}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get calendar URL for Clinic {ClinicId}", clinicId);
                return null;
            }
        }
    }
}