using HFiles_Backend.API.Interfaces;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Clinics.Appointment;
using HFiles_Backend.Application.DTOs.Clinics.Treatment;
using HFiles_Backend.Domain.Entities.Clinics;
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
    IClinicVisitRepository clinicVisitRepository
    ) : ControllerBase
    {
        private readonly IAppointmentRepository _appointmentRepository = appointmentRepository;
        private readonly IClinicAuthorizationService _clinicAuthorizationService = clinicAuthorizationService;
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IClinicRepository _clinicRepository = clinicRepository;
        private readonly ILogger<AppointmentsController> _logger = logger;
        private readonly IClinicVisitRepository _clinicVisitRepository = clinicVisitRepository;





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
                await transaction.CommitAsync();
                committed = true;

                var response = new
                {
                    appointment.Id,
                    appointment.ClinicId,
                    appointment.VisitorUsername,
                    appointment.VisitorPhoneNumber,
                    AppointmentDate = appointment.AppointmentDate.ToString("dd-MM-yyyy"),
                    AppointmentTime = appointment.AppointmentTime.ToString(@"hh\:mm"),
                    appointment.Status
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
        public async Task<IActionResult> GetAppointmentsByClinicId([FromRoute] int clinicId)
        {
            HttpContext.Items["Log-Category"] = "Clinic Appointment";

            bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
            if (!isAuthorized)
            {
                _logger.LogWarning("Unauthorized access attempt for Clinic ID {ClinicId}", clinicId);
                return Unauthorized(ApiResponseFactory.Fail("You are not authorized to view appointments for this clinic."));
            }

            var appointments = await _appointmentRepository.GetAppointmentsByClinicIdAsync(clinicId);
            var visits = await _clinicVisitRepository.GetVisitsByClinicIdAsync(clinicId);

            if (appointments == null || !appointments.Any())
            {
                _logger.LogInformation("No appointments found for Clinic ID {ClinicId}", clinicId);
                return Ok(ApiResponseFactory.Success(new
                {
                    Appointments = new List<object>(),
                    TotalAppointmentsToday = 0,
                    MissedAppointmentsToday = 0
                }, "No appointments found."));
            }

            var today = DateTime.Today;

            // Build a lookup from VisitId to HFID
            var visitHfidLookup = visits
               .Where(v => v.Patient != null)
               .ToLookup(v => new
               {
                   v.ClinicId,
                   v.AppointmentDate,
                   v.AppointmentTime
               }, v => v.Patient.HFID);


            var response = appointments.Select(a =>
            {
                var hfid = visitHfidLookup[new
                {
                    ClinicId = a.ClinicId,
                    AppointmentDate = a.AppointmentDate.Date,
                    AppointmentTime = a.AppointmentTime
                }].FirstOrDefault() ?? string.Empty;

                return new
                {
                    a.Id,
                    a.ClinicId,
                    a.VisitorUsername,
                    a.VisitorPhoneNumber,
                    AppointmentDate = a.AppointmentDate.ToString("dd-MM-yyyy"),
                    AppointmentTime = a.AppointmentTime.ToString(@"hh\:mm"),
                    a.Treatment,
                    a.Status,
                    HFID = hfid
                };
            }).ToList();

            int totalAppointmentsToday = appointments.Count(a => a.AppointmentDate.Date == today);
            int missedAppointmentsToday = appointments.Count(a => a.AppointmentDate.Date == today && a.Status == "Absent");

            _logger.LogInformation("Fetched {Count} appointments for Clinic ID {ClinicId}", response.Count, clinicId);

            return Ok(ApiResponseFactory.Success(new
            {
                Appointments = response,
                TotalAppointmentsToday = totalAppointmentsToday,
                MissedAppointmentsToday = missedAppointmentsToday
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
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
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
                    _logger.LogWarning("Appointment not found for ID {AppointmentId} in Clinic ID {ClinicId}", appointmentId, clinicId);
                    return NotFound(ApiResponseFactory.Fail("Appointment not found."));
                }

                var now = DateTime.Now;
                var appointmentDateTime = appointment.AppointmentDate.Date + appointment.AppointmentTime;

                if (dto.Status == "Canceled")
                {
                    if (appointmentDateTime <= now)
                        return BadRequest(ApiResponseFactory.Fail("Cannot cancel past or ongoing appointments."));
                }
                else if (dto.Status == "Completed")
                {
                    if (appointment.AppointmentDate.Date != now.Date || appointmentDateTime > now)
                        return BadRequest(ApiResponseFactory.Fail("Can only mark as completed if appointment is today and time has passed."));

                    appointment.Treatment = dto.Treatment;
                }

                if (!string.IsNullOrWhiteSpace(dto.Status))
                {
                    appointment.Status = dto.Status;
                }
                await _clinicRepository.SaveChangesAsync();
                await transaction.CommitAsync();
                committed = true;

                var response = new
                {
                    appointment.Id,
                    appointment.ClinicId,
                    appointment.VisitorUsername,
                    appointment.VisitorPhoneNumber,
                    AppointmentDate = appointment.AppointmentDate.ToString("dd-MM-yyyy"),
                    AppointmentTime = appointment.AppointmentTime.ToString(@"hh\:mm"),
                    appointment.Status,
                    appointment.Treatment
                };

                _logger.LogInformation("Appointment ID {AppointmentId} status updated to {Status}", appointmentId, dto.Status);
                return Ok(ApiResponseFactory.Success(response, "Appointment status updated successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while updating appointment status for ID {AppointmentId}", appointmentId);
                return StatusCode(500, ApiResponseFactory.Fail("Unexpected error occurred while updating appointment status."));
            }
            finally
            {
                if (!committed && transaction.GetDbTransaction().Connection != null)
                    await transaction.RollbackAsync();
            }
        }





        // Delete Appointment based on appointment Id
        [HttpDelete("appointments/{appointmentId:int}")]
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

                transaction = await _clinicRepository.BeginTransactionAsync();

                await _appointmentRepository.DeleteAsync(appointment);
                await _clinicRepository.SaveChangesAsync();
                await transaction.CommitAsync();
                committed = true;

                _logger.LogInformation("Deleted appointment ID {AppointmentId} from Clinic ID {ClinicId}", appointmentId, appointment.ClinicId);
                return Ok(ApiResponseFactory.Success("Appointment deleted successfully."));
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

                await transaction.CommitAsync();
                committed = true;

                var response = new
                {
                    PatientName = patient.PatientName,
                    HFID = patient.HFID,
                    AppointmentDate = date.ToString("dd-MM-yyyy"),
                    AppointmentTime = time.ToString(@"hh\:mm"),
                    ConsentFormsSent = consentForms.Select(f => f.Title).ToList(),
                    Treatment = appointment.Treatment,
                    AppointmentStatus = appointment.Status,
                    ClinicId = clinicId
                };

                _logger.LogInformation("Follow-up appointment created for HFID {HFID} and ClinicId {ClinicId}", dto.HFID, clinicId);
                return Ok(ApiResponseFactory.Success(response, "Follow-up appointment and clinic appointment saved successfully."));
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

                var response = new
                {
                    HFID = dto.HFID,
                    PatientName = fullName,
                    AppointmentDate = date.ToString("dd-MM-yyyy"),
                    AppointmentTime = time.ToString(@"hh\:mm"),
                    AppointmentStatus = appointment.Status,
                    ClinicId = clinicId
                };

                _logger.LogInformation("Follow-up appointment booked for existing patient HFID {HFID} in Clinic ID {ClinicId}", dto.HFID, clinicId);
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
         [FromServices] ClinicPatientRecordRepository recordRepository)
        {
            HttpContext.Items["Log-Category"] = "Clinic Patient Overview";

            bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
            if (!isAuthorized)
            {
                _logger.LogWarning("Unauthorized appointment creation attempt for Clinic ID {ClinicId}", clinicId);
                return Unauthorized(ApiResponseFactory.Fail("Only main or branch clinics can create appointments."));
            }

            try
            {
                DateTime? start = null;
                DateTime? end = null;
                DateTime parsedStart;
                DateTime parsedEnd;

                if (!string.IsNullOrEmpty(startDate)) { if (!DateTime.TryParseExact(startDate, "dd-MM-yyyy", null, DateTimeStyles.None, out parsedStart)) { return BadRequest(ApiResponseFactory.Fail("Invalid startDate format. Expected dd-MM-yyyy.")); } start = parsedStart; }
                if (!string.IsNullOrEmpty(endDate)) { if (!DateTime.TryParseExact(endDate, "dd-MM-yyyy", null, DateTimeStyles.None, out parsedEnd)) { return BadRequest(ApiResponseFactory.Fail("Invalid endDate format. Expected dd-MM-yyyy.")); } end = parsedEnd; }

                var patients = await clinicRepository.GetClinicPatientsWithVisitsAsync(clinicId);

                var filteredPatients = new List<PatientDto>();

                foreach (var patient in patients)
                {
                    var lastVisit = patient.Visits.OrderByDescending(v => v.AppointmentDate).FirstOrDefault();
                    if (lastVisit == null) continue;

                    if (start.HasValue && lastVisit.AppointmentDate.Date < start.Value.Date) continue;
                    if (end.HasValue && lastVisit.AppointmentDate.Date > end.Value.Date) continue;

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

                    var dto = new PatientDto
                    {
                        PatientId = patient.Id,
                        PatientName = patient.PatientName,
                        HFID = patient.HFID,
                        LastVisitDate = lastVisit.AppointmentDate.ToString("dd-MM-yyyy"),
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
    }
}
