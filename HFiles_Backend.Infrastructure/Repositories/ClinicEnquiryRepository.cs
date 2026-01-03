using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HFiles_Backend.Infrastructure.Repositories
{
	public class ClinicEnquiryRepository : IClinicEnquiryRepository
	{
		private readonly AppDbContext _context;
		private readonly ILogger<ClinicEnquiryRepository> _logger;

		public ClinicEnquiryRepository(
			AppDbContext context,
			ILogger<ClinicEnquiryRepository> logger)
		{
			_context = context;
			_logger = logger;
		}

		// =====================================================
		// ADD
		// =====================================================
		public async Task AddAsync(ClinicEnquiry enquiry)
		{
			try
			{
				await _context.clinicEnquiry.AddAsync(enquiry);
				await _context.SaveChangesAsync();

				_logger.LogInformation(
					"Created new enquiry ID {EnquiryId}",
					enquiry.Id
				);
			}
			catch (Exception ex)
			{
				_logger.LogError(
					ex,
					"Failed to create enquiry for Clinic ID {ClinicId}",
					enquiry.ClinicId
				);
				throw;
			}
		}

		public async Task<List<ClinicEnquiry>> GetAllAsync(int clinicId)
		{
			return await _context.clinicEnquiry
				.AsNoTracking()
				.Where(e => e.ClinicId == clinicId)
				.OrderByDescending(e => e.EpochTime)
				.ToListAsync();
		}

		// =====================================================
		// GET BY ID
		// =====================================================
		public async Task<ClinicEnquiry?> GetByIdAsync(int id)
		{
			return await _context.clinicEnquiry
				.Include(e => e.AssignedCoaches)           // Include junction table
					//.ThenInclude(ec => ec.CoachMember)     // Include clinicmembers
						//.ThenInclude(cm => cm.User)        // Include users
				.FirstOrDefaultAsync(e => e.Id == id);
		}

		// =====================================================
		// UPDATE
		// =====================================================
		public async Task UpdateAsync(ClinicEnquiry enquiry)
		{
			try
			{
				_context.clinicEnquiry.Update(enquiry);
				await _context.SaveChangesAsync();

				_logger.LogInformation(
					"Updated enquiry ID {EnquiryId}",
					enquiry.Id
				);
			}
			catch (Exception ex)
			{
				_logger.LogError(
					ex,
					"Failed to update enquiry ID {EnquiryId}",
					enquiry.Id
				);
				throw;
			}
		}

	
		public async Task<List<ClinicEnquiry>> GetTodayAppointmentsAsync(
			int clinicId,
			DateTime today)
		{
			var startDate = today.Date;
			var endDate = startDate.AddDays(1);

			return await _context.clinicEnquiry
				.AsNoTracking()
				.Where(e =>
					e.ClinicId == clinicId &&
					e.AppointmentDate.HasValue &&
					e.AppointmentDate.Value >= startDate &&
					e.AppointmentDate.Value < endDate
				)
				.OrderBy(e => e.AppointmentTime)
				.ToListAsync();
		}

		public async Task AssignCoachAsync(int enquiryId, int coachId)
		{
			try
			{
				// Optional: Remove existing assignments for this enquiry
				var existing = await _context.ClinicEnquiryCoaches
					.Where(x => x.EnquiryId == enquiryId)
					.ToListAsync();

				if (existing.Any())
					_context.ClinicEnquiryCoaches.RemoveRange(existing);

				// Validate that the coach exists
				var validCoach = await _context.ClinicMembers
					.FirstOrDefaultAsync(c => c.Id == coachId);

				if (validCoach == null)
					throw new Exception($"Invalid Coach ID: {coachId}");

				// Create new mapping
				var record = new ClinicEnquiryCoach
				{
					EnquiryId = enquiryId,
					CoachId = coachId,
					EpochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
				};

				await _context.ClinicEnquiryCoaches.AddAsync(record);
				await _context.SaveChangesAsync();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to assign coach for EnquiryId {EnquiryId}", enquiryId);
				throw;
			}
		}

		// Get all coaches assigned to a specific enquiry
		public async Task<List<ClinicEnquiryCoach>> GetEnquiryCoachesAsync(int enquiryId)
		{
			return await _context.ClinicEnquiryCoaches
				.AsNoTracking()
				.Include(ec => ec.ClinicMember)         // Include clinic member details
					.ThenInclude(cm => cm.User)        // Include linked user info if applicable
				.Where(ec => ec.EnquiryId == enquiryId)
				.ToListAsync();
		}


	}
}
