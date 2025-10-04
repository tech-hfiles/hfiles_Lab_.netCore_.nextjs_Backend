using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Services;
using HFiles_Backend.API.Interfaces;
using HFiles_Backend.API.Services;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Clinics.Appointment;
using HFiles_Backend.Application.DTOs.Clinics.ConsentForm;
using HFiles_Backend.Application.DTOs.Clinics.Treatment;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Enums;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;
using Newtonsoft.Json;
using System.Globalization;

namespace HFiles_Backend.API.Controllers.Clinics
{
    [ApiController]
    [Route("api/appointments")]
    public class AppointmentsController(
    IAppointmentRepository appointmentRepository,
    IClinicAuthorizationService clinicAuthorizationService,
    IUserRepository userRepository,
    IClinicRepository clinicRepository,
    ILogger<AppointmentsController> logger,
    IClinicVisitRepository clinicVisitRepository,
    IClinicPatientRecordRepository clinicPatientRecordRepository,
     IConfiguration configuration,
     IEmailTemplateService emailTemplateService,
     EmailService emailService,
     IGoogleCalendarService googleCalendarService
    ) : ControllerBase
    {
        private readonly IAppointmentRepository _appointmentRepository = appointmentRepository;
        private readonly IClinicAuthorizationService _clinicAuthorizationService = clinicAuthorizationService;
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IClinicRepository _clinicRepository = clinicRepository;
        private readonly ILogger<AppointmentsController> _logger = logger;
        private readonly IClinicVisitRepository _clinicVisitRepository = clinicVisitRepository;
        private readonly IClinicPatientRecordRepository _clinicPatientRecordRepository = clinicPatientRecordRepository;
        private readonly IConfiguration _configuration = configuration;
        private readonly IEmailTemplateService _emailTemplateService = emailTemplateService;
        private readonly EmailService _emailService = emailService;
        private readonly IGoogleCalendarService _googleCalendarService = googleCalendarService;



        private string GetBaseUrl()
        {
            var environment = _configuration["Environment"] ?? "Development";
            return environment.Equals("Production", StringComparison.OrdinalIgnoreCase)
                ? "https://hfiles.in"
                : "http://localhost:3000";
        }



        private string UrlEncodeForConsentForm(string consentFormName)
        {
            // Custom URL encoding to match the required format:
            // Spaces should be %20 (not +)
            // Forward slash should be %2F (uppercase F)
            return consentFormName
                .Replace(" ", "%20")
                .Replace("/", "%2F");
        }




        // Helper method to determine the correct consent form URL
        private string DetermineConsentFormUrl(string consentFormName)
        {
            var formNameLower = consentFormName.ToLower();

            if (formNameLower.Contains("dtr"))
            {
                return "PublicDTRConsentForm";
            }
            else if (formNameLower.Contains("tmd") || formNameLower.Contains("tmjp"))
            {
                return "PublicTMDConsentForm";
            }
            else if (formNameLower.Contains("photo"))
            {
                return "publicPhotographyReleaseForm";
            }
            else if (formNameLower.Contains("arthrose") && formNameLower.Contains("functional") && formNameLower.Contains("screening"))
            {
                return "publicFunctionalScreeningForm";
            }
            else
            {
                // Default fallback
                return "PublicTMDConsentForm";
            }
        }




        // Helper method to create notification message
        private string CreateNotificationMessage(string patientName, DateTime date, TimeSpan time, List<ConsentFormLinkInfo> consentFormLinks)
        {
            var baseMessage = $"Follow-up appointment booked for {patientName} on {date:dd-MM-yyyy} at {time:hh\\:mm}.";

            if (consentFormLinks.Any())
            {
                var formsText = consentFormLinks.Count == 1 ? "consent form" : "consent forms";
                var formNames = string.Join(", ", consentFormLinks.Select(f => f.ConsentFormName));
                return $"{baseMessage} {consentFormLinks.Count} {formsText} sent via email: {formNames}";
            }

            return baseMessage;
        }





        // Create an appointment
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateAppointment([FromBody] AppointmentCreateDto dto)
        {
            HttpContext.Items["Log-Category"] = "Clinic Appointment";

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                _logger.LogWarning("Validation failed for appointment creation. Errors: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(dto.ClinicId, User);
            if (!isAuthorized)
            {
                _logger.LogWarning("Unauthorized appointment creation attempt for Clinic ID {ClinicId}", dto.ClinicId);
                return Unauthorized(ApiResponseFactory.Fail("Only main or branch clinics can create appointments."));
            }

            if (!DateTime.TryParseExact(dto.AppointmentDate, "dd-MM-yyyy", null, System.Globalization.DateTimeStyles.None, out var date))
            {
                _logger.LogWarning("Invalid AppointmentDate format: {Date}", dto.AppointmentDate);
                return BadRequest(ApiResponseFactory.Fail("Invalid AppointmentDate format. Expected dd-MM-yyyy."));
            }

            if (!TimeSpan.TryParse(dto.AppointmentTime, out var time))
            {
                _logger.LogWarning("Invalid AppointmentTime format: {Time}", dto.AppointmentTime);
                return BadRequest(ApiResponseFactory.Fail("Invalid AppointmentTime format. Expected HH:mm."));
            }

            var appointment = new ClinicAppointment
            {
                ClinicId = dto.ClinicId,
                VisitorUsername = dto.VisitorUsername,
                VisitorPhoneNumber = dto.VisitorPhoneNumber,
                AppointmentDate = date.Date,
                AppointmentTime = time,
                Status = "Scheduled"
            };

            var transaction = await _clinicRepository.BeginTransactionAsync();
            var committed = false;

            try
            {
                await _appointmentRepository.SaveAppointmentAsync(appointment);
                await _clinicRepository.SaveChangesAsync();

                // Get clinic name
                var clinic = await _clinicRepository.GetClinicByIdAsync(dto.ClinicId);

                // Create Google Calendar Event
                var googleEventId = await _googleCalendarService.CreateAppointmentAsync(
                    dto.ClinicId,  // Pass clinic ID
                    dto.VisitorUsername,
                    clinic?.ClinicName ?? "Clinic",
                    date.Date,
                    time,
                    dto.VisitorPhoneNumber
                );

                if (!string.IsNullOrEmpty(googleEventId))
                {
                    appointment.GoogleCalendarEventId = googleEventId;
                    await _clinicRepository.SaveChangesAsync();
                }

                await transaction.CommitAsync();
                committed = true;

                // Response + Notification
                var response = new
                {
                    appointment.Id,
                    appointment.ClinicId,
                    appointment.VisitorUsername,
                    appointment.VisitorPhoneNumber,
                    AppointmentDate = appointment.AppointmentDate.ToString("dd-MM-yyyy"),
                    AppointmentTime = appointment.AppointmentTime.ToString(@"hh\:mm"),
                    appointment.Status,

                    NotificationContext = new
                    {
                        AppointmentId = appointment.Id,
                        PatientName = appointment.VisitorUsername,
                        PhoneNumber = appointment.VisitorPhoneNumber,
                        AppointmentDate = appointment.AppointmentDate.ToString("dd-MM-yyyy"),
                        AppointmentTime = appointment.AppointmentTime.ToString(@"hh\:mm"),
                        Status = appointment.Status
                    },
                    NotificationMessage = $"Appointment booked for {appointment.VisitorUsername} on {appointment.AppointmentDate:dd-MM-yyyy} at {appointment.AppointmentTime:hh\\:mm}."
                };

                _logger.LogInformation("Appointment created for Clinic ID {ClinicId} on {Date} at {Time}", dto.ClinicId, date, time);
                return Ok(ApiResponseFactory.Success(response, "Appointment saved successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating appointment for Clinic ID {ClinicId}", dto.ClinicId);
                return StatusCode(500, ApiResponseFactory.Fail("Unexpected error occurred while saving the appointment."));
            }
            finally
            {
                if (!committed && transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.RollbackAsync();
                }
            }
        }





        // Get Appointments
        [HttpGet("clinic/{clinicId:int}")]
        [Authorize]
        public async Task<IActionResult> GetAppointmentsByClinicId(
        [FromRoute] int clinicId,
        [FromQuery] string? startDate,
        [FromQuery] string? endDate)
        {
            HttpContext.Items["Log-Category"] = "Clinic Appointment";

            if (!await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User))
            {
                _logger.LogWarning("Unauthorized access attempt for Clinic ID {ClinicId}", clinicId);
                return Unauthorized(ApiResponseFactory.Fail("You are not authorized to view appointments for this clinic."));
            }

            var appointments = await _appointmentRepository.GetAppointmentsByClinicIdAsync(clinicId);
            var visits = await _clinicVisitRepository.GetVisitsByClinicIdAsync(clinicId);
            var records = await _clinicPatientRecordRepository.GetTreatmentRecordsByClinicIdAsync(clinicId);

            if (appointments == null || !appointments.Any())
            {
                _logger.LogInformation("No appointments found for Clinic ID {ClinicId}", clinicId);
                return Ok(ApiResponseFactory.Success(new
                {
                    Appointments = new List<object>(),
                    TotalAppointmentsToday = 0,
                    MissedAppointmentsToday = 0,
                    DailyCounts = new List<object>()
                }, "No appointments found."));
            }

            // Parse date filters
            DateTime start;
            DateTime end;

            if (!string.IsNullOrEmpty(startDate))
            {
                if (!DateTime.TryParseExact(startDate, "dd-MM-yyyy", null, DateTimeStyles.None, out var parsed))
                    return BadRequest(ApiResponseFactory.Fail("Invalid startDate format. Expected dd-MM-yyyy."));
                start = parsed;
            }
            else
            {
                start = DateTime.Today;
            }

            if (!string.IsNullOrEmpty(endDate))
            {
                if (!DateTime.TryParseExact(endDate, "dd-MM-yyyy", null, DateTimeStyles.None, out var parsed))
                    return BadRequest(ApiResponseFactory.Fail("Invalid endDate format. Expected dd-MM-yyyy."));
                end = parsed;
            }
            else
            {
                end = DateTime.Today;
            }

            // Filter appointments by date range
            var filteredAppointments = appointments
                .Where(a => a.AppointmentDate.Date >= start.Date && a.AppointmentDate.Date <= end.Date)
                .ToList();

            var today = DateTime.Today;

            // Build lookup from visit to HFID
            var visitHfidLookup = visits
                .Where(v => v.Patient != null)
                .ToLookup(v => new
                {
                    v.ClinicId,
                    v.AppointmentDate.Date,
                    v.AppointmentTime
                }, v => v.Patient.HFID);

            // Extract unique HFIDs
            var uniqueHfids = visitHfidLookup.SelectMany(g => g).Where(h => !string.IsNullOrWhiteSpace(h)).Distinct().ToList();

            // Batch fetch users
            var userMap = new Dictionary<string, string>();
            foreach (var hfid in uniqueHfids)
            {
                var user = await _userRepository.GetUserByHFIDAsync(hfid);
                userMap[hfid] = user?.ProfilePhoto ?? "Not a registered user";
            }

            // Build response
            var response = filteredAppointments.Select(a =>
            {
                var hfid = visitHfidLookup[new
                {
                    a.ClinicId,
                    a.AppointmentDate.Date,
                    a.AppointmentTime
                }].FirstOrDefault();

                var profilePhoto = !string.IsNullOrEmpty(hfid) && userMap.TryGetValue(hfid, out var photo)
                    ? photo
                    : "Not a registered user";

                // Try to find matching treatment record
                var matchingRecord = records.FirstOrDefault(r =>
                    r.ClinicId == a.ClinicId &&
                    r.ClinicVisitId == visits.FirstOrDefault(v =>
                        v.ClinicId == a.ClinicId &&
                        v.AppointmentDate.Date == a.AppointmentDate.Date &&
                        v.AppointmentTime == a.AppointmentTime)?.Id &&
                    r.Type == RecordType.Treatment);

                string treatmentName = a.Treatment ?? "-";

                if (matchingRecord != null)
                {
                    try
                    {
                        var json = JsonConvert.DeserializeObject<dynamic>(matchingRecord.JsonData);
                        var treatments = json?.treatments;
                        if (treatments != null)
                        {
                            var names = new List<string>();
                            foreach (var t in treatments)
                            {
                                if (t.name != null)
                                    names.Add((string)t.name);
                            }

                            if (names.Any())
                                treatmentName = string.Join(", ", names);
                        }
                    }
                    catch
                    {
                        _logger.LogWarning("Failed to parse treatment JSON for record ID {RecordId}", matchingRecord.Id);
                    }
                }

                return new
                {
                    a.Id,
                    a.ClinicId,
                    a.VisitorUsername,
                    a.VisitorPhoneNumber,
                    AppointmentDate = a.AppointmentDate.ToString("dd-MM-yyyy"),
                    AppointmentTime = a.AppointmentTime.ToString(@"hh\:mm"),
                    Treatment = treatmentName,
                    a.Status,
                    HFID = hfid ?? "Not a registered user",
                    ProfilePhoto = profilePhoto
                };
            }).ToList();

            // Group all appointments by date (not filtered)
            var dailyCounts = appointments
                .GroupBy(a => a.AppointmentDate.Date)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    Date = g.Key.ToString("dd-MM-yyyy"),
                    TotalAppointments = g.Count()
                })
                .ToList();

            int totalAppointmentsToday = filteredAppointments.Count(a => a.AppointmentDate.Date == today);
            int missedAppointmentsToday = filteredAppointments.Count(a => a.AppointmentDate.Date == today && a.Status == "Absent");

            _logger.LogInformation("Fetched {Count} appointments for Clinic ID {ClinicId}", response.Count, clinicId);

            return Ok(ApiResponseFactory.Success(new
            {
                Appointments = response,
                TotalAppointmentsToday = totalAppointmentsToday,
                MissedAppointmentsToday = missedAppointmentsToday,
                DailyCounts = dailyCounts
            }, "Appointments fetched successfully."));
        }






