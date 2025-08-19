using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace HFiles_Backend.Infrastructure.Repositories
{
    public class AppointmentRepository(AppDbContext context, ILogger<AppointmentRepository> logger) : IAppointmentRepository
    {
        private readonly AppDbContext _context = context;
        private readonly ILogger<AppointmentRepository> _logger = logger;

        public async Task SaveAppointmentAsync(ClinicAppointment appointment)
        {
            try
            {
                await _context.ClinicAppointments.AddAsync(appointment);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save appointment");
                throw;
            }
        }
    }
}
