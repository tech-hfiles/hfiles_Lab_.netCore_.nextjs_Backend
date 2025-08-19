using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
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
        public async Task<ClinicAppointment?> GetAppointmentByIdAsync(int id)
        {
            return await _context.ClinicAppointments
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id);
        }
        public async Task<List<ClinicAppointment>> GetAppointmentsByClinicIdAsync(int clinicId)
        {
            return await _context.ClinicAppointments
                .AsNoTracking()
                .Where(a => a.ClinicId == clinicId)
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.AppointmentTime)
                .ToListAsync();
        }
    }
}
