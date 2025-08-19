using HFiles_Backend.API.Interfaces;
using HFiles_Backend.API.Services;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Clinics.Appointment;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;

namespace HFiles_Backend.API.Controllers.Clinics
{
    [ApiController]
    [Route("api/appointments")]
    public class AppointmentsController(
    IAppointmentRepository appointmentRepository, 
    IClinicAuthorizationService clinicAuthorizationService,
    IUserRepository userRepository,
    IClinicRepository clinicRepository,
    ILogger<AppointmentsController> logger
    ) : ControllerBase
    {
        private readonly IAppointmentRepository _appointmentRepository = appointmentRepository;
        private readonly IClinicAuthorizationService _clinicAuthorizationService = clinicAuthorizationService;
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IClinicRepository _clinicRepository = clinicRepository;
        private readonly ILogger<AppointmentsController> _logger = logger;





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
    }
}
