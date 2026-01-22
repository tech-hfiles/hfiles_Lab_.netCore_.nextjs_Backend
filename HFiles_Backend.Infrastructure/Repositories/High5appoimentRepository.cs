using System.Text.Json;
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



		public async Task<List<PatientPackageHistoryDto>> GetMEPPackagesByPatientAsync(int patientId)
		{
			var records = await _context.ClinicPatientRecords
				.Where(r =>
					r.PatientId == patientId &&
					r.UniqueRecordId != null &&
					r.UniqueRecordId.StartsWith("MEP"))
				.OrderByDescending(r => r.EpochTime)
				.ToListAsync();

			var appointments = await _context.High5Appointments
				.Include(a => a.CoachMember)            // ✅ Load Coach
					.ThenInclude(c => c.User)           // ✅ Then load User from Coach
				.Where(a => a.PatientId == patientId)
				.OrderBy(a => a.PackageDate)
				.ToListAsync();

			var result = new List<PatientPackageHistoryDto>();

			foreach (var rec in records)
			{
				try
				{
					using var json = JsonDocument.Parse(rec.JsonData);
					var root = json.RootElement;
					var patientObj = root.GetProperty("patient");

					foreach (var treatmentObj in root.GetProperty("treatments").EnumerateArray())
					{
						var packageName = treatmentObj.GetProperty("name").GetString();


						// ✅ MATCH FLEXIBLY - UniqueRecordId is optional
						var packageAppointments = appointments
							.Where(a =>
								a.PackageName == packageName &&
								(string.IsNullOrEmpty(a.UniqueRecordId) ||
								 a.UniqueRecordId.Equals(rec.UniqueRecordId, StringComparison.OrdinalIgnoreCase)))
							.ToList();
						var latestAppointment = packageAppointments
						.OrderByDescending(a => a.PackageDate)
						.FirstOrDefault();

						// ✅ GET CURRENT COACH NAME FROM MOST RECENT APPOINTMENT
						var coachName = latestAppointment?.CoachMember?.User != null
						? $"{latestAppointment.CoachMember.User.FirstName} {latestAppointment.CoachMember.User.LastName}".Trim()
						: (treatmentObj.TryGetProperty("coach", out var c)
						? c.GetString()	
						: "Not Assigned");

						var package = new PatientPackageHistoryDto
						{
							UniqueRecordId = rec.UniqueRecordId,
							PatientName = patientObj.TryGetProperty("name", out var n) ? n.GetString() : "",
							CoachName = coachName, // ✅ FROM APPOINTMENTS TABLE
							HFID = patientObj.TryGetProperty("hfid", out var h)
								? h.GetString()
								: patientObj.TryGetProperty("uhid", out var u) ? u.GetString() : "",
							PackageName = packageName,
							StartDate = treatmentObj.TryGetProperty("startDate", out var sd) ? sd.GetString() : "",
							EndDate = treatmentObj.TryGetProperty("endDate", out var ed) ? ed.GetString() : "",
							Sessions = packageAppointments.Select(a => new SessionDetail
							{
								Date = a.PackageDate.ToString("yyyy-MM-dd"),
								Time = a.PackageDate.ToString("HH:mm:ss"),
								Status = (int)a.Status,
								AppointmentId = a.Id
							}).ToList()
						};

						result.Add(package);
					}
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error parsing JSON for Record {RecordId}", rec.UniqueRecordId);
				}
			}

			return result;
		}

		public async Task<bool> IsDuplicateAppointmentAsync(int excludeId, int userId, int? packageId, DateTime date)
		{
			// The query lives inside the service because the service has access to '_context'
			return await _context.High5Appointments.AnyAsync(a =>
				a.UserId == userId &&
				a.PackageId == packageId &&
				a.PackageDate.Date == date.Date &&
				a.Id != excludeId);
		}
		public async Task<List<PackageAppointmentDetailDto>> GetAppointmentsByRecordIdAsync(string uniqueRecordId)
		{
			// The query lives here because the service has access to the Database Context (_context)
			return await (from app in _context.High5Appointments
						  join rec in _context.ClinicPatientRecords
						  on app.UniqueRecordId equals rec.UniqueRecordId
						  where app.UniqueRecordId == uniqueRecordId
						  select new PackageAppointmentDetailDto
						  {
							  AppointmentId = app.Id,
							  PackageName = app.PackageName,
							  PackageDate = app.PackageDate,
							  PackageTime = app.PackageTime,
							  Status = (int)app.Status,
							  UniqueRecordId = app.UniqueRecordId,
							  PatientName = app.PackageName // Or pull more data if needed
						  })
						  .OrderBy(a => a.PackageDate)
						  .ToListAsync();
		}
	}
}