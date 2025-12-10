using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sprache;

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
        public async Task<ClinicAppointment?> GetAppointmentByIdAsync(int appointmentId, int clinicId)
        {
            return await _context.ClinicAppointments
                .FirstOrDefaultAsync(a => a.Id == appointmentId && a.ClinicId == clinicId);
        }

        public async Task<List<ClinicAppointment>> GetAppointmentsByClinicIdAsync(int clinicId)
        {
            return await _context.ClinicAppointments
            .AsNoTracking()
            .Where(a => a.ClinicId == clinicId)
            .Select(a => new ClinicAppointment
            {
                Id = a.Id,
                VisitorUsername = a.VisitorUsername,
                VisitorPhoneNumber = a.VisitorPhoneNumber,
                AppointmentDate = a.AppointmentDate,
                AppointmentTime = a.AppointmentTime,
                ClinicId = a.ClinicId,
                Status = a.Status,
                Treatment = a.Treatment
            })
            .OrderByDescending(a => a.AppointmentDate)
            .ThenByDescending(a => a.AppointmentTime)
            .ToListAsync();
        }

        public async Task<ClinicAppointment?> GetByIdAsync(int appointmentId)
        {
            return await _context.ClinicAppointments
                .FirstOrDefaultAsync(a => a.Id == appointmentId);
        }


        public async Task DeleteAsync(ClinicAppointment appointment)
        {
            _context.ClinicAppointments.Remove(appointment);
            _logger.LogInformation("Marked appointment ID {AppointmentId} for deletion", appointment.Id);
            await Task.CompletedTask; 
        }

        public async Task<int> MarkOverdueAppointmentsAsAbsentAsync()
        {
            var now = DateTime.UtcNow;
            var today = now.Date;

            // Only pick appointments scheduled for today
            var candidates = await _context.ClinicAppointments
                .Where(a => a.Status == "Scheduled")
                .Where(a => a.AppointmentDate.Date == today)
                .OrderBy(a => a.AppointmentDate) // helps index usage
                .Take(500)                       // batch size
                .ToListAsync();

            var cutoff = now.Subtract(TimeSpan.FromMinutes(15));

            var overdueAppointments = candidates
                .Where(a => a.AppointmentDate.Date.Add(a.AppointmentTime) <= cutoff)
                .ToList();

            foreach (var appointment in overdueAppointments)
            {
                appointment.Status = "Absent";
            }

            await _context.SaveChangesAsync();
            return overdueAppointments.Count;
        }

        public async Task AddRangeAsync(IEnumerable<ClinicAppointment> appointments)
        {
            await _context.ClinicAppointments.AddRangeAsync(appointments);
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }
        public async Task UpdateAsync(ClinicAppointment appointment)
        {
            _context.ClinicAppointments.Update(appointment);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Updated appointment ID {AppointmentId}", appointment.Id);
        }
    }
}