        // Update Appointments
        [HttpPut("clinic/{clinicId:int}/appointment/{appointmentId:int}/status")]
        [Authorize]
        public async Task<IActionResult> UpdateAppointmentStatus(
      [FromRoute] int clinicId,
      [FromRoute] int appointmentId,
      [FromBody] AppointmentStatusUpdateDto dto)
        {
            HttpContext.Items["Log-Category"] = "Clinic Appointment";

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed for status update. Errors: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
            if (!isAuthorized)
            {
                _logger.LogWarning("Unauthorized status update attempt for Clinic ID {ClinicId}", clinicId);
                return Unauthorized(ApiResponseFactory.Fail("Only main or branch clinics can update appointments."));
            }

            var transaction = await _clinicRepository.BeginTransactionAsync();
            var committed = false;

            try
            {
                var appointment = await _appointmentRepository.GetAppointmentByIdAsync(appointmentId, clinicId);
                if (appointment == null)
                {
                    _logger.LogWarning("Appointment not found for ID {AppointmentId} in Clinic ID {ClinicId}",
                        appointmentId, clinicId);
                    return NotFound(ApiResponseFactory.Fail("Appointment not found."));
                }

                // NEW: Handle HFID linking if provided
                ClinicPatient? clinicPatient = null;
                ClinicVisit? createdVisit = null;

                if (!string.IsNullOrWhiteSpace(dto.HFID))
                {
                    // Step 1: Get user by HFID
                    var user = await _userRepository.GetUserByHFIDAsync(dto.HFID);
                    if (user == null)
                    {
                        _logger.LogWarning("No user found for HFID {HFID}", dto.HFID);
                        return NotFound(ApiResponseFactory.Fail($"No user found for HFID: {dto.HFID}"));
                    }

                    // Step 2: Get or create clinic patient
                    var fullName = $"{user.FirstName} {user.LastName}".Trim();
                    clinicPatient = await _clinicVisitRepository.GetOrCreatePatientAsync(dto.HFID, fullName);

                    // Step 3: Update appointment with user details
                    appointment.VisitorUsername = fullName;
                    appointment.VisitorPhoneNumber = user.PhoneNumber ?? "N/A";

                    // Step 4: Check if visit already exists for this appointment date/time
                    var existingVisit = await _clinicVisitRepository.GetExistingVisitAsyncWithTime(
                        clinicPatient.Id,
                        appointment.AppointmentDate,
                        appointment.AppointmentTime);

                    if (existingVisit == null)
                    {
                        // Step 5: Create clinic visit
                        createdVisit = new ClinicVisit
                        {
                            ClinicPatientId = clinicPatient.Id,
                            ClinicId = clinicId,
                            AppointmentDate = appointment.AppointmentDate,
                            AppointmentTime = appointment.AppointmentTime,
                            PaymentMethod = null
                        };

                        await _clinicVisitRepository.SaveVisitAsync(createdVisit);

                        _logger.LogInformation(
                            "Created clinic visit for Patient {PatientName} (HFID: {HFID}) in Clinic {ClinicId}",
                            fullName, dto.HFID, clinicId);
                    }
                    else
                    {
                        createdVisit = existingVisit;
                        _logger.LogInformation(
                            "Using existing visit ID {VisitId} for Patient {PatientName}",
                            existingVisit.Id, fullName);
                    }
                }

                // Update status logic (existing code)
                if (!string.IsNullOrWhiteSpace(dto.Status))
                {
                    appointment.Status = dto.Status;

                    if (dto.Status == "Canceled" && !string.IsNullOrEmpty(appointment.GoogleCalendarEventId))
                    {
                        await _googleCalendarService.CancelAppointmentAsync(
                            appointment.ClinicId,
                            appointment.GoogleCalendarEventId);
                    }
                }

                var now = DateTime.Now;
                var appointmentDateTime = appointment.AppointmentDate.Date + appointment.AppointmentTime;

                if (dto.Status == "Canceled")
                {
                    if (appointmentDateTime <= now)
                        return BadRequest(ApiResponseFactory.Fail(
                            "Cannot cancel past or ongoing appointments."));
                }
                else if (dto.Status == "Completed")
                {
                    if (appointment.AppointmentDate.Date != now.Date || appointmentDateTime > now)
                        return BadRequest(ApiResponseFactory.Fail(
                            "Can only mark as completed if appointment is today and time has passed."));

                    appointment.Treatment = dto.Treatment;
                }

                // Save appointment changes
                await _appointmentRepository.UpdateAsync(appointment);
                await _clinicRepository.SaveChangesAsync();
                await transaction.CommitAsync();
                committed = true;

                // Build response
                var response = new
                {
                    appointment.Id,
                    appointment.ClinicId,
                    appointment.VisitorUsername,
                    appointment.VisitorPhoneNumber,
                    AppointmentDate = appointment.AppointmentDate.ToString("dd-MM-yyyy"),
                    AppointmentTime = appointment.AppointmentTime.ToString(@"hh\:mm"),
                    appointment.Status,
                    appointment.Treatment,

                    // NEW: Include patient linking info if HFID was provided
                    PatientLinkInfo = !string.IsNullOrWhiteSpace(dto.HFID) ? new
                    {
                        HFID = dto.HFID,
                        ClinicPatientId = clinicPatient?.Id,
                        ClinicVisitId = createdVisit?.Id,
                        PatientName = clinicPatient?.PatientName,
                        LinkedSuccessfully = true
                    } : null,

                    NotificationContext = new
                    {
                        PatientName = appointment.VisitorUsername,
                        PhoneNumber = appointment.VisitorPhoneNumber,
                        Date = appointment.AppointmentDate.ToString("dd-MM-yyyy"),
                        Time = appointment.AppointmentTime.ToString(@"hh\:mm"),
                        ClinicId = appointment.ClinicId,
                        NewStatus = appointment.Status,
                        TreatmentDetails = appointment.Treatment,
                        HFID = dto.HFID,
                        WasLinkedToPatient = !string.IsNullOrWhiteSpace(dto.HFID)
                    },

                    NotificationMessage = $"Appointment for {appointment.VisitorUsername} on {appointment.AppointmentDate:dd-MM-yyyy} at {appointment.AppointmentTime:hh\\:mm} has been updated to '{appointment.Status}'" +
                        $"{(appointment.Status == "Completed" && !string.IsNullOrWhiteSpace(appointment.Treatment) ? $" with treatment noted as: {appointment.Treatment}." : ".")}" +
                        $"{(!string.IsNullOrWhiteSpace(dto.HFID) ? $" Patient linked with HFID: {dto.HFID}." : "")}"
                };

                _logger.LogInformation(
                    "Appointment ID {AppointmentId} status updated to {Status}{HfidInfo}",
                    appointmentId,
                    dto.Status,
                    !string.IsNullOrWhiteSpace(dto.HFID) ? $" and linked to HFID {dto.HFID}" : "");

                return Ok(ApiResponseFactory.Success(response, "Appointment status updated successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while updating appointment status for ID {AppointmentId}", appointmentId);
                return StatusCode(500, ApiResponseFactory.Fail(
                    "Unexpected error occurred while updating appointment status."));
            }
            finally
            {
                if (!committed && transaction.GetDbTransaction().Connection != null)
                    await transaction.RollbackAsync();
            }
        }





        // Delete Appointment based on appointment Id
        [HttpDelete("{appointmentId:int}")]
        [Authorize]
        public async Task<IActionResult> DeleteAppointmentById([FromRoute] int appointmentId)
        {
            HttpContext.Items["Log-Category"] = "Clinic Appointment";

            var transaction = await _clinicRepository.BeginTransactionAsync();
            var committed = false;

            try
            {
                var appointment = await _appointmentRepository.GetByIdAsync(appointmentId);
                if (appointment == null)
                {
                    _logger.LogWarning("Appointment ID {AppointmentId} not found", appointmentId);
                    return NotFound(ApiResponseFactory.Fail("Appointment not found."));
                }

                bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(appointment.ClinicId, User);
                if (!isAuthorized)
                {
                    _logger.LogWarning("Unauthorized delete attempt for appointment ID {AppointmentId} in Clinic ID {ClinicId}", appointmentId, appointment.ClinicId);
                    return Unauthorized(ApiResponseFactory.Fail("You are not authorized to delete appointments for this clinic."));
                }

                // Delete from Google Calendar
                if (!string.IsNullOrEmpty(appointment.GoogleCalendarEventId))
                {
                    await _googleCalendarService.DeleteAppointmentAsync(appointment.ClinicId, appointment.GoogleCalendarEventId);
                }

                await _appointmentRepository.DeleteAsync(appointment);
                await _clinicRepository.SaveChangesAsync();
                await transaction.CommitAsync();
                committed = true;

                // Response + Notification
                var response = new
                {
                    appointment.Id,
                    appointment.ClinicId,
                    appointment.VisitorUsername,
                    appointment.VisitorPhoneNumber,
                    AppointmentDate = appointment.AppointmentDate.ToString("dd-MM-yyyy"),
                    AppointmentTime = appointment.AppointmentTime.ToString(@"hh\:mm"),
                    appointment.Status,

                    // Notification section
                    NotificationContext = new
                    {
                        AppointmentId = appointment.Id,
                        PatientName = appointment.VisitorUsername,
                        PhoneNumber = appointment.VisitorPhoneNumber,
                        AppointmentDate = appointment.AppointmentDate.ToString("dd-MM-yyyy"),
                        AppointmentTime = appointment.AppointmentTime.ToString(@"hh\:mm"),
                        Status = "Deleted"
                    },
                    NotificationMessage = $"Appointment for {appointment.VisitorUsername} on {appointment.AppointmentDate:dd-MM-yyyy} at {appointment.AppointmentTime:hh\\:mm} has been deleted."
                };

                _logger.LogInformation("Deleted appointment ID {AppointmentId} from Clinic ID {ClinicId}", appointmentId, appointment.ClinicId);
                return Ok(ApiResponseFactory.Success(response, "Appointment deleted successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while deleting appointment ID {AppointmentId}", appointmentId);
                return StatusCode(500, ApiResponseFactory.Fail("Unexpected error occurred while deleting the appointment."));
            }
            finally
            {
                if (!committed && transaction?.GetDbTransaction()?.Connection != null)
                {
                    await transaction.RollbackAsync();
                    _logger.LogWarning("Transaction rolled back for appointment deletion ID {AppointmentId}", appointmentId);
                }
            }
        }





        // Add new patient API
        // Updated Add new patient API with enhanced email service
        [HttpPost("clinics/{clinicId}/follow-up")]
        [Authorize]
        public async Task<IActionResult> CreateFollowUpAppointment(
         [FromBody] FollowUpAppointmentDto dto,
         [FromRoute] int clinicId)
        {
            HttpContext.Items["Log-Category"] = "Follow-up Appointment";

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            if (!DateTime.TryParseExact(dto.AppointmentDate, "dd-MM-yyyy", null, DateTimeStyles.None, out var date))
                return BadRequest(ApiResponseFactory.Fail("Invalid AppointmentDate format. Expected dd-MM-yyyy."));

            if (!TimeSpan.TryParse(dto.AppointmentTime, out var time))
                return BadRequest(ApiResponseFactory.Fail("Invalid AppointmentTime format. Expected HH:mm."));

            var transaction = await _clinicRepository.BeginTransactionAsync();
            var committed = false;

            try
            {
                bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
                if (!isAuthorized)
                {
                    _logger.LogWarning("Unauthorized appointment creation attempt for Clinic ID {ClinicId}", clinicId);
                    return Unauthorized(ApiResponseFactory.Fail("Only main or branch clinics can create appointments."));
                }

                var clinicExists = await _clinicRepository.ExistsAsync(clinicId);
                if (!clinicExists)
                {
                    _logger.LogWarning("Clinic ID {ClinicId} does not exist in clinicsignups", clinicId);
                    return BadRequest(ApiResponseFactory.Fail("Invalid Clinic ID."));
                }

                // Check if patient already has a visit in this clinic
                bool hasVisit = await _clinicVisitRepository.HasVisitInClinicAsync(dto.HFID, clinicId);
                if (hasVisit)
                {
                    _logger.LogWarning("Patient with HFID {HFID} already has a visit in Clinic ID {ClinicId}", dto.HFID, clinicId);
                    return BadRequest(ApiResponseFactory.Fail("This patient already exists. Please book a follow-up appointment instead."));
                }

                var user = await _userRepository.GetUserByHFIDAsync(dto.HFID);
                if (user == null)
                {
                    _logger.LogWarning("No user found for HFID {HFID}", dto.HFID);
                    return NotFound(ApiResponseFactory.Fail("No user found for provided HFID."));
                }

                if (user.FirstName == null)
                {
                    _logger.LogWarning("No FirstName found for HFID {HFID}", dto.HFID);
                    return NotFound(ApiResponseFactory.Fail("No FirstName found for provided HFID."));
                }

                var fullName = $"{user.FirstName} {user.LastName}";
                var phone = user.PhoneNumber ?? "N/A";

                var patient = await _clinicVisitRepository.GetOrCreatePatientAsync(dto.HFID, fullName);

                var consentForms = await _clinicVisitRepository.GetConsentFormsByTitlesAsync(dto.ConsentFormTitles);
                if (consentForms.Count != dto.ConsentFormTitles.Count)
                {
                    var missing = dto.ConsentFormTitles.Except(consentForms.Select(f => f.Title)).ToList();
                    return BadRequest(ApiResponseFactory.Fail($"Invalid consent form titles: {string.Join(", ", missing)}"));
                }

                var visit = new ClinicVisit
                {
                    ClinicPatientId = patient.Id,
                    ClinicId = clinicId,
                    AppointmentDate = date.Date,
                    AppointmentTime = time,
                    ConsentFormsSent = consentForms.Select(f => new ClinicVisitConsentForm
                    {
                        ConsentFormId = f.Id
                    }).ToList()
                };
                await _clinicVisitRepository.SaveVisitAsync(visit);

                var appointment = new ClinicAppointment
                {
                    VisitorUsername = fullName,
                    VisitorPhoneNumber = phone,
                    AppointmentDate = date.Date,
                    AppointmentTime = time,
                    ClinicId = clinicId,
                    Status = "Scheduled"
                };
                await _appointmentRepository.SaveAppointmentAsync(appointment);

                // Create Google Calendar Event for follow-up appointment
                var clinic = await _clinicRepository.GetClinicByIdAsync(clinicId);
                var googleEventId = await _googleCalendarService.CreateAppointmentAsync(
                    clinicId,
                    fullName,
                    clinic?.ClinicName ?? "Clinic",
                    date.Date,
                    time,
                    phone
                );

                if (!string.IsNullOrEmpty(googleEventId))
                {
                    appointment.GoogleCalendarEventId = googleEventId;
                    // Just save changes to update the existing record
                    await _clinicRepository.SaveChangesAsync();
                    _logger.LogInformation("Google Calendar event created for follow-up appointment. EventId: {EventId}", googleEventId);
                }

                await transaction.CommitAsync();
                committed = true;

                // Generate consent form links and send email
                var consentFormLinks = new List<ConsentFormLinkInfo>();
                //var clinic = await _clinicRepository.GetClinicByIdAsync(clinicId);

                if (visit.ConsentFormsSent.Any())
                {
                    var baseUrl = GetBaseUrl();

                    for (int i = 0; i < visit.ConsentFormsSent.Count; i++)
                    {
                        var consentFormEntry = visit.ConsentFormsSent.ElementAt(i);
                        var consentFormTitle = dto.ConsentFormTitles[i];
                        var encodedConsentName = UrlEncodeForConsentForm(consentFormTitle);

                        // Determine the correct form URL based on consent form name
                        string formUrl = DetermineConsentFormUrl(consentFormTitle);
                        var consentFormLink = $"{baseUrl}/{formUrl}?ConsentId={consentFormEntry.Id}&ConsentName={encodedConsentName}&hfid={patient.HFID}";

                        consentFormLinks.Add(new ConsentFormLinkInfo
                        {
                            ConsentFormId = consentFormEntry.Id,
                            ConsentFormName = consentFormTitle,
                            ConsentFormLink = consentFormLink
                        });
                    }

                    // Send email with consent form links using the appointment confirmation template
                    if (consentFormLinks.Any())
                    {
                        try
                        {
                            var emailTemplate = _emailTemplateService.GenerateAppointmentConfirmationWithConsentFormsEmailTemplate(
                                user.FirstName,
                                consentFormLinks,
                                clinic?.ClinicName ?? "Clinic",
                                date.ToString("dd-MM-yyyy"),
                                time.ToString(@"hh\:mm")
                            );

                            var consentFormNames = string.Join(", ", consentFormLinks.Select(f => f.ConsentFormName));
                            await _emailService.SendEmailAsync(
                                user.Email,
                                $"Appointment Confirmation & Consent Forms - {clinic?.ClinicName}",
                                emailTemplate
                            );

                            _logger.LogInformation("Appointment confirmation email sent successfully to {Email} with {Count} consent forms",
                                user.Email, consentFormLinks.Count);
                        }
                        catch (Exception emailEx)
                        {
                            _logger.LogError(emailEx, "Failed to send appointment confirmation email to {Email} for appointment {AppointmentId}",
                                user.Email, appointment.Id);
                            // Don't fail the entire operation if email fails
                        }
                    }
                }

                // Response + Notification
                var response = new
                {
                    PatientName = patient.PatientName,
                    HFID = patient.HFID,
                    AppointmentDate = date.ToString("dd-MM-yyyy"),
                    AppointmentTime = time.ToString(@"hh\:mm"),
                    ConsentFormsSent = consentForms.Select(f => f.Title).ToList(),
                    ConsentFormLinks = consentFormLinks.Select(link => new
                    {
                        ConsentFormId = link.ConsentFormId,
                        ConsentFormName = link.ConsentFormName,
                        ConsentFormLink = link.ConsentFormLink
                    }).ToList(),
                    Treatment = appointment.Treatment,
                    AppointmentStatus = appointment.Status,
                    ClinicId = clinicId,
                    EmailSent = consentFormLinks.Any(),
                    SentToEmail = user.Email,
                    ClinicName = clinic?.ClinicName,

                    // Enhanced notification section
                    NotificationContext = new
                    {
                        AppointmentId = appointment.Id,
                        PatientName = patient.PatientName,
                        HFID = patient.HFID,
                        PhoneNumber = phone,
                        AppointmentDate = date.ToString("dd-MM-yyyy"),
                        AppointmentTime = time.ToString(@"hh\:mm"),
                        Status = "Scheduled",
                        ConsentFormsCount = consentFormLinks.Count,
                        ConsentFormNames = string.Join(", ", consentFormLinks.Select(f => f.ConsentFormName)),
                        ClinicName = clinic?.ClinicName,
                        EmailStatus = consentFormLinks.Any() ? "Sent" : "No forms to send"
                    },
                    NotificationMessage = CreateNotificationMessage(patient.PatientName, date, time, consentFormLinks)
                };

                _logger.LogInformation("Follow-up appointment created for HFID {HFID} and ClinicId {ClinicId}. Consent forms sent: {ConsentFormsCount}",
                    dto.HFID, clinicId, consentFormLinks.Count);

                return Ok(ApiResponseFactory.Success(response, "Appointment saved successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating follow-up appointment for HFID {HFID}", dto.HFID);
                return StatusCode(500, ApiResponseFactory.Fail("Unexpected error occurred while saving the follow-up appointment."));
            }
            finally
            {
                if (!committed && transaction.GetDbTransaction().Connection != null)
                    await transaction.RollbackAsync();
            }
        }





        // Check existing patient and book follow up appointment
        [HttpPost("clinics/{clinicId}/appointments/follow-up")]
        [Authorize]
        public async Task<IActionResult> BookFollowUpAppointmentForExistingPatient(
            [FromBody] FollowUpAppointmentDto dto,
            [FromRoute] int clinicId)
        {
            HttpContext.Items["Log-Category"] = "Follow-up Appointment (Existing Patient)";

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            if (!DateTime.TryParseExact(dto.AppointmentDate, "dd-MM-yyyy", null, DateTimeStyles.None, out var date))
                return BadRequest(ApiResponseFactory.Fail("Invalid AppointmentDate format. Expected dd-MM-yyyy."));

            if (!TimeSpan.TryParse(dto.AppointmentTime, out var time))
                return BadRequest(ApiResponseFactory.Fail("Invalid AppointmentTime format. Expected HH:mm."));

            try
            {
                bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
                if (!isAuthorized)
                {
                    _logger.LogWarning("Unauthorized appointment booking attempt for Clinic ID {ClinicId}", clinicId);
                    return Unauthorized(ApiResponseFactory.Fail("Only main or branch clinics can book appointments."));
                }

                var clinicExists = await _clinicRepository.ExistsAsync(clinicId);
                if (!clinicExists)
                {
                    _logger.LogWarning("Clinic ID {ClinicId} does not exist", clinicId);
                    return BadRequest(ApiResponseFactory.Fail("Invalid Clinic ID."));
                }

                // Validate patient existence in this clinic
                bool hasVisit = await _clinicVisitRepository.HasVisitInClinicAsync(dto.HFID, clinicId);
                if (!hasVisit)
                {
                    _logger.LogWarning("Patient with HFID {HFID} not found in Clinic ID {ClinicId}", dto.HFID, clinicId);
                    return BadRequest(ApiResponseFactory.Fail("Patient not found in this clinic. Please add the patient first."));
                }

                var user = await _userRepository.GetUserByHFIDAsync(dto.HFID);
                if (user == null)
                {
                    _logger.LogWarning("No user found for HFID {HFID}", dto.HFID);
                    return NotFound(ApiResponseFactory.Fail("No user found for provided HFID."));
                }

                if (user.FirstName == null)
                {
                    _logger.LogWarning("No FirstName found for HFID {HFID}", dto.HFID);
                    return NotFound(ApiResponseFactory.Fail("No FirstName found for provided HFID."));
                }

                var fullName = $"{user.FirstName} {user.LastName}";
                var phone = user.PhoneNumber ?? "N/A";

                var patient = await _clinicVisitRepository.GetOrCreatePatientAsync(dto.HFID, fullName);

                var consentForms = await _clinicVisitRepository.GetConsentFormsByTitlesAsync(dto.ConsentFormTitles);
                if (consentForms.Count != dto.ConsentFormTitles.Count)
                {
                    var missing = dto.ConsentFormTitles.Except(consentForms.Select(f => f.Title)).ToList();
                    return BadRequest(ApiResponseFactory.Fail($"Invalid consent form titles: {string.Join(", ", missing)}"));
                }

                var visit = new ClinicVisit
                {
                    ClinicPatientId = patient.Id,
                    ClinicId = clinicId,
                    AppointmentDate = date.Date,
                    AppointmentTime = time,
                    ConsentFormsSent = consentForms.Select(f => new ClinicVisitConsentForm
                    {
                        ConsentFormId = f.Id
                    }).ToList()
                };
                await _clinicVisitRepository.SaveVisitAsync(visit);

                var appointment = new ClinicAppointment
                {
                    VisitorUsername = fullName,
                    VisitorPhoneNumber = phone,
                    AppointmentDate = date.Date,
                    AppointmentTime = time,
                    ClinicId = clinicId,
                    Status = "Scheduled"
                };
                await _appointmentRepository.SaveAppointmentAsync(appointment);

                // Create Google Calendar Event for follow-up appointment
                var clinic = await _clinicRepository.GetClinicByIdAsync(clinicId);
                var googleEventId = await _googleCalendarService.CreateAppointmentAsync(
                    clinicId,
                    fullName,
                    clinic?.ClinicName ?? "Clinic",
                    date.Date,
                    time,
                    phone
                );

                if (!string.IsNullOrEmpty(googleEventId))
                {
                    appointment.GoogleCalendarEventId = googleEventId;
                    // Just save changes to update the existing record
                    await _clinicRepository.SaveChangesAsync();
                    _logger.LogInformation("Google Calendar event created for follow-up appointment. EventId: {EventId}", googleEventId);
                }

                // Generate consent form links and send email
                var consentFormLinks = new List<ConsentFormLinkInfo>();
                //var clinic = await _clinicRepository.GetClinicByIdAsync(clinicId);

                if (visit.ConsentFormsSent.Any())
                {
                    var baseUrl = GetBaseUrl();

                    for (int i = 0; i < visit.ConsentFormsSent.Count; i++)
                    {
                        var consentFormEntry = visit.ConsentFormsSent.ElementAt(i);
                        var consentFormTitle = dto.ConsentFormTitles[i];
                        var encodedConsentName = UrlEncodeForConsentForm(consentFormTitle);

                        // Determine the correct form URL based on consent form name
                        string formUrl = DetermineConsentFormUrl(consentFormTitle);
                        var consentFormLink = $"{baseUrl}/{formUrl}?ConsentId={consentFormEntry.Id}&ConsentName={encodedConsentName}&hfid={patient.HFID}";

                        consentFormLinks.Add(new ConsentFormLinkInfo
                        {
                            ConsentFormId = consentFormEntry.Id,
                            ConsentFormName = consentFormTitle,
                            ConsentFormLink = consentFormLink
                        });
                    }

                    // Send email with consent form links using the appointment confirmation template
                    if (consentFormLinks.Any())
                    {
                        try
                        {
                            var emailTemplate = _emailTemplateService.GenerateAppointmentConfirmationWithConsentFormsEmailTemplate(
                                user.FirstName,
                                consentFormLinks,
                                clinic?.ClinicName ?? "Clinic",
                                date.ToString("dd-MM-yyyy"),
                                time.ToString(@"hh\:mm")
                            );

                            var consentFormNames = string.Join(", ", consentFormLinks.Select(f => f.ConsentFormName));
                            await _emailService.SendEmailAsync(
                                user.Email,
                                $"Appointment Confirmation & Consent Forms - {clinic?.ClinicName}",
                                emailTemplate
                            );

                            _logger.LogInformation("Appointment confirmation email sent successfully to {Email} with {Count} consent forms",
                                user.Email, consentFormLinks.Count);
                        }
                        catch (Exception emailEx)
                        {
                            _logger.LogError(emailEx, "Failed to send appointment confirmation email to {Email} for appointment {AppointmentId}",
                                user.Email, appointment.Id);
                            // Don't fail the entire operation if email fails
                        }
                    }
                }

                // Response + Notification
                var response = new
                {
                    HFID = dto.HFID,
                    PatientName = fullName,
                    AppointmentDate = date.ToString("dd-MM-yyyy"),
                    AppointmentTime = time.ToString(@"hh\:mm"),
                    ConsentFormsSent = consentForms.Select(f => f.Title).ToList(),
                    ConsentFormLinks = consentFormLinks.Select(link => new
                    {
                        ConsentFormId = link.ConsentFormId,
                        ConsentFormName = link.ConsentFormName,
                        ConsentFormLink = link.ConsentFormLink
                    }).ToList(),
                    Treatment = appointment.Treatment,
                    AppointmentStatus = appointment.Status,
                    ClinicId = clinicId,
                    EmailSent = consentFormLinks.Any(),
                    SentToEmail = user.Email,
                    ClinicName = clinic?.ClinicName,

                    // Enhanced notification section
                    NotificationContext = new
                    {
                        AppointmentId = appointment.Id,
                        PatientName = fullName,
                        HFID = dto.HFID,
                        PhoneNumber = phone,
                        AppointmentDate = date.ToString("dd-MM-yyyy"),
                        AppointmentTime = time.ToString(@"hh\:mm"),
                        Status = "Scheduled",
                        ConsentFormsCount = consentFormLinks.Count,
                        ConsentFormNames = string.Join(", ", consentFormLinks.Select(f => f.ConsentFormName)),
                        ClinicName = clinic?.ClinicName,
                        EmailStatus = consentFormLinks.Any() ? "Sent" : "No forms to send"
                    },
                    NotificationMessage = CreateNotificationMessage(fullName, date, time, consentFormLinks)
                };

                _logger.LogInformation("Follow-up appointment booked for existing patient HFID {HFID} in Clinic ID {ClinicId}. Consent forms sent: {ConsentFormsCount}",
                    dto.HFID, clinicId, consentFormLinks.Count);

                return Ok(ApiResponseFactory.Success(response, "Follow-up appointment booked successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while booking follow-up appointment for HFID {HFID}", dto.HFID);
                return StatusCode(500, ApiResponseFactory.Fail("Unexpected error occurred while booking the follow-up appointment."));
            }
        }





        // Fetch CLinic Patients
        [HttpGet("clinics/{clinicId}/patients")]
        [Authorize]
        public async Task<IActionResult> GetClinicPatients(
         [FromRoute] int clinicId,
         [FromQuery] string? startDate,
         [FromQuery] string? endDate,
         [FromServices] ClinicRepository clinicRepository,
         [FromServices] ClinicPatientRecordRepository recordRepository,
         [FromServices] IUserRepository userRepository)
        {
            HttpContext.Items["Log-Category"] = "Clinic Patient Overview";

            if (!await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User))
            {
                _logger.LogWarning("Unauthorized appointment creation attempt for Clinic ID {ClinicId}", clinicId);
                return Unauthorized(ApiResponseFactory.Fail("Only main or branch clinics can create appointments."));
            }

            try
            {
                DateTime? start = null;
                DateTime? end = null;

                if (!string.IsNullOrEmpty(startDate))
                {
                    if (!DateTime.TryParseExact(startDate, "dd-MM-yyyy", null, DateTimeStyles.None, out var parsed))
                        return BadRequest(ApiResponseFactory.Fail("Invalid startDate format. Expected dd-MM-yyyy."));
                    start = parsed;
                }

                if (!string.IsNullOrEmpty(endDate))
                {
                    if (!DateTime.TryParseExact(endDate, "dd-MM-yyyy", null, DateTimeStyles.None, out var parsed))
                        return BadRequest(ApiResponseFactory.Fail("Invalid endDate format. Expected dd-MM-yyyy."));
                    end = parsed;
                }


                var patients = await clinicRepository.GetClinicPatientsWithVisitsAsync(clinicId);

                // Build HFID → ProfilePhoto map
                var uniqueHfids = patients
                    .Where(p => !string.IsNullOrWhiteSpace(p.HFID))
                    .Select(p => p.HFID)
                    .Distinct()
                    .ToList();

                var userMap = new Dictionary<string, string>();
                foreach (var hfid in uniqueHfids)
                {
                    var user = await userRepository.GetUserByHFIDAsync(hfid);
                    userMap[hfid] = user?.ProfilePhoto ?? "Not a registered user";
                }

                var filteredPatients = new List<PatientDto>();

                foreach (var patient in patients)
                {
                    var lastVisit = patient.Visits.OrderByDescending(v => v.AppointmentDate).FirstOrDefault();
                    if (lastVisit == null) continue;

                    if ((start.HasValue && lastVisit.AppointmentDate.Date < start.Value.Date) ||
                         (end.HasValue && lastVisit.AppointmentDate.Date > end.Value.Date))
                        continue;


                    var treatmentRecords = await recordRepository.GetTreatmentRecordsAsync(clinicId, patient.Id, lastVisit.Id);

                    var treatmentNames = treatmentRecords
                        .SelectMany(r =>
                        {
                            try
                            {
                                var payload = JsonConvert.DeserializeObject<TreatmentRecordPayload>(r.JsonData);
                                return payload?.Treatments.Select(t => t.Name) ?? Enumerable.Empty<string>();
                            }
                            catch
                            {
                                _logger.LogWarning("Failed to parse treatment JSON for PatientId={PatientId}, VisitId={VisitId}", patient.Id, lastVisit.Id);
                                return Enumerable.Empty<string>();
                            }
                        })
                        .Distinct()
                        .ToList();

                    var profilePhoto = !string.IsNullOrEmpty(patient.HFID) && userMap.TryGetValue(patient.HFID, out var photo)
                        ? photo
                        : "Not a registered user";

                    var dto = new PatientDto
                    {
                        PatientId = patient.Id,
                        PatientName = patient.PatientName,
                        HFID = patient.HFID,
                        ProfilePhoto = profilePhoto,
                        LastVisitDate = lastVisit.AppointmentDate.ToString("dd-MM-yyyy"),
                        PaymentStatus = Enum.GetName(typeof(PaymentMethod), lastVisit?.PaymentMethod ?? default) ?? "Pending",
                                    
                        TreatmentNames = treatmentNames.Any()
                                         ? string.Join(", ", treatmentNames)
                                         : "-",
                        Visits = patient.Visits
                            .Select(v => new VisitDto
                            {
                                VisitId = v.Id,
                                AppointmentDate = v.AppointmentDate.ToString("dd-MM-yyyy"),
                                AppointmentTime = v.AppointmentTime.ToString(@"hh\:mm"),
                                ConsentFormsSent = v.ConsentFormsSent.Select(cf => cf.ConsentForm.Title).ToList()
                            })
                            .OrderByDescending(v => v.AppointmentDate)
                            .ToList()
                    };

                    filteredPatients.Add(dto);
                }

                var response = new ClinicPatientResponseDto
                {
                    TotalPatients = filteredPatients.Count,
                    Patients = filteredPatients
                };

                return Ok(ApiResponseFactory.Success(response, "Filtered clinic patient data retrieved successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while fetching patient data for Clinic ID {ClinicId}", clinicId);
                return StatusCode(500, ApiResponseFactory.Fail("Unexpected error occurred while retrieving patient data."));
            }
        }




        // Calendar View
        [HttpGet("clinic/{clinicId}/calendar-url")]
        [Authorize]
        public async Task<IActionResult> GetClinicCalendarUrl([FromRoute] int clinicId)
        {
            HttpContext.Items["Log-Category"] = "Google Calendar URL";

            bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
            if (!isAuthorized)
            {
                return Unauthorized(ApiResponseFactory.Fail("Not authorized to access this clinic's calendar."));
            }

            try
            {
                var calendarUrl = await _googleCalendarService.GetCalendarEmbedUrlAsync(clinicId);

                if (string.IsNullOrEmpty(calendarUrl))
                {
                    return NotFound(ApiResponseFactory.Fail("Google Calendar not configured for this clinic."));
                }

                var response = new
                {
                    CalendarUrl = calendarUrl,
                    Message = "Open this URL to view your clinic's appointments in Google Calendar"
                };

                return Ok(ApiResponseFactory.Success(response, "Calendar URL retrieved successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving calendar URL for Clinic {ClinicId}", clinicId);
                return StatusCode(500, ApiResponseFactory.Fail("Failed to retrieve calendar URL."));
            }
        }





        [HttpPost("clinics/{clinicId}/setup-calendar-sharing")]
        [Authorize]
        public async Task<IActionResult> SetupCalendarSharing([FromRoute] int clinicId)
        {
            HttpContext.Items["Log-Category"] = "Calendar Sharing Setup";

            try
            {
                var clinic = await _clinicRepository.GetClinicByIdAsync(clinicId);
                if (clinic == null || string.IsNullOrEmpty(clinic.GoogleCalendarId))
                    return BadRequest(ApiResponseFactory.Fail("Calendar not configured"));

                if (string.IsNullOrEmpty(clinic.GoogleCredentialsPath) || !System.IO.File.Exists(clinic.GoogleCredentialsPath))
                    return BadRequest(ApiResponseFactory.Fail("Credentials file not found"));

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

                // Make calendar publicly readable
                var publicAcl = new Google.Apis.Calendar.v3.Data.AclRule
                {
                    Scope = new Google.Apis.Calendar.v3.Data.AclRule.ScopeData
                    {
                        Type = "default"
                    },
                    Role = "reader"
                };

                try
                {
                    await calendarService.Acl.Insert(publicAcl, clinic.GoogleCalendarId).ExecuteAsync();
                    _logger.LogInformation("Calendar sharing configured for Clinic {ClinicId}", clinicId);
                }
                catch (Exception ex)
                {
                    // Might already be public
                    _logger.LogWarning(ex, "Calendar might already be public for Clinic {ClinicId}", clinicId);
                }

                return Ok(ApiResponseFactory.Success("Calendar sharing configured successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting up calendar sharing for Clinic {ClinicId}", clinicId);
                return StatusCode(500, ApiResponseFactory.Fail(ex.Message));
            }
        }



/* ***************************************************************************************** */
        ////ARTHROSE APPOINTMENTS API


        ///// <summary>
        ///// Imports appointments from Excel file and creates appointment entries.
        ///// Excel structure: A=patientName, B=patientId, C=date, D=status, E=explanation
        ///// Maps patients to existing users or creates appointments with patient names
        ///// </summary>
        ///// <param name="request">The Excel file containing appointment data</param>
        ///// <returns>Summary of import results including added and skipped appointments</returns>
        //[HttpPost("import-excel")]
        //public async Task<IActionResult> ImportAppointmentsFromExcel([FromForm] AppointmentImportRequest request)
        //{
        //    HttpContext.Items["Log-Category"] = "Appointment Import";

        //    _logger.LogInformation("Starting appointment import from Excel file");

        //    // Validate file upload
        //    if (request.ExcelFile == null || request.ExcelFile.Length == 0)
        //        return BadRequest(ApiResponseFactory.Fail("Excel file is required."));

        //    if (!Path.GetExtension(request.ExcelFile.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        //        return BadRequest(ApiResponseFactory.Fail("Only .xlsx files are supported."));

        //    var response = new AppointmentImportResponse();
        //    var appointmentsToAdd = new List<ClinicAppointment>();
        //    var skippedReasons = new List<string>();

        //    try
        //    {
        //        using var stream = new MemoryStream();
        //        await request.ExcelFile.CopyToAsync(stream);

        //        ExcelPackage.License.SetNonCommercialPersonal("Ayush");
        //        using var package = new ExcelPackage(stream);
        //        var worksheet = package.Workbook.Worksheets[0];

        //        if (worksheet.Dimension == null)
        //            return BadRequest(ApiResponseFactory.Fail("Excel file is empty."));

        //        var rowCount = worksheet.Dimension.End.Row;
        //        _logger.LogInformation("Processing {RowCount} rows from Excel", rowCount - 1);

        //        // Process each row (skip header row)
        //        for (int row = 2; row <= rowCount; row++)
        //        {
        //            try
        //            {
        //                // Extract data from Excel row
        //                var appointmentData = ExtractAppointmentDataFromRow(worksheet, row);
        //                response.TotalProcessed++;

        //                // Validate required fields
        //                var validationResult = ValidateAppointmentData(appointmentData, row);
        //                if (!validationResult.IsValid)
        //                {
        //                    skippedReasons.Add($"Row {row}: {validationResult.ErrorMessage}");
        //                    response.Skipped++;
        //                    continue;
        //                }

        //                // Parse date and time from the complex date string
        //                var dateTimeResult = ParseDateTimeString(appointmentData.DateString);
        //                if (!dateTimeResult.IsValid)
        //                {
        //                    skippedReasons.Add($"Row {row}: {dateTimeResult.ErrorMessage}");
        //                    response.Skipped++;
        //                    continue;
        //                }

        //                // Create appointment entity
        //                var appointment = await CreateAppointmentFromData(appointmentData, dateTimeResult);
        //                appointmentsToAdd.Add(appointment);

        //                // Track statistics
        //                if (appointment.VisitorPhoneNumber != "N/A")
        //                    response.PatientsFound++;
        //                else
        //                    response.PatientsNotFound++;

        //            }
        //            catch (Exception ex)
        //            {
        //                _logger.LogError(ex, "Error processing row {Row}", row);
        //                skippedReasons.Add($"Row {row}: Processing error - {ex.Message}");
        //                response.Skipped++;
        //            }
        //        }

        //        // Bulk insert valid appointments
        //        if (appointmentsToAdd.Any())
        //        {
        //            await _appointmentRepository.AddRangeAsync(appointmentsToAdd);
        //            await _appointmentRepository.SaveChangesAsync();

        //            response.SuccessfullyAdded = appointmentsToAdd.Count;
        //            response.AddedAppointments = appointmentsToAdd.Select(a => new AppointmentSummary
        //            {
        //                PatientName = a.VisitorUsername,
        //                PatientId = GetPatientIdFromAppointment(a),
        //                VisitorPhoneNumber = a.VisitorPhoneNumber,
        //                AppointmentDate = a.AppointmentDate.ToString("dd-MM-yyyy"),
        //                AppointmentTime = a.AppointmentTime.ToString(@"hh\:mm"),
        //                Status = a.Status,
        //                Treatment = a.Treatment ?? "",
        //                PatientFoundInUsers = a.VisitorPhoneNumber != "N/A"
        //            }).ToList();
        //        }

        //        response.SkippedReasons = skippedReasons;
        //        response.Message = $"Import completed: {response.SuccessfullyAdded} added, {response.Skipped} skipped out of {response.TotalProcessed} total appointments. " +
        //                          $"Found {response.PatientsFound} existing patients, {response.PatientsNotFound} new patients.";

        //        _logger.LogInformation("Appointment import completed: {Added} added, {Skipped} skipped",
        //            response.SuccessfullyAdded, response.Skipped);

        //        return Ok(ApiResponseFactory.Success(response, response.Message));
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Failed to import appointments from Excel");
        //        return StatusCode(500, ApiResponseFactory.Fail("Failed to process Excel file: " + ex.Message));
        //    }
        //}

        //private ExcelAppointmentRow ExtractAppointmentDataFromRow(ExcelWorksheet worksheet, int row)
        //{
        //    return new ExcelAppointmentRow
        //    {
        //        PatientName = worksheet.Cells[row, 1].Text.Trim(),
        //        PatientId = worksheet.Cells[row, 2].Text.Trim(),
        //        DateString = worksheet.Cells[row, 3].Text.Trim(),
        //        Status = worksheet.Cells[row, 4].Text.Trim(),
        //        Explanation = worksheet.Cells[row, 5].Text.Trim()
        //    };
        //}

        //private (bool IsValid, string ErrorMessage) ValidateAppointmentData(ExcelAppointmentRow data, int row)
        //{
        //    if (string.IsNullOrWhiteSpace(data.PatientName))
        //        return (false, "Patient name is required");

        //    if (string.IsNullOrWhiteSpace(data.PatientId))
        //        return (false, "Patient ID is required");

        //    if (string.IsNullOrWhiteSpace(data.DateString))
        //        return (false, "Date string is required");

        //    if (string.IsNullOrWhiteSpace(data.Status))
        //        return (false, "Status is required");

        //    if (!data.Status.Equals("CONFIRM", StringComparison.OrdinalIgnoreCase) &&
        //        !data.Status.Equals("CANCEL", StringComparison.OrdinalIgnoreCase))
        //        return (false, $"Invalid status '{data.Status}'. Expected CONFIRM or CANCEL");

        //    return (true, "");
        //}

        //private (bool IsValid, DateTime Date, TimeSpan Time, string ErrorMessage) ParseDateTimeString(string dateString)
        //{
        //    try
        //    {
        //        // Pattern: "Tue Apr 01 19:54:27 IST 2025"
        //        // Format: {weekday} {month} {dd} {hh:mm:ss} IST {yyyy}
        //        var regex = new Regex(@"^\w{3}\s+(\w{3})\s+(\d{1,2})\s+(\d{2}):(\d{2}):(\d{2})\s+IST\s+(\d{4})$");
        //        var match = regex.Match(dateString);

        //        if (!match.Success)
        //            return (false, default, default, $"Invalid date format: {dateString}");

        //        var monthStr = match.Groups[1].Value;
        //        var day = int.Parse(match.Groups[2].Value);
        //        var hour = int.Parse(match.Groups[3].Value);
        //        var minute = int.Parse(match.Groups[4].Value);
        //        var second = int.Parse(match.Groups[5].Value);
        //        var year = int.Parse(match.Groups[6].Value);

        //        // Convert month name to number
        //        var month = monthStr switch
        //        {
        //            "Jan" => 1,
        //            "Feb" => 2,
        //            "Mar" => 3,
        //            "Apr" => 4,
        //            "May" => 5,
        //            "Jun" => 6,
        //            "Jul" => 7,
        //            "Aug" => 8,
        //            "Sep" => 9,
        //            "Oct" => 10,
        //            "Nov" => 11,
        //            "Dec" => 12,
        //            _ => throw new ArgumentException($"Invalid month: {monthStr}")
        //        };

        //        var date = new DateTime(year, month, day);
        //        var time = new TimeSpan(hour, minute, second);

        //        return (true, date, time, "");
        //    }
        //    catch (Exception ex)
        //    {
        //        return (false, default, default, $"Error parsing date '{dateString}': {ex.Message}");
        //    }
        //}

        //private async Task<ClinicAppointment> CreateAppointmentFromData(
        //    ExcelAppointmentRow data,
        //    (bool IsValid, DateTime Date, TimeSpan Time, string ErrorMessage) dateTimeResult)
        //{
        //    // Try to find user by PatientId
        //    var user = await _userRepository.GetUserByPatientIdAsync(data.PatientId);

        //    string visitorUsername;
        //    string visitorPhoneNumber;

        //    if (user != null)
        //    {
        //        // Patient found in users table - use their data
        //        visitorUsername = $"{user.FirstName} {user.LastName}".Trim();
        //        visitorPhoneNumber = user.PhoneNumber;
        //    }
        //    else
        //    {
        //        // Patient not found - use Excel data
        //        visitorUsername = data.PatientName;
        //        visitorPhoneNumber = "N/A";
        //    }

        //    // Convert status: CONFIRM -> Completed, CANCEL -> Canceled
        //    string status = data.Status.Equals("CONFIRM", StringComparison.OrdinalIgnoreCase)
        //        ? "Completed"
        //        : "Canceled";

        //    var appointment = new ClinicAppointment
        //    {
        //        VisitorUsername = visitorUsername,
        //        VisitorPhoneNumber = visitorPhoneNumber,
        //        AppointmentDate = dateTimeResult.Date,
        //        AppointmentTime = dateTimeResult.Time,
        //        Treatment = data.Explanation,
        //        ClinicId = 8, // Fixed clinic ID as requested
        //        Status = status
        //    };

        //    return appointment;
        //}

        //private string GetPatientIdFromAppointment(ClinicAppointment appointment)
        //{
        //    // This is a helper method to extract patient ID for the response
        //    // You might want to store this temporarily or get it differently
        //    // For now, we'll return a placeholder since we don't store it in the appointment
        //    return "N/A"; // You could modify this logic based on your needs
        //}

        //// 7. Optional: Add a method to get import statistics
        ///// <summary>
        ///// Gets statistics about appointment imports for a specific clinic
        ///// </summary>
        ///// <param name="clinicId">The clinic ID to get statistics for</param>
        ///// <returns>Import statistics</returns>
        //[HttpGet("clinic/{clinicId:int}/import-stats")]
        //[Authorize]
        //public async Task<IActionResult> GetImportStats([FromRoute] int clinicId)
        //{
        //    HttpContext.Items["Log-Category"] = "Appointment Import Stats";

        //    if (!await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User))
        //    {
        //        _logger.LogWarning("Unauthorized access attempt for Clinic ID {ClinicId}", clinicId);
        //        return Unauthorized(ApiResponseFactory.Fail("You are not authorized to view statistics for this clinic."));
        //    }

        //    try
        //    {
        //        var appointments = await _appointmentRepository.GetAppointmentsByClinicIdAsync(clinicId);

        //        var stats = new
        //        {
        //            TotalAppointments = appointments.Count,
        //            CompletedAppointments = appointments.Count(a => a.Status == "Completed"),
        //            CanceledAppointments = appointments.Count(a => a.Status == "Canceled"),
        //            ScheduledAppointments = appointments.Count(a => a.Status == "Scheduled"),
        //            AppointmentsWithTreatment = appointments.Count(a => !string.IsNullOrWhiteSpace(a.Treatment)),
        //            AppointmentsWithPhoneNumber = appointments.Count(a => a.VisitorPhoneNumber != "N/A"),
        //            RecentImports = appointments
        //                .Where(a => a.AppointmentDate >= DateTime.Today.AddDays(-30))
        //                .Count()
        //        };

        //        _logger.LogInformation("Retrieved import statistics for Clinic ID {ClinicId}", clinicId);
        //        return Ok(ApiResponseFactory.Success(stats, "Import statistics retrieved successfully."));
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error retrieving import statistics for Clinic ID {ClinicId}", clinicId);
        //        return StatusCode(500, ApiResponseFactory.Fail("Failed to retrieve import statistics."));
        //    }
        //}
    }
}

