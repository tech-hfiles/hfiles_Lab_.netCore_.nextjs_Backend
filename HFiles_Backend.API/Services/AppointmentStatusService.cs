using HFiles_Backend.Domain.Interfaces;

namespace HFiles_Backend.API.Services
{
    public class AppointmentStatusService(IAppointmentRepository appointmentRepository, ILogger<AppointmentStatusService> logger)
    {
        private readonly IAppointmentRepository _appointmentRepository = appointmentRepository;
        private readonly ILogger<AppointmentStatusService> _logger = logger;

        public async Task SweepAbsentAppointmentsAsync()
        {
            var updatedCount = await _appointmentRepository.MarkOverdueAppointmentsAsAbsentAsync();
            _logger.LogInformation("Marked {Count} overdue appointments as Absent", updatedCount);
        }
    }
}
