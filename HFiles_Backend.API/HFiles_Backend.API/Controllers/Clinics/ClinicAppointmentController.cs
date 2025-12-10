using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Services;
using HFiles_Backend.API.DTOs.Clinics;
using HFiles_Backend.API.Interfaces;
using HFiles_Backend.API.Services;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Clinics.Appointment;
using HFiles_Backend.Application.DTOs.Clinics.ConsentForm;
using HFiles_Backend.Application.DTOs.Clinics.Treatment;
using HFiles_Backend.Application.Extensions;
using HFiles_Backend.Application.Models.Filters;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Entities.Users;
using HFiles_Backend.Domain.Enums;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.VisualBasic.FileIO;
using Newtonsoft.Json;
using System.Globalization;
using System.Text.RegularExpressions;

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
     IGoogleCalendarService googleCalendarService,
     IClinicStatisticsCacheService cacheService,
     IHfidService hfidService
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
        private readonly IClinicStatisticsCacheService _cacheService = cacheService;
        private readonly IHfidService _hfidService = hfidService;



        private string GetBaseUrl()
        {
            var environment = _configuration["Environment"] ?? "Development";
            return environment.Equals("Production", StringComparison.OrdinalIgnoreCase)
                ? "https://hfiles.co.in"
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

        // Constant for unlimited subscription
        private const long SUBSCRIPTION_UNLIMITED = 99999999999;




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

                // INVALIDATE CACHE AFTER SUCCESSFUL CREATION
                _cacheService.InvalidateClinicStatistics(dto.ClinicId);

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
            int completedAppointmentsToday = filteredAppointments.Count(a => a.AppointmentDate.Date == today && a.Status == "Completed");

            _logger.LogInformation("Fetched {Count} appointments for Clinic ID {ClinicId}", response.Count, clinicId);

            return Ok(ApiResponseFactory.Success(new
            {
                Appointments = response,
                TotalAppointmentsToday = totalAppointmentsToday,
                MissedAppointmentsToday = missedAppointmentsToday,
                CompletedAppointmentsToday = completedAppointmentsToday,
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

                if (!string.IsNullOrWhiteSpace(dto.HFID) && dto.HFID != "Not a registered user")
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

                // Update status logic
                if (!string.IsNullOrWhiteSpace(dto.Status))
                {
                    appointment.Status = dto.Status;

                    // Update Google Calendar for ALL status changes
                    if (!string.IsNullOrEmpty(appointment.GoogleCalendarEventId))
                    {
                        if (dto.Status == "Canceled")
                        {
                            // Cancel the event in Google Calendar
                            await _googleCalendarService.CancelAppointmentAsync(
                                appointment.ClinicId,
                                appointment.GoogleCalendarEventId);
                        }
                        else if (dto.Status == "Completed")
                        {
                            // Update the event to show it's completed
                            var clinic = await _clinicRepository.GetClinicByIdAsync(appointment.ClinicId);
                            await _googleCalendarService.UpdateAppointmentAsync(
                                appointment.ClinicId,
                                appointment.GoogleCalendarEventId,
                                appointment.VisitorUsername,
                                clinic?.ClinicName ?? "Clinic",
                                appointment.AppointmentDate,
                                appointment.AppointmentTime,
                                appointment.VisitorPhoneNumber
                            );
                        }
                        else if (!string.IsNullOrWhiteSpace(dto.HFID) && dto.HFID != "Not a registered user")
                        {
                            // Update event with new patient info when HFID is linked
                            var clinic = await _clinicRepository.GetClinicByIdAsync(appointment.ClinicId);
                            await _googleCalendarService.UpdateAppointmentAsync(
                                appointment.ClinicId,
                                appointment.GoogleCalendarEventId,
                                appointment.VisitorUsername,
                                clinic?.ClinicName ?? "Clinic",
                                appointment.AppointmentDate,
                                appointment.AppointmentTime,
                                appointment.VisitorPhoneNumber
                            );
                        }
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
                    // Calculate 2 hours before current time
                    var twoHoursAgo = now.AddHours(-2);

                    // Check if appointment is today
                    if (appointment.AppointmentDate.Date != now.Date)
                    {
                        return BadRequest(ApiResponseFactory.Fail(
                            "Can only mark as completed if appointment is today."));
                    }

                    // Check if appointment time is within the last 2 hours or has passed
                    //if (appointmentDateTime > now)
                    //{
                    //    return BadRequest(ApiResponseFactory.Fail(
                    //        "Cannot mark appointment as completed before it occurs."));
                    //}

                    // Check if appointment was more than 2 hours ago
                    if (appointmentDateTime < twoHoursAgo)
                    {
                        return BadRequest(ApiResponseFactory.Fail(
                            "Can only mark as completed within 2 hours of the appointment time."));
                    }

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

                // Check and delete corresponding ClinicVisit if exists
                try
                {
                    // Get patient from Patients table using phone number
                    var patient = await _userRepository.GetByPhoneNumberAsync(appointment.VisitorPhoneNumber);

                    if (patient != null && !string.IsNullOrEmpty(patient.HfId))
                    {
                        // Get ClinicPatient using HFID
                        var clinicPatient = await _clinicVisitRepository.GetPatientAsync(patient.HfId);

                        if (clinicPatient != null)
                        {
                            // Find matching visit with clinic ID confirmation
                            var visit = await _clinicVisitRepository.GetVisitByDetailsAsync(
                                clinicPatient.Id,
                                appointment.AppointmentDate,
                                appointment.AppointmentTime,
                                appointment.ClinicId
                            );

                            if (visit != null)
                            {
                                await _clinicVisitRepository.DeleteAsync(visit);
                                _logger.LogInformation("Deleted corresponding visit ID {VisitId} for appointment ID {AppointmentId} in Clinic ID {ClinicId}",
                                    visit.Id, appointmentId, appointment.ClinicId);
                            }
                            else
                            {
                                _logger.LogInformation("No matching visit found for appointment ID {AppointmentId} in Clinic ID {ClinicId}",
                                    appointmentId, appointment.ClinicId);
                            }
                        }
                    }
                    else
                    {
                        _logger.LogInformation("No patient with valid HFID found for phone number {PhoneNumber} for appointment ID {AppointmentId}",
                            appointment.VisitorPhoneNumber, appointmentId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while checking/deleting corresponding visit for appointment ID {AppointmentId}", appointmentId);
                    // Continue with appointment deletion even if visit deletion fails
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

                // INVALIDATE CACHE AFTER SUCCESSFUL CREATION
                _cacheService.InvalidateClinicStatistics(appointment.ClinicId);

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
        /// Creates a follow-up appointment. Supports both existing patients (via HFID) 
        /// and new patient registration (via patient details)
        [HttpPost("clinics/{clinicId}/follow-up")]
        [Authorize]
        public async Task<IActionResult> CreateFollowUpAppointment(
         [FromBody] FollowUpAppointmentDto dto,
         [FromRoute] int clinicId)
        {
            HttpContext.Items["Log-Category"] = "Clinic Appointment";

            // Basic model validation
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            // Custom validation - either HFID OR new patient details must be provided
            var isExistingPatient = !string.IsNullOrWhiteSpace(dto.HFID);
            var isNewPatient = !isExistingPatient;

            if (isNewPatient)
            {
                var validationErrors = new List<string>();

                if (string.IsNullOrWhiteSpace(dto.FirstName))
                    validationErrors.Add("First name is required when HFID is not provided.");

                if (string.IsNullOrWhiteSpace(dto.LastName))
                    validationErrors.Add("Last name is required when HFID is not provided.");

                if (string.IsNullOrWhiteSpace(dto.DOB))
                    validationErrors.Add("Date of birth is required when HFID is not provided.");

                if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
                    validationErrors.Add("Phone number is required when HFID is not provided.");

                if (string.IsNullOrWhiteSpace(dto.CountryCode))
                    validationErrors.Add("Country code is required when HFID is not provided.");

                if (validationErrors.Any())
                {
                    _logger.LogWarning("New patient validation failed: {@Errors}", validationErrors);
                    return BadRequest(ApiResponseFactory.Fail(validationErrors));
                }
            }

            // Validate appointment date format
            if (!DateTime.TryParseExact(dto.AppointmentDate, "dd-MM-yyyy", null, DateTimeStyles.None, out var appointmentDate))
            {
                _logger.LogWarning("Invalid appointment date format: {Date}", dto.AppointmentDate);
                return BadRequest(ApiResponseFactory.Fail("Invalid AppointmentDate format. Expected dd-MM-yyyy."));
            }

            // Validate appointment time format
            if (!TimeSpan.TryParse(dto.AppointmentTime, out var appointmentTime))
            {
                _logger.LogWarning("Invalid appointment time format: {Time}", dto.AppointmentTime);
                return BadRequest(ApiResponseFactory.Fail("Invalid AppointmentTime format. Expected HH:mm."));
            }

            // Begin atomic transaction
            await using var transaction = await _userRepository.BeginTransactionAsync();
            var committed = false;

            try
            {
                // Check clinic authorization
                bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
                if (!isAuthorized)
                {
                    _logger.LogWarning("Unauthorized appointment creation attempt for Clinic ID {ClinicId}", clinicId);
                    return Unauthorized(ApiResponseFactory.Fail("Only main or branch clinics can create appointments."));
                }

                // Verify clinic exists
                var clinicExists = await _userRepository.ExistsAsync(clinicId);
                if (!clinicExists)
                {
                    _logger.LogWarning("Clinic ID {ClinicId} does not exist", clinicId);
                    return BadRequest(ApiResponseFactory.Fail("Invalid Clinic ID."));
                }

                User? user;
                bool isPatientNewlyCreated = false;

                // ==================== FLOW 1: EXISTING PATIENT WITH HFID ====================
                if (isExistingPatient)
                {
                    _logger.LogInformation("Processing existing patient with HFID: {HFID}", dto.HFID);

                    // Use null-forgiving operator after validation
                    // Check if patient already has a visit in this clinic
                    bool hasVisit = await _clinicVisitRepository.HasVisitInClinicAsync(dto.HFID!, clinicId);
                    if (hasVisit)
                    {
                        _logger.LogWarning("Patient with HFID {HFID} already has a visit in Clinic ID {ClinicId}", dto.HFID, clinicId);
                        return BadRequest(ApiResponseFactory.Fail("This patient already has a visit in this clinic."));
                    }

                    // Retrieve user by HFID - we know dto.HFID is not null here
                    user = await _userRepository.GetUserByHFIDAsync(dto.HFID!);
                    if (user == null)
                    {
                        _logger.LogWarning("No user found for HFID {HFID}", dto.HFID);
                        return NotFound(ApiResponseFactory.Fail("No user found for provided HFID."));
                    }

                    if (string.IsNullOrWhiteSpace(user.FirstName))
                    {
                        _logger.LogWarning("User found but FirstName is missing for HFID {HFID}", dto.HFID);
                        return BadRequest(ApiResponseFactory.Fail("User data is incomplete. FirstName is required."));
                    }
                }
                // ==================== FLOW 2: NEW PATIENT CREATION ====================
                else
                {
                    _logger.LogInformation("Processing new patient registration: {FirstName} {LastName}", dto.FirstName, dto.LastName);

                    // Validate and parse DOB
                    if (!DateTime.TryParseExact(dto.DOB, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDob))
                    {
                        _logger.LogWarning("Invalid DOB format for new patient: {DOB}", dto.DOB);
                        return BadRequest(ApiResponseFactory.Fail("Invalid DOB format. Please use dd-MM-yyyy."));
                    }

                    // Calculate and validate age
                    var age = DateTime.Today.Year - parsedDob.Year;
                    if (parsedDob > DateTime.Today.AddYears(-age)) age--;
                    if (age < 0 || age > 150)
                    {
                        _logger.LogWarning("Unrealistic age calculated from DOB: {Age} years", age);
                        return BadRequest(ApiResponseFactory.Fail("Invalid date of birth. Age must be between 0 and 150."));
                    }

                    // Check if phone number already exists - we validated these are not null earlier
                    bool phoneExists = await _userRepository.IsPhoneNumberExistsAsync(dto.PhoneNumber!, dto.CountryCode!);
                    if (phoneExists)
                    {
                        _logger.LogWarning("Phone number already registered: {CountryCode} {Phone}", dto.CountryCode, dto.PhoneNumber);
                        return BadRequest(ApiResponseFactory.Fail("Phone number is already registered."));
                    }

                    // Check if email already exists (only if email is provided)
                    if (!string.IsNullOrWhiteSpace(dto.Email))
                    {
                        bool emailExists = await _userRepository.IsEmailExistsAsync(dto.Email);
                        if (emailExists)
                        {
                            _logger.LogWarning("Email already registered: {Email}", dto.Email);
                            return BadRequest(ApiResponseFactory.Fail("Email is already registered."));
                        }
                    }

                    // Generate HFID and timestamp - we validated these are not null earlier
                    var hfid = _hfidService.GenerateHfid(dto.FirstName!, dto.LastName!, parsedDob);
                    var epochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                    // Create new user entity
                    user = new User
                    {
                        FirstName = dto.FirstName!,
                        LastName = dto.LastName!,
                        DOB = dto.DOB!,
                        CountryCallingCode = dto.CountryCode!,
                        PhoneNumber = dto.PhoneNumber!,
                        Email = string.IsNullOrWhiteSpace(dto.Email) ? string.Empty : dto.Email,
                        HfId = hfid,
                        UserReference = 0,
                        IsEmailVerified = !string.IsNullOrWhiteSpace(dto.Email), // True only if email provided
                        IsPhoneVerified = true, // Always true for clinic-created patients
                        DeletedBy = 0,
                        CreatedEpoch = epochTime,
                        Password = null // No password for clinic-created patients
                    };

                    // Save user to database
                    await _userRepository.AddUserAsync(user);
                    await _userRepository.CommitAsync();

                    // Assign default "Basic" subscription to the new user
                    var subscription = new UserSubscription
                    {
                        UserId = user.Id,
                        SubscriptionPlan = "Basic",
                        StartEpoch = epochTime,
                        EndEpoch = SUBSCRIPTION_UNLIMITED
                    };

                    await _userRepository.AddSubscriptionAsync(subscription);
                    await _userRepository.CommitAsync();

                    isPatientNewlyCreated = true;

                    _logger.LogInformation(
                        "New patient created successfully. UserId: {UserId}, HFID: {HFID}, Email: {Email}",
                        user.Id, user.HfId, string.IsNullOrWhiteSpace(user.Email) ? "Not provided" : user.Email);
                }

                // ==================== COMMON FLOW: CREATE APPOINTMENT AND VISIT ====================

                HttpContext.Items["Sent-To-UserId"] = user.Id;

                var fullName = $"{user.FirstName} {user.LastName}";
                var phone = user.PhoneNumber ?? "N/A";

                // Get or create patient record in clinic system - user.HfId is guaranteed to be non-null here
                var patient = await _clinicVisitRepository.GetOrCreatePatientAsync(user.HfId!, fullName);

                // Validate consent forms
                var consentForms = await _clinicVisitRepository.GetConsentFormsByTitlesAsync(dto.ConsentFormTitles);
                if (consentForms.Count != dto.ConsentFormTitles.Count)
                {
                    var missing = dto.ConsentFormTitles.Except(consentForms.Select(f => f.Title)).ToList();
                    _logger.LogWarning("Invalid consent form titles: {Missing}", string.Join(", ", missing));
                    return BadRequest(ApiResponseFactory.Fail($"Invalid consent form titles: {string.Join(", ", missing)}"));
                }

                // Create clinic visit record
                var visit = new ClinicVisit
                {
                    ClinicPatientId = patient.Id,
                    ClinicId = clinicId,
                    AppointmentDate = appointmentDate.Date,
                    AppointmentTime = appointmentTime,
                    ConsentFormsSent = consentForms.Select(f => new ClinicVisitConsentForm
                    {
                        ConsentFormId = f.Id
                    }).ToList()
                };
                await _clinicVisitRepository.SaveVisitAsync(visit);

                // Create appointment record
                var appointment = new ClinicAppointment
                {
                    VisitorUsername = fullName,
                    VisitorPhoneNumber = phone,
                    AppointmentDate = appointmentDate.Date,
                    AppointmentTime = appointmentTime,
                    ClinicId = clinicId,
                    Status = "Scheduled"
                };
                await _appointmentRepository.SaveAppointmentAsync(appointment);

                // Create Google Calendar Event
                var clinic = await _userRepository.GetClinicByIdAsync(clinicId);
                var googleEventId = await _googleCalendarService.CreateAppointmentAsync(
                    clinicId,
                    fullName,
                    clinic?.ClinicName ?? "Clinic",
                    appointmentDate.Date,
                    appointmentTime,
                    phone
                );

                if (!string.IsNullOrEmpty(googleEventId))
                {
                    appointment.GoogleCalendarEventId = googleEventId;
                    await _userRepository.SaveChangesAsync();
                    _logger.LogInformation("Google Calendar event created successfully. EventId: {EventId}", googleEventId);
                }

                // Commit transaction before sending emails
                await transaction.CommitAsync();
                committed = true;

                // ==================== GENERATE CONSENT FORM LINKS ====================

                var consentFormLinks = new List<ConsentFormLinkInfo>();

                if (visit.ConsentFormsSent.Any())
                {
                    var baseUrl = GetBaseUrl();

                    for (int i = 0; i < visit.ConsentFormsSent.Count; i++)
                    {
                        var consentFormEntry = visit.ConsentFormsSent.ElementAt(i);
                        var consentFormTitle = dto.ConsentFormTitles[i];
                        var encodedConsentName = Uri.EscapeDataString(consentFormTitle);

                        string formUrl = DetermineConsentFormUrl(consentFormTitle);
                        var consentFormLink = $"{baseUrl}/{formUrl}?ConsentId={consentFormEntry.Id}&ConsentName={encodedConsentName}&hfid={patient.HFID}";

                        consentFormLinks.Add(new ConsentFormLinkInfo
                        {
                            ConsentFormId = consentFormEntry.Id,
                            ConsentFormName = consentFormTitle,
                            ConsentFormLink = consentFormLink
                        });
                    }

                    // ==================== SEND EMAIL NOTIFICATION ====================

                    // Only send email if email is provided
                    if (!string.IsNullOrWhiteSpace(user.Email) && consentFormLinks.Any())
                    {
                        try
                        {
                            var emailTemplate = _emailTemplateService.GenerateAppointmentConfirmationWithConsentFormsEmailTemplate(
                                user.FirstName!,
                                consentFormLinks,
                                clinic?.ClinicName ?? "Clinic",
                                appointmentDate.ToString("dd-MM-yyyy"),
                                appointmentTime.ToString(@"hh\:mm")
                            );

                            await _emailService.SendEmailAsync(
                                user.Email,
                                $"Appointment Confirmation & Consent Forms - {clinic?.ClinicName}",
                                emailTemplate
                            );

                            _logger.LogInformation(
                                "Appointment confirmation email sent successfully to {Email} with {Count} consent forms",
                                user.Email, consentFormLinks.Count);
                        }
                        catch (Exception emailEx)
                        {
                            _logger.LogError(emailEx,
                                "Failed to send appointment confirmation email to {Email} for appointment {AppointmentId}",
                                user.Email, appointment.Id);
                            // Don't fail the entire operation if email fails
                        }
                    }
                    else if (string.IsNullOrWhiteSpace(user.Email))
                    {
                        _logger.LogInformation("Email not provided for patient HFID {HFID}. Skipping email notification.", user.HfId);
                    }
                }

                // Invalidate cache after successful creation
                _cacheService.InvalidateClinicStatistics(clinicId);

                // ==================== BUILD RESPONSE ====================

                var consentFormsInfo = consentFormLinks.Any()
                    ? $"\n\nConsent Forms to Complete:\n{string.Join("\n", consentFormLinks.Select((link, index) => $"{index + 1}. {link.ConsentFormName}: {link.ConsentFormLink}"))}"
                    : "";

                var appointmentDateFormatted = appointmentDate.ToString("dd-MM-yyyy");
                var appointmentTimeFormatted = appointmentTime.ToString(@"hh\:mm");

                var userNotificationMessage = $"{clinic?.ClinicName} has scheduled an appointment for you on {appointmentDateFormatted} at {appointmentTimeFormatted}. Please arrive on time.{consentFormsInfo}";

                var response = new
                {
                    // Patient Information
                    PatientName = patient.PatientName,
                    HFID = patient.HFID,
                    ProfilePhoto = user.ProfilePhoto,
                    IsNewPatient = isPatientNewlyCreated,
                    Email = string.IsNullOrWhiteSpace(user.Email) ? "Not provided" : user.Email,
                    PhoneNumber = user.PhoneNumber,
                    IsEmailVerified = user.IsEmailVerified,
                    IsPhoneVerified = user.IsPhoneVerified,

                    // Appointment Details
                    AppointmentDate = appointmentDateFormatted,
                    AppointmentTime = appointmentTimeFormatted,
                    Treatment = appointment.Treatment,
                    AppointmentStatus = appointment.Status,
                    ClinicId = clinicId,
                    ClinicName = clinic?.ClinicName,

                    // Consent Forms
                    ConsentFormsSent = consentForms.Select(f => f.Title).ToList(),
                    ConsentFormLinks = consentFormLinks.Select(link => new
                    {
                        ConsentFormId = link.ConsentFormId,
                        ConsentFormName = link.ConsentFormName,
                        ConsentFormLink = link.ConsentFormLink
                    }).ToList(),

                    // Email Status
                    EmailSent = !string.IsNullOrWhiteSpace(user.Email) && consentFormLinks.Any(),
                    SentToEmail = string.IsNullOrWhiteSpace(user.Email) ? null : user.Email,

                    // Notification Context
                    NotificationContext = new
                    {
                        AppointmentId = appointment.Id,
                        PatientName = patient.PatientName,
                        HFID = patient.HFID,
                        PhoneNumber = user.PhoneNumber,
                        AppointmentDate = appointmentDateFormatted,
                        AppointmentTime = appointmentTimeFormatted,
                        Status = "Scheduled",
                        ConsentFormsCount = consentFormLinks.Count,
                        ConsentFormNames = string.Join(", ", consentFormLinks.Select(f => f.ConsentFormName)),
                        ClinicName = clinic?.ClinicName,
                        EmailStatus = !string.IsNullOrWhiteSpace(user.Email) && consentFormLinks.Any()
                            ? "Sent"
                            : string.IsNullOrWhiteSpace(user.Email)
                                ? "No email provided"
                                : "No forms to send",
                        IsNewPatient = isPatientNewlyCreated
                    },

                    NotificationMessage = $"Appointment scheduled for {patient.PatientName} on {appointmentDateFormatted} at {appointmentTimeFormatted}." +
                                        (consentFormLinks.Any() ? $" {consentFormLinks.Count} consent form(s) sent." : ""),
                    UserNotificationMessage = userNotificationMessage
                };

                _logger.LogInformation(
                    "Follow-up appointment created successfully. HFID: {HFID}, ClinicId: {ClinicId}, IsNewPatient: {IsNewPatient}, ConsentFormsCount: {Count}",
                    user.HfId, clinicId, isPatientNewlyCreated, consentFormLinks.Count);

                var successMessage = isPatientNewlyCreated
                    ? "Patient registered and appointment created successfully."
                    : "Appointment created successfully.";

                return Ok(ApiResponseFactory.Success(response, successMessage));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating follow-up appointment for ClinicId {ClinicId}", clinicId);
                return StatusCode(500, ApiResponseFactory.Fail("Unexpected error occurred while processing the appointment."));
            }
            finally
            {
                if (!committed && transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.RollbackAsync();
                    _logger.LogWarning("Transaction rolled back for ClinicId {ClinicId}", clinicId);
                }
            }
        }





        // Check existing patient and book follow up appointment
        [HttpPost("clinics/{clinicId}/appointments/follow-up")]
        [Authorize]
        public async Task<IActionResult> BookFollowUpAppointmentForExistingPatient(
            [FromBody] FollowUpAppointmentDto dto,
            [FromRoute] int clinicId)
        {
            HttpContext.Items["Log-Category"] = "Clinic Appointment";

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
                bool hasVisit = await _clinicVisitRepository.HasVisitInClinicAsync(dto.HFID!, clinicId);
                if (!hasVisit)
                {
                    _logger.LogWarning("Patient with HFID {HFID} not found in Clinic ID {ClinicId}", dto.HFID, clinicId);
                    return BadRequest(ApiResponseFactory.Fail("Patient not found in this clinic. Please add the patient first."));
                }

                var user = await _userRepository.GetUserByHFIDAsync(dto.HFID!);
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

                HttpContext.Items["Sent-To-UserId"] = user.Id;
                var fullName = $"{user.FirstName} {user.LastName}";
                var phone = user.PhoneNumber ?? "N/A";

                var patient = await _clinicVisitRepository.GetOrCreatePatientAsync(dto.HFID!, fullName);

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
                var appointmentDateFormatted = date.ToString("dd-MM-yyyy");
                var appointmentTimeFormatted = time.ToString(@"hh\:mm");

                var consentFormsInfo = consentFormLinks.Any()
                    ? $"\n\nConsent Forms to Complete:\n{string.Join("\n", consentFormLinks.Select((link, index) => $"{index + 1}. {link.ConsentFormName}: {link.ConsentFormLink}"))}"
                    : "";

                var userNotificationMessage = $"{clinic?.ClinicName} has scheduled a follow-up appointment for you on {appointmentDateFormatted} at {appointmentTimeFormatted}. Please arrive on time.{consentFormsInfo}";

                // INVALIDATE CACHE AFTER SUCCESSFUL CREATION
                _cacheService.InvalidateClinicStatistics(clinicId);

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
                    NotificationMessage = CreateNotificationMessage(fullName, date, time, consentFormLinks),
                    UserNotificationMessage = userNotificationMessage
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
        [FromServices] ClinicRepository clinicRepository,
        [FromServices] ClinicPatientRecordRepository recordRepository,
        [FromServices] IUserRepository userRepository,
        [FromQuery] string? startDate,
        [FromQuery] string? endDate,
        [FromQuery] string? paymentStatus,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 6)
        {
            HttpContext.Items["Log-Category"] = "Clinic Patient Overview";
            if (!await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User))
            {
                _logger.LogWarning("Unauthorized patient view attempt for Clinic ID {ClinicId}", clinicId);
                return Unauthorized(ApiResponseFactory.Fail("Only main or branch clinics can view patients."));
            }
            // Validate pagination parameters
            if (page < 1)
                return BadRequest(ApiResponseFactory.Fail("Page must be greater than 0."));
            if (pageSize < 1 || pageSize > 50)
                return BadRequest(ApiResponseFactory.Fail("PageSize must be between 1 and 50."));
            try
            {
                DateTime? start = null;
                DateTime? end = null;
                PaymentStatusFilter? paymentFilter = null;
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
                if (!string.IsNullOrEmpty(paymentStatus))
                {
                    paymentFilter = paymentStatus.ToLowerInvariant() switch
                    {
                        "paid" => PaymentStatusFilter.Paid,
                        "unpaid" => PaymentStatusFilter.Unpaid,
                        "all" => PaymentStatusFilter.All,
                        _ => (PaymentStatusFilter?)null
                    };
                    if (!paymentFilter.HasValue)
                        return BadRequest(ApiResponseFactory.Fail("Invalid paymentStatus. Expected 'paid', 'unpaid', or 'all'."));
                }
                var patients = await clinicRepository.GetClinicPatientsWithVisitsAsync(clinicId);
                // Pre-filter patients by date range and payment status in memory (without building full DTOs or fetching extra data)
                var allMatchingPatients = new List<ClinicPatient>();
                foreach (var patient in patients)
                {
                    var lastVisit = patient.Visits.OrderByDescending(v => v.AppointmentDate).FirstOrDefault();
                    if (lastVisit == null) continue;
                    if ((start.HasValue && lastVisit.AppointmentDate.Date < start.Value.Date) ||
                        (end.HasValue && lastVisit.AppointmentDate.Date > end.Value.Date))
                        continue;
                    bool paymentMatch = true;
                    if (paymentFilter.HasValue)
                    {
                        var isPaid = lastVisit.PaymentMethod != null;
                        paymentMatch = paymentFilter.Value switch
                        {
                            PaymentStatusFilter.Paid => isPaid,
                            PaymentStatusFilter.Unpaid => !isPaid,
                            _ => true
                        };
                    }
                    if (paymentMatch)
                        allMatchingPatients.Add(patient);
                }
                // Apply pagination on the filtered patients list
                int totalCount = allMatchingPatients.Count;
                var orderedPatients = allMatchingPatients
                    .OrderByDescending(p => p.Visits.Max(v => v.AppointmentDate))
                    .ToList();
                var pagedPatients = orderedPatients
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();
                int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
                if (!pagedPatients.Any())
                {
                    var emptyResponse = new
                    {
                        TotalPatients = totalCount,
                        Page = page,
                        PageSize = pageSize,
                        TotalPages = totalPages,
                        Patients = new List<PatientDto>()
                    };
                    return Ok(ApiResponseFactory.Success(emptyResponse, "No patients found matching the criteria."));
                }
                // Build HFID → ProfilePhoto map only for paged patients (max 6)
                var uniqueHfids = pagedPatients
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
                foreach (var patient in pagedPatients)
                {
                    var lastVisit = patient.Visits.OrderByDescending(v => v.AppointmentDate).FirstOrDefault();
                    // Safe to assume lastVisit is not null due to pre-filtering
                    var treatmentRecords = await recordRepository.GetTreatmentRecordsAsync(clinicId, patient.Id, lastVisit!.Id);
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
                        PaymentMethod = lastVisit?.PaymentMethod,
                        PaymentStatus = lastVisit?.PaymentMethod?.ToString() ?? "Pending",
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
                // No need for ApplyPaymentStatusFilter since pre-filtered
                var response = new
                {
                    TotalPatients = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = totalPages,
                    Patients = filteredPatients
                };
                return Ok(ApiResponseFactory.Success(response, "Paginated clinic patient data retrieved successfully."));
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
        [Obsolete]
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




        /// <summary>
        /// Retrieves clinic statistics including total patients and appointments
        /// </summary>
        /// <param name="clinicId">The clinic ID to fetch statistics for</param>
        /// <returns>Total patients and appointments count with detailed breakdown</returns>
        [HttpGet("clinic/{clinicId:int}/statistics")]
        [Authorize]
        public async Task<IActionResult> GetClinicStatistics([FromRoute] int clinicId)
        {
            HttpContext.Items["Log-Category"] = "Clinic Statistics";

            // Input validation
            if (clinicId <= 0)
            {
                _logger.LogWarning("Invalid clinic ID provided: {ClinicId}", clinicId);
                return BadRequest(ApiResponseFactory.Fail("Clinic ID must be a positive integer."));
            }

            // Authorization check
            bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
            if (!isAuthorized)
            {
                _logger.LogWarning("Unauthorized statistics access attempt for Clinic ID {ClinicId}", clinicId);
                return Unauthorized(ApiResponseFactory.Fail("You are not authorized to view statistics for this clinic."));
            }

            try
            {
                // Verify clinic exists
                var clinicExists = await _clinicRepository.ExistsAsync(clinicId);
                if (!clinicExists)
                {
                    _logger.LogWarning("Clinic not found for ID {ClinicId}", clinicId);
                    return NotFound(ApiResponseFactory.Fail("Clinic not found."));
                }

                // Fetch all appointments for the clinic
                var appointments = await _appointmentRepository.GetAppointmentsByClinicIdAsync(clinicId);

                // Fetch all visits to count unique patients
                var visits = await _clinicVisitRepository.GetVisitsByClinicIdAsync(clinicId);

                // Get unique patient IDs from visits
                var uniquePatientIds = visits
                    .Select(v => v.ClinicPatientId)
                    .Distinct()
                    .ToList();

                // Calculate statistics
                var totalPatients = uniquePatientIds.Count;
                var totalAppointments = appointments.Count;

                // Appointment breakdown by status
                var scheduledAppointments = appointments.Count(a => a.Status == "Scheduled");
                var completedAppointments = appointments.Count(a => a.Status == "Completed");
                var canceledAppointments = appointments.Count(a => a.Status == "Canceled");
                var absentAppointments = appointments.Count(a => a.Status == "Absent");

                // Today's statistics
                var today = DateTime.Today;
                var todayAppointments = appointments.Count(a => a.AppointmentDate.Date == today);
                var todayCompletedAppointments = appointments.Count(a =>
                    a.AppointmentDate.Date == today && a.Status == "Completed");

                // Current month statistics
                var currentMonth = DateTime.Today.Month;
                var currentYear = DateTime.Today.Year;
                var monthlyAppointments = appointments.Count(a =>
                    a.AppointmentDate.Month == currentMonth &&
                    a.AppointmentDate.Year == currentYear);

                // Build response
                var response = new
                {
                    ClinicId = clinicId,
                    TotalPatients = totalPatients,
                    TotalAppointments = totalAppointments,

                    AppointmentsByStatus = new
                    {
                        Scheduled = scheduledAppointments,
                        Completed = completedAppointments,
                        Canceled = canceledAppointments,
                        Absent = absentAppointments
                    },

                    TodayStatistics = new
                    {
                        TotalAppointments = todayAppointments,
                        CompletedAppointments = todayCompletedAppointments,
                        Date = today.ToString("dd-MM-yyyy")
                    },

                    MonthlyStatistics = new
                    {
                        TotalAppointments = monthlyAppointments,
                        Month = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(currentMonth),
                        Year = currentYear
                    },

                    Summary = new
                    {
                        AverageAppointmentsPerPatient = totalPatients > 0
                            ? Math.Round((double)totalAppointments / totalPatients, 2)
                            : 0,
                        CompletionRate = totalAppointments > 0
                            ? Math.Round((double)completedAppointments / totalAppointments * 100, 2)
                            : 0,
                        CancellationRate = totalAppointments > 0
                            ? Math.Round((double)canceledAppointments / totalAppointments * 100, 2)
                            : 0
                    }
                };

                _logger.LogInformation(
                    "Statistics retrieved for Clinic ID {ClinicId}: {TotalPatients} patients, {TotalAppointments} appointments",
                    clinicId, totalPatients, totalAppointments);

                return Ok(ApiResponseFactory.Success(response, "Clinic statistics retrieved successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while fetching statistics for Clinic ID {ClinicId}", clinicId);
                return StatusCode(500, ApiResponseFactory.Fail("An unexpected error occurred while retrieving clinic statistics."));
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




        // *******************************************************************************************************************************




        /// <summary>
        /// Imports 2019 patient appointments from CSV file and creates appointment entries with clinic visits.
        /// CSV structure: date, patientId, localPatientName, mobileNumber, emailAddress, secMobile, gender...
        /// Only processes patients with 2019 dates. Creates appointments and clinic visits for existing users.
        /// </summary>
        /// <param name="request">The CSV file and clinic ID</param>
        /// <returns>Summary of import results including added, not found, and skipped appointments</returns>
        /// <summary>
        /// Imports 2019 patient appointments from CSV file and creates appointment entries with clinic visits.
        /// CSV structure: date, patientId, localPatientName, mobileNumber, emailAddress, secMobile, gender...
        /// Date format: "Tue Apr 01 19:54:27 IST 2019"
        /// Only processes patients with 2019 dates. Creates appointments and clinic visits for existing users.
        /// </summary>
        /// <param name="request">The CSV file and clinic ID</param>
        /// <returns>Summary of import results including added, not found, and skipped appointments</returns>
        /// <summary>
        /// Imports 2019 patient appointments from CSV file and creates appointment entries with clinic visits.
        /// CSV structure: doctorName, patientName, patientId, date, startTime, endTime, status, explanation
        /// Date format: "Fri Aug 16 14:00:00 IST 2019"
        /// Only processes patients with 2019 dates. Creates appointments and clinic visits for existing users.
        /// </summary>
        [HttpPost("import-2019-appointments-csv")]
        //[Authorize]
        public async Task<IActionResult> Import2019AppointmentsFromCsv([FromForm] Appointment2019ImportRequest request)
        {
            HttpContext.Items["Log-Category"] = "2019 Appointment Import";

            _logger.LogInformation("Starting 2019 appointment import from CSV file for Clinic ID {ClinicId}", request.ClinicId);

            // Validate clinic exists
            var clinicExists = await _clinicRepository.ExistsAsync(request.ClinicId);
            if (!clinicExists)
            {
                _logger.LogWarning("Clinic ID {ClinicId} does not exist", request.ClinicId);
                return BadRequest(ApiResponseFactory.Fail("Invalid Clinic ID."));
            }

            // Validate file upload
            if (request.CsvFile == null || request.CsvFile.Length == 0)
                return BadRequest(ApiResponseFactory.Fail("CSV file is required."));

            var extension = Path.GetExtension(request.CsvFile.FileName).ToLower();
            if (extension != ".csv")
                return BadRequest(ApiResponseFactory.Fail("Only .csv files are supported."));

            var response = new Appointment2019ImportResponse();
            var appointmentsToAdd = new List<ClinicAppointment>();
            var visitsToAdd = new List<ClinicVisit>();
            var skippedReasons = new List<string>();
            var patientNotFoundList = new List<string>();

            var transaction = await _clinicRepository.BeginTransactionAsync();
            var committed = false;

            try
            {
                using var stream = new MemoryStream();
                await request.CsvFile.CopyToAsync(stream);
                stream.Position = 0;

                using var reader = new StreamReader(stream);
                using var parser = new TextFieldParser(reader);

                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");
                parser.HasFieldsEnclosedInQuotes = true;
                parser.TrimWhiteSpace = true;

                // Skip header row
                if (!parser.EndOfData)
                    parser.ReadFields();

                int rowNumber = 1;

                while (!parser.EndOfData)
                {
                    rowNumber++;

                    try
                    {
                        string[]? fields = parser.ReadFields();

                        if (fields == null || fields.Length == 0)
                            continue;

                        response.TotalProcessed++;

                        // Check minimum columns (8 columns expected)
                        if (fields.Length < 7)
                        {
                            skippedReasons.Add($"Row {rowNumber}: Insufficient columns ({fields.Length})");
                            response.Skipped++;
                            continue;
                        }

                        // Extract data from CSV columns
                        // Column 0: doctorName (skip)
                        // Column 1: patientName
                        // Column 2: patientId
                        // Column 3: date (format: "Fri Aug 16 14:00:00 IST 2019")
                        // Column 4: startTime (minutes)
                        // Column 5: endTime (minutes)
                        // Column 6: status (CONFIRM/CANCEL)
                        // Column 7: explanation (optional)

                        var patientName = fields[1].Trim();
                        var patientId = fields[2].Trim();
                        var dateString = fields[3].Trim();
                        var status = fields[6].Trim();
                        var explanation = fields.Length > 7 ? fields[7].Trim() : "";

                        // Parse date using the complex date string parser
                        var dateTimeResult = ParseDateTimeString(dateString);
                        if (!dateTimeResult.IsValid)
                        {
                            skippedReasons.Add($"Row {rowNumber}: {dateTimeResult.ErrorMessage}");
                            response.Skipped++;
                            continue;
                        }

                        // Filter: Only process 2019 appointments
                        if (dateTimeResult.Date.Year != 2019)
                        {
                            _logger.LogDebug("Skipping row {Row}: Date is not from 2019 ({Date})",
                                rowNumber, dateTimeResult.Date.ToString("yyyy-MM-dd"));
                            continue;
                        }

                        // Validate required fields
                        if (string.IsNullOrWhiteSpace(patientName))
                        {
                            skippedReasons.Add($"Row {rowNumber}: Patient name is required");
                            response.Skipped++;
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(patientId))
                        {
                            skippedReasons.Add($"Row {rowNumber}: Patient ID is required");
                            response.Skipped++;
                            continue;
                        }

                        if (!status.Equals("CONFIRM", StringComparison.OrdinalIgnoreCase) &&
                            !status.Equals("CANCEL", StringComparison.OrdinalIgnoreCase))
                        {
                            skippedReasons.Add($"Row {rowNumber}: Invalid status '{status}'. Expected CONFIRM or CANCEL");
                            response.Skipped++;
                            continue;
                        }

                        // Try to find user by PatientId only (CSV doesn't have mobile/email)
                        var user = await _userRepository.GetUserByPatientIdAsync(patientId);

                        if (user == null)
                        {
                            patientNotFoundList.Add($"Row {rowNumber}: Patient '{patientName}' (ID: {patientId}) not found in system");
                            response.PatientNotFound++;
                            _logger.LogInformation("Patient not found for row {Row}: {PatientId}", rowNumber, patientId);
                            continue;
                        }

                        // Null-safe access to user properties
                        var userPhoneNumber = user.PhoneNumber ?? "";
                        var userFirstName = user.FirstName ?? "Unknown";
                        var userLastName = user.LastName ?? "User";
                        var userHfId = user.HfId ?? "";

                        // Check if appointment already exists for this date/time
                        var existingAppointment = await CheckExistingAppointment(
                            request.ClinicId,
                            userPhoneNumber,
                            dateTimeResult.Date,
                            dateTimeResult.Time);

                        if (existingAppointment != null)
                        {
                            skippedReasons.Add($"Row {rowNumber}: Appointment already exists for {userFirstName} {userLastName} on {dateTimeResult.Date:dd-MM-yyyy} at {dateTimeResult.Time:hh\\:mm}");
                            response.AlreadyHasAppointment++;
                            continue;
                        }

                        // Create full name
                        var fullName = $"{userFirstName} {userLastName}".Trim();

                        // Get or create clinic patient
                        var clinicPatient = await _clinicVisitRepository.GetOrCreatePatientAsync(
                            userHfId,
                            fullName);

                        // Convert status: CONFIRM -> Completed, CANCEL -> Canceled
                        string appointmentStatus = status.Equals("CONFIRM", StringComparison.OrdinalIgnoreCase)
                            ? "Completed"
                            : "Canceled";

                        // Create appointment
                        var appointment = new ClinicAppointment
                        {
                            VisitorUsername = fullName,
                            VisitorPhoneNumber = userPhoneNumber,
                            AppointmentDate = dateTimeResult.Date.Date,
                            AppointmentTime = dateTimeResult.Time,
                            ClinicId = request.ClinicId,
                            Status = appointmentStatus,
                            Treatment = string.IsNullOrWhiteSpace(explanation) ? null : explanation
                        };

                        appointmentsToAdd.Add(appointment);

                        // Only create visit if appointment was confirmed
                        if (appointmentStatus == "Completed")
                        {
                            // Check if visit already exists for this date AND time
                            var existingVisit = await _clinicVisitRepository.GetExistingVisitAsyncWithTime(
                                clinicPatient.Id,
                                dateTimeResult.Date.Date,
                                dateTimeResult.Time);

                            if (existingVisit == null)
                            {
                                // Create clinic visit
                                var visit = new ClinicVisit
                                {
                                    ClinicPatientId = clinicPatient.Id,
                                    ClinicId = request.ClinicId,
                                    AppointmentDate = dateTimeResult.Date.Date,
                                    AppointmentTime = dateTimeResult.Time,
                                    PaymentMethod = null
                                };

                                visitsToAdd.Add(visit);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing row {Row}", rowNumber);
                        skippedReasons.Add($"Row {rowNumber}: Processing error - {ex.Message}");
                        response.Skipped++;
                    }
                }

                // Bulk insert appointments and visits
                if (appointmentsToAdd.Any())
                {
                    await _appointmentRepository.AddRangeAsync(appointmentsToAdd);
                    await _appointmentRepository.SaveChangesAsync();

                    // Save visits after appointments
                    foreach (var visit in visitsToAdd)
                    {
                        await _clinicVisitRepository.SaveVisitAsync(visit);
                    }

                    response.SuccessfullyAdded = appointmentsToAdd.Count;

                    // Build summary list
                    response.AddedAppointments = new List<ImportedAppointmentSummary>();
                    for (int i = 0; i < appointmentsToAdd.Count; i++)
                    {
                        var appointment = appointmentsToAdd[i];
                        var visit = visitsToAdd.FirstOrDefault(v =>
                            v.AppointmentDate == appointment.AppointmentDate &&
                            v.AppointmentTime == appointment.AppointmentTime);

                        response.AddedAppointments.Add(new ImportedAppointmentSummary
                        {
                            AppointmentId = appointment.Id,
                            VisitId = visit?.Id,
                            PatientName = appointment.VisitorUsername,
                            PatientId = "",
                            HFID = "",
                            PhoneNumber = appointment.VisitorPhoneNumber,
                            AppointmentDate = appointment.AppointmentDate.ToString("dd-MM-yyyy"),
                            AppointmentTime = appointment.AppointmentTime.ToString(@"hh\:mm"),
                            Status = appointment.Status
                        });
                    }
                }

                await transaction.CommitAsync();
                committed = true;

                response.PatientNotFoundList = patientNotFoundList;
                response.SkippedReasons = skippedReasons;
                response.Message = $"Import completed: {response.SuccessfullyAdded} appointments added, " +
                                  $"{response.PatientNotFound} patients not found, " +
                                  $"{response.AlreadyHasAppointment} already have appointments, " +
                                  $"{response.Skipped} skipped out of {response.TotalProcessed} total 2019 records.";

                _logger.LogInformation("2019 Appointment import completed for Clinic {ClinicId}: {Added} added, {NotFound} not found, {Skipped} skipped",
                    request.ClinicId, response.SuccessfullyAdded, response.PatientNotFound, response.Skipped);

                // Invalidate cache
                _cacheService.InvalidateClinicStatistics(request.ClinicId);

                return Ok(ApiResponseFactory.Success(response, response.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import 2019 appointments from CSV for Clinic {ClinicId}", request.ClinicId);
                return StatusCode(500, ApiResponseFactory.Fail("Failed to process CSV file: " + ex.Message));
            }
            finally
            {
                if (!committed && transaction?.GetDbTransaction()?.Connection != null)
                {
                    await transaction.RollbackAsync();
                }
            }
        }

        /// <summary>
        /// Parses date/time string in format: "Fri Aug 16 14:00:00 IST 2019"
        /// </summary>
        private (bool IsValid, DateTime Date, TimeSpan Time, string ErrorMessage) ParseDateTimeString(string dateString)
        {
            try
            {
                // Pattern: "Fri Aug 16 14:00:00 IST 2019"
                // Format: {weekday} {month} {dd} {hh:mm:ss} IST {yyyy}
                var regex = new Regex(@"^\w{3}\s+(\w{3})\s+(\d{1,2})\s+(\d{2}):(\d{2}):(\d{2})\s+IST\s+(\d{4})$");
                var match = regex.Match(dateString);

                if (!match.Success)
                    return (false, default, default, $"Invalid date format: {dateString}");

                var monthStr = match.Groups[1].Value;
                var day = int.Parse(match.Groups[2].Value);
                var hour = int.Parse(match.Groups[3].Value);
                var minute = int.Parse(match.Groups[4].Value);
                var second = int.Parse(match.Groups[5].Value);
                var year = int.Parse(match.Groups[6].Value);

                // Convert month name to number
                var month = monthStr switch
                {
                    "Jan" => 1,
                    "Feb" => 2,
                    "Mar" => 3,
                    "Apr" => 4,
                    "May" => 5,
                    "Jun" => 6,
                    "Jul" => 7,
                    "Aug" => 8,
                    "Sep" => 9,
                    "Oct" => 10,
                    "Nov" => 11,
                    "Dec" => 12,
                    _ => throw new ArgumentException($"Invalid month: {monthStr}")
                };

                var date = new DateTime(year, month, day);
                var time = new TimeSpan(hour, minute, second);

                return (true, date, time, "");
            }
            catch (Exception ex)
            {
                return (false, default, default, $"Error parsing date '{dateString}': {ex.Message}");
            }
        }

        private async Task<ClinicAppointment?> CheckExistingAppointment(int clinicId, string phoneNumber, DateTime date, TimeSpan time)
        {
            var appointments = await _appointmentRepository.GetAppointmentsByClinicIdAsync(clinicId);

            return appointments.FirstOrDefault(a =>
                a.VisitorPhoneNumber == phoneNumber &&
                a.AppointmentDate.Date == date.Date &&
                a.AppointmentTime == time);
        }










        /// <summary>
        /// Imports 2020-2024 patient appointments from CSV file and creates appointment entries with clinic visits.
        /// CSV structure: doctorName, patientName, patientId, date, startTime, endTime, status, explanation
        /// Date format: "Fri Aug 16 14:00:00 IST 2020" (or 2021, 2022, 2023, 2024)
        /// Only processes patients with 2020-2024 dates. Creates appointments and clinic visits for existing users.
        /// Provides year-wise breakdown of imported appointments.
        /// </summary>
        /// <param name="request">The CSV file and clinic ID</param>
        /// <returns>Summary of import results including added, not found, and skipped appointments with year-wise statistics</returns>
        [HttpPost("import-2020-2024-appointments-csv")]
        //[Authorize]
        public async Task<IActionResult> Import2020To2024AppointmentsFromCsv([FromForm] Appointment2020To2024ImportRequest request)
        {
            HttpContext.Items["Log-Category"] = "2020-2024 Appointment Import";

            _logger.LogInformation("Starting 2020-2024 appointment import from CSV file for Clinic ID {ClinicId}", request.ClinicId);

            // Validate clinic exists
            var clinicExists = await _clinicRepository.ExistsAsync(request.ClinicId);
            if (!clinicExists)
            {
                _logger.LogWarning("Clinic ID {ClinicId} does not exist", request.ClinicId);
                return BadRequest(ApiResponseFactory.Fail("Invalid Clinic ID."));
            }

            // Validate file upload
            if (request.CsvFile == null || request.CsvFile.Length == 0)
                return BadRequest(ApiResponseFactory.Fail("CSV file is required."));

            var extension = Path.GetExtension(request.CsvFile.FileName).ToLower();
            if (extension != ".csv")
                return BadRequest(ApiResponseFactory.Fail("Only .csv files are supported."));

            var response = new Appointment2020To2024ImportResponse();
            var appointmentsToAdd = new List<ClinicAppointment>();
            var visitsToAdd = new List<ClinicVisit>();
            var skippedReasons = new List<string>();
            var patientNotFoundList = new List<string>();

            // Initialize year-wise breakdown for 2020-2024
            var yearWiseStats = new Dictionary<int, YearWiseAppointmentStats>();
            for (int year = 2020; year <= 2024; year++)
            {
                yearWiseStats[year] = new YearWiseAppointmentStats();
            }

            var transaction = await _clinicRepository.BeginTransactionAsync();
            var committed = false;

            try
            {
                using var stream = new MemoryStream();
                await request.CsvFile.CopyToAsync(stream);
                stream.Position = 0;

                using var reader = new StreamReader(stream);
                using var parser = new TextFieldParser(reader);

                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");
                parser.HasFieldsEnclosedInQuotes = true;
                parser.TrimWhiteSpace = true;

                // Skip header row
                if (!parser.EndOfData)
                    parser.ReadFields();

                int rowNumber = 1;

                while (!parser.EndOfData)
                {
                    rowNumber++;

                    try
                    {
                        string[]? fields = parser.ReadFields();

                        if (fields == null || fields.Length == 0)
                            continue;

                        response.TotalProcessed++;

                        // Check minimum columns (8 columns expected)
                        if (fields.Length < 7)
                        {
                            skippedReasons.Add($"Row {rowNumber}: Insufficient columns ({fields.Length})");
                            response.Skipped++;
                            continue;
                        }

                        // Extract data from CSV columns
                        // Column 0: doctorName (skip)
                        // Column 1: patientName
                        // Column 2: patientId
                        // Column 3: date (format: "Fri Aug 16 14:00:00 IST 2020")
                        // Column 4: startTime (minutes)
                        // Column 5: endTime (minutes)
                        // Column 6: status (CONFIRM/CANCEL)
                        // Column 7: explanation (optional)

                        var patientName = fields[1].Trim();
                        var patientId = fields[2].Trim();
                        var dateString = fields[3].Trim();
                        var status = fields[6].Trim();
                        var explanation = fields.Length > 7 ? fields[7].Trim() : "";

                        // Parse date using the complex date string parser
                        var dateTimeResult = ParseDateTimeString(dateString);
                        if (!dateTimeResult.IsValid)
                        {
                            skippedReasons.Add($"Row {rowNumber}: {dateTimeResult.ErrorMessage}");
                            response.Skipped++;
                            continue;
                        }

                        // Filter: Only process 2020-2024 appointments
                        if (dateTimeResult.Date.Year < 2020 || dateTimeResult.Date.Year > 2024)
                        {
                            _logger.LogDebug("Skipping row {Row}: Date is not from 2020-2024 ({Date})",
                                rowNumber, dateTimeResult.Date.ToString("yyyy-MM-dd"));
                            continue;
                        }

                        // Validate required fields
                        if (string.IsNullOrWhiteSpace(patientName))
                        {
                            skippedReasons.Add($"Row {rowNumber}: Patient name is required");
                            response.Skipped++;
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(patientId))
                        {
                            skippedReasons.Add($"Row {rowNumber}: Patient ID is required");
                            response.Skipped++;
                            continue;
                        }

                        if (!status.Equals("CONFIRM", StringComparison.OrdinalIgnoreCase) &&
                            !status.Equals("CANCEL", StringComparison.OrdinalIgnoreCase))
                        {
                            skippedReasons.Add($"Row {rowNumber}: Invalid status '{status}'. Expected CONFIRM or CANCEL");
                            response.Skipped++;
                            continue;
                        }

                        // Try to find user by PatientId
                        var user = await _userRepository.GetUserByPatientIdAsync(patientId);

                        if (user == null)
                        {
                            patientNotFoundList.Add($"Row {rowNumber}: Patient '{patientName}' (ID: {patientId}) not found in system");
                            response.PatientNotFound++;
                            yearWiseStats[dateTimeResult.Date.Year].PatientsNotFound++;
                            _logger.LogInformation("Patient not found for row {Row}: {PatientId}", rowNumber, patientId);
                            continue;
                        }

                        // Null-safe access to user properties
                        var userPhoneNumber = user.PhoneNumber ?? "";
                        var userFirstName = user.FirstName ?? "Unknown";
                        var userLastName = user.LastName ?? "User";
                        var userHfId = user.HfId ?? "";

                        // Check if appointment already exists for this date/time
                        var existingAppointment = await CheckExistingAppointment(
                            request.ClinicId,
                            userPhoneNumber,
                            dateTimeResult.Date,
                            dateTimeResult.Time);

                        if (existingAppointment != null)
                        {
                            skippedReasons.Add($"Row {rowNumber}: Appointment already exists for {userFirstName} {userLastName} on {dateTimeResult.Date:dd-MM-yyyy} at {dateTimeResult.Time:hh\\:mm}");
                            response.AlreadyHasAppointment++;
                            continue;
                        }

                        // Create full name
                        var fullName = $"{userFirstName} {userLastName}".Trim();

                        // Get or create clinic patient
                        var clinicPatient = await _clinicVisitRepository.GetOrCreatePatientAsync(
                            userHfId,
                            fullName);

                        // Convert status: CONFIRM -> Completed, CANCEL -> Canceled
                        string appointmentStatus = status.Equals("CONFIRM", StringComparison.OrdinalIgnoreCase)
                            ? "Completed"
                            : "Canceled";

                        // Track year-wise statistics
                        yearWiseStats[dateTimeResult.Date.Year].TotalAppointments++;
                        if (appointmentStatus == "Completed")
                            yearWiseStats[dateTimeResult.Date.Year].Confirmed++;
                        else
                            yearWiseStats[dateTimeResult.Date.Year].Canceled++;

                        // Create appointment
                        var appointment = new ClinicAppointment
                        {
                            VisitorUsername = fullName,
                            VisitorPhoneNumber = userPhoneNumber,
                            AppointmentDate = dateTimeResult.Date.Date,
                            AppointmentTime = dateTimeResult.Time,
                            ClinicId = request.ClinicId,
                            Status = appointmentStatus,
                            Treatment = string.IsNullOrWhiteSpace(explanation) ? null : explanation
                        };

                        appointmentsToAdd.Add(appointment);

                        // Only create visit if appointment was confirmed
                        if (appointmentStatus == "Completed")
                        {
                            // Check if visit already exists for this date AND time
                            var existingVisit = await _clinicVisitRepository.GetExistingVisitAsyncWithTime(
                                clinicPatient.Id,
                                dateTimeResult.Date.Date,
                                dateTimeResult.Time);

                            if (existingVisit == null)
                            {
                                // Create clinic visit
                                var visit = new ClinicVisit
                                {
                                    ClinicPatientId = clinicPatient.Id,
                                    ClinicId = request.ClinicId,
                                    AppointmentDate = dateTimeResult.Date.Date,
                                    AppointmentTime = dateTimeResult.Time,
                                    PaymentMethod = null
                                };

                                visitsToAdd.Add(visit);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing row {Row}", rowNumber);
                        skippedReasons.Add($"Row {rowNumber}: Processing error - {ex.Message}");
                        response.Skipped++;
                    }
                }

                // Bulk insert appointments and visits
                if (appointmentsToAdd.Any())
                {
                    await _appointmentRepository.AddRangeAsync(appointmentsToAdd);
                    await _appointmentRepository.SaveChangesAsync();

                    // Save visits after appointments
                    foreach (var visit in visitsToAdd)
                    {
                        await _clinicVisitRepository.SaveVisitAsync(visit);
                    }

                    response.SuccessfullyAdded = appointmentsToAdd.Count;

                    // Build summary list
                    response.AddedAppointments = new List<ImportedAppointmentSummary>();
                    for (int i = 0; i < appointmentsToAdd.Count; i++)
                    {
                        var appointment = appointmentsToAdd[i];
                        var visit = visitsToAdd.FirstOrDefault(v =>
                            v.AppointmentDate == appointment.AppointmentDate &&
                            v.AppointmentTime == appointment.AppointmentTime);

                        response.AddedAppointments.Add(new ImportedAppointmentSummary
                        {
                            AppointmentId = appointment.Id,
                            VisitId = visit?.Id,
                            PatientName = appointment.VisitorUsername,
                            PatientId = "",
                            HFID = "",
                            PhoneNumber = appointment.VisitorPhoneNumber,
                            AppointmentDate = appointment.AppointmentDate.ToString("dd-MM-yyyy"),
                            AppointmentTime = appointment.AppointmentTime.ToString(@"hh\:mm"),
                            Status = appointment.Status,
                            //Treatment = appointment.Treatment ?? ""
                        });
                    }
                }

                await transaction.CommitAsync();
                committed = true;

                response.PatientNotFoundList = patientNotFoundList;
                response.SkippedReasons = skippedReasons;
                response.YearWiseBreakdown = yearWiseStats;

                // Build year-wise breakdown message
                var yearBreakdown = string.Join(", ",
                    yearWiseStats.Where(kvp => kvp.Value.TotalAppointments > 0)
                                .Select(kvp => $"{kvp.Key}: {kvp.Value.TotalAppointments} appointments ({kvp.Value.Confirmed} confirmed, {kvp.Value.Canceled} canceled)"));

                response.Message = $"Import completed: {response.SuccessfullyAdded} appointments added, " +
                                  $"{response.PatientNotFound} patients not found, " +
                                  $"{response.AlreadyHasAppointment} already have appointments, " +
                                  $"{response.Skipped} skipped out of {response.TotalProcessed} total 2020-2024 records. " +
                                  (yearBreakdown.Any() ? $"Year-wise breakdown - {yearBreakdown}" : "");

                _logger.LogInformation("2020-2024 Appointment import completed for Clinic {ClinicId}: {Added} added, {NotFound} not found, {Skipped} skipped",
                    request.ClinicId, response.SuccessfullyAdded, response.PatientNotFound, response.Skipped);

                // Invalidate cache
                _cacheService.InvalidateClinicStatistics(request.ClinicId);

                return Ok(ApiResponseFactory.Success(response, response.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import 2020-2024 appointments from CSV for Clinic {ClinicId}", request.ClinicId);
                return StatusCode(500, ApiResponseFactory.Fail("Failed to process CSV file: " + ex.Message));
            }
            finally
            {
                if (!committed && transaction?.GetDbTransaction()?.Connection != null)
                {
                    await transaction.RollbackAsync();
                }
            }
        }
    }
}

