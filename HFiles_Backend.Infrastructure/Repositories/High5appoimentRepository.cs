using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Enums;
using HFiles_Backend.Domain.Interfaces.Clinics;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HFiles_Backend.Infrastructure.Repositories
{
	public class High5AppointmentRepository : IClinicHigh5AppointmentService
	{
		private readonly AppDbContext _context;
		private readonly ILogger<High5AppointmentRepository> _logger;

		public High5AppointmentRepository(
			AppDbContext context,
			ILogger<High5AppointmentRepository> logger)
		{
			_context = context;
			_logger = logger;
		}

		// ================= Create =================
		public async Task<High5Appointment> CreateAppointmentAsync(High5Appointment appointment)
		{
			try
			{
				await _context.High5Appointments.AddAsync(appointment);
				await _context.SaveChangesAsync();
				_logger.LogInformation(
					"Created High5 appointment ID {AppointmentId}",
					appointment.Id
				);
				return appointment;
			}
			catch (Exception ex)
			{
				_logger.LogError(
					ex,
					"Failed to create High5 appointment for Clinic ID {ClinicId}",
					appointment.ClinicId
				);
				throw;
			}
		}

		// ================= Read =================
		public async Task<High5Appointment?> GetAppointmentByIdAsync(int appointmentId)
		{
			return await _context.High5Appointments
				.AsNoTracking()
				.FirstOrDefaultAsync(a => a.Id == appointmentId);
		}

		public async Task<IEnumerable<High5Appointment>> GetAppointmentsByClinicIdAsync(int clinicId)
		{
			return await _context.High5Appointments
				.AsNoTracking()
				.Where(a => a.ClinicId == clinicId)
				.OrderByDescending(a => a.EpochTime)
				.ToListAsync();
		}

		// NEW METHOD - Add this
		public async Task<List<High5Appointment>> GetAppointmentsByClinicIdWithUserAsync(int clinicId)
		{
			return await _context.High5Appointments
				.Include(h => h.User)                        // Load Patient User
				.Include(h => h.CoachMember)                 // Load ClinicMember (Coach)
					.ThenInclude(c => c.User)                // Then load User from ClinicMember
				.Where(h => h.ClinicId == clinicId)
				.OrderByDescending(h => h.EpochTime)
				.ToListAsync();
		}

		public async Task<IEnumerable<High5Appointment>> GetAppointmentsByUserIdAsync(int userId)
		{
			return await _context.High5Appointments
				.AsNoTracking()
				.Where(a => a.UserId == userId)
				.OrderByDescending(a => a.EpochTime)
				.ToListAsync();
		}

		public async Task<IEnumerable<High5Appointment>> GetAllAppointmentsAsync()
		{
			return await _context.High5Appointments
				.AsNoTracking()
				.OrderByDescending(a => a.EpochTime)
				.ToListAsync();
		}

		// ================= Update =================
		public async Task<bool> UpdateAppointmentAsync(High5Appointment appointment)
		{
			try
			{
				_context.High5Appointments.Update(appointment);
				await _context.SaveChangesAsync();
				_logger.LogInformation(
					"Updated High5 appointment ID {AppointmentId}",
					appointment.Id
				);
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(
					ex,
					"Failed to update High5 appointment ID {AppointmentId}",
					appointment.Id
				);
				return false;
			}
		}
	}
}