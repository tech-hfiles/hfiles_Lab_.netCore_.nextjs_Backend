using HFiles_Backend.API.Services;
using HFiles_Backend.Application.DTOs.Clinics.Appointment;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HFiles_Backend.API.Controllers.Clinics
{
    [ApiController]
    [Route("api/appointments")]
    public class AppointmentsController(IAppointmentRepository appointmentRepository, ClinicAuthorizationService clinicAuthorizationService) : ControllerBase
    {
        private readonly IAppointmentRepository _appointmentRepository = appointmentRepository;
        private readonly ClinicAuthorizationService _clinicAuthorizationService = clinicAuthorizationService;

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateAppointment([FromBody] AppointmentCreateDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(dto.ClinicId, User);
            if (!isAuthorized)
                return Unauthorized("Only main or branch clinics can create appointments.");

            // Parse date and time safely
            if (!DateTime.TryParseExact(dto.AppointmentDate, "dd-MM-yyyy", null, System.Globalization.DateTimeStyles.None, out var date))
                return BadRequest("Invalid AppointmentDate format. Expected dd-MM-yyyy.");

            if (!TimeSpan.TryParse(dto.AppointmentTime, out var time))
                return BadRequest("Invalid AppointmentTime format. Expected HH:mm.");

            var appointment = new ClinicAppointment
            {
                ClinicId = dto.ClinicId,
                VisitorUsername = dto.VisitorUsername,
                VisitorPhoneNumber = dto.VisitorPhoneNumber,
                AppointmentDate = date.Date,
                AppointmentTime = time
            };

            await _appointmentRepository.SaveAppointmentAsync(appointment);

            return Ok("Appointment saved successfully.");
        }


    }
}
