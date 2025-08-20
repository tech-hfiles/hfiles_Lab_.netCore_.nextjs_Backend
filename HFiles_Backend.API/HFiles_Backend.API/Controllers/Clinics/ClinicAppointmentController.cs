using HFiles_Backend.API.Interfaces;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Clinics.Appointment;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;
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
            if (appointments == null || !appointments.Any())
            {
                _logger.LogInformation("No appointments found for Clinic ID {ClinicId}", clinicId);
                return Ok(ApiResponseFactory.Success(new List<object>(), "No appointments found."));
            }

            var response = new List<object>();

            foreach (var appointment in appointments)
            {
                var user = await _userRepository.GetByPhoneNumberAsync(appointment.VisitorPhoneNumber);

                response.Add(new
                {
                    appointment.Id,
                    appointment.ClinicId,
                    appointment.VisitorUsername,
                    appointment.VisitorPhoneNumber,
                    AppointmentDate = appointment.AppointmentDate.ToString("dd-MM-yyyy"),
                    AppointmentTime = appointment.AppointmentTime.ToString(@"hh\:mm"),
                    appointment.Treatment,
                    appointment.Status,
                    HFID = user?.HfId
                });
            }

            _logger.LogInformation("Fetched {Count} appointments for Clinic ID {ClinicId}", response.Count, clinicId);
            return Ok(ApiResponseFactory.Success(response, "Appointments fetched successfully."));
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





        // Add patient and appointment follow ups
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
                // Authorization check
                bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
                if (!isAuthorized)
                {
                    _logger.LogWarning("Unauthorized appointment creation attempt for Clinic ID {ClinicId}", clinicId);
                    return Unauthorized(ApiResponseFactory.Fail("Only main or branch clinics can create appointments."));
                }

                // Validate clinic existence to avoid FK errors
                var clinicExists = await _clinicRepository.ExistsAsync(clinicId);
                if (!clinicExists)
                {
                    _logger.LogWarning("Clinic ID {ClinicId} does not exist in clinicsignups", clinicId);
                    return BadRequest(ApiResponseFactory.Fail("Invalid Clinic ID."));
                }

                // Resolve user by HFID
                var user = await _userRepository.GetUserByHFIDAsync(dto.HFID);
                if (user == null)
                {
                    _logger.LogWarning("No user found for HFID {HFID}", dto.HFID);
                    return NotFound(ApiResponseFactory.Fail("No user found for provided HFID."));
                }

                var fullName = $"{user.FirstName} {user.LastName}";
                var phone = user.PhoneNumber ?? "N/A";

                // Resolve or create patient
                var patient = await _clinicVisitRepository.GetOrCreatePatientAsync(dto.HFID, fullName);

                // Resolve consent forms
                var consentForms = await _clinicVisitRepository.GetConsentFormsByTitlesAsync(dto.ConsentFormTitles);
                if (consentForms.Count != dto.ConsentFormTitles.Count)
                {
                    var missing = dto.ConsentFormTitles.Except(consentForms.Select(f => f.Title)).ToList();
                    return BadRequest(ApiResponseFactory.Fail($"Invalid consent form titles: {string.Join(", ", missing)}"));
                }

                // Save visit with ClinicId included
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

                // Save appointment
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





        // Fetch CLinic Patients
        [HttpGet("clinics/{clinicId}/patients")]
        [Authorize]
        public async Task<IActionResult> GetClinicPatients([FromRoute] int clinicId, [FromServices] ClinicRepository clinicRepository)
        {
            HttpContext.Items["Log-Category"] = "Clinic Patient Overview";

            // Authorization: Ensure clinicId belongs to main clinic or its branches
            bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
            if (!isAuthorized)
            {
                _logger.LogWarning("Unauthorized appointment creation attempt for Clinic ID {ClinicId}", clinicId);
                return Unauthorized(ApiResponseFactory.Fail("Only main or branch clinics can create appointments."));
            }

            try
            {
                var patients = await clinicRepository.GetClinicPatientsWithVisitsAsync(clinicId);

                var response = new ClinicPatientResponseDto
                {
                    TotalPatients = patients.Count,
                    Patients = patients.Select(p => new PatientDto
                    {
                        PatientId = p.Id,
                        PatientName = p.PatientName,
                        HFID = p.HFID,
                        LastVisitDate = p.Visits
                            .OrderByDescending(v => v.AppointmentDate)
                            .FirstOrDefault()?.AppointmentDate.ToString("dd-MM-yyyy"),
                        Visits = p.Visits.Select(v => new VisitDto
                        {
                            VisitId = v.Id,
                            AppointmentDate = v.AppointmentDate.ToString("dd-MM-yyyy"),
                            AppointmentTime = v.AppointmentTime.ToString(@"hh\:mm"),
                            ConsentFormsSent = v.ConsentFormsSent.Select(cf => cf.ConsentForm.Title).ToList()
                        }).OrderByDescending(v => v.AppointmentDate).ToList()
                    }).ToList()
                };

                return Ok(ApiResponseFactory.Success(response, "Clinic patient data retrieved successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while fetching patient data for Clinic ID {ClinicId}", clinicId);
                return StatusCode(500, ApiResponseFactory.Fail("Unexpected error occurred while retrieving patient data."));
            }
        }
    }
}
