using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Enums;
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

		// In your repository
		public async Task<IEnumerable<ClinicEnquiry>> GetAllAsync(int clinicId)
		{
			return await _context.clinicEnquiry
				.Where(e => e.ClinicId == clinicId)
				.Include(e => e.AssignedCoaches)
					.ThenInclude(ac => ac.ClinicMember)
						.ThenInclude(cm => cm.User)
				.Include(e => e.PricingPackage) // ✅ ADD THIS LINE

				.ToListAsync();
		}


		public async Task<(List<ClinicEnquiry> enquiries, int totalCount)> GetFilteredEnquiriesAsync(
	int clinicId,
	List<EnquiryStatus>? status,
	PaymentStatus? paymentStatus,
	int? coachId,
	string? search,
	DateTime? startDate,
	DateTime? endDate,
	int page,
	int pageSize)
		{
			var query = _context.clinicEnquiry
				.Where(e => e.ClinicId == clinicId && e.Status != EnquiryStatus.Member)
				.AsQueryable();

			// ✅ Apply filters in SQL (not in memory)
			if (status != null && status.Any())
				query = query.Where(e => status.Contains(e.Status));

			if (paymentStatus.HasValue)
				query = query.Where(e => e.Payment == paymentStatus.Value);

			if (coachId.HasValue)
				query = query.Where(e => e.AssignedCoaches.Any(ac => ac.CoachId == coachId.Value));

			// Date filters
			if (startDate.HasValue)
			{
				query = query.Where(e => e.EpochTime > 0 &&
					DateTimeOffset.FromUnixTimeSeconds(e.EpochTime).DateTime.Date >= startDate.Value.Date);
			}

			if (endDate.HasValue)
			{
				query = query.Where(e => e.EpochTime > 0 &&
					DateTimeOffset.FromUnixTimeSeconds(e.EpochTime).DateTime.Date <= endDate.Value.Date);
			}

			// Search filter
			if (!string.IsNullOrWhiteSpace(search))
			{
				var keyword = search.Trim().ToLower();
				query = query.Where(e =>
					(e.Firstname != null && e.Firstname.ToLower().StartsWith(keyword)) ||
					(e.Lastname != null && e.Lastname.ToLower().StartsWith(keyword)) ||
					(e.Firstname != null && e.Lastname != null &&
					 (e.Firstname + " " + e.Lastname).ToLower().StartsWith(keyword))
				);
			}

			// ✅ Get count BEFORE loading data
			var totalCount = await query.CountAsync();

			// ✅ Load ONLY the records for current page
			var today = DateTime.Today;
			var enquiries = await query
				.Include(e => e.PricingPackage)
				.Include(e => e.AssignedCoaches)
					.ThenInclude(ac => ac.ClinicMember)
						.ThenInclude(cm => cm.User)
				.OrderByDescending(e => e.FollowUpDate.HasValue && e.FollowUpDate.Value.Date == today)
				.ThenByDescending(e => e.FollowUpDate)
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.ToListAsync();

			return (enquiries, totalCount);
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
		public async Task SyncCoachesAsync(int enquiryId, List<int> coachIds)
		{
			var existing = await _context.ClinicEnquiryCoaches
				.Where(x => x.EnquiryId == enquiryId)
				.ToListAsync();

			var existingIds = existing.Select(x => x.CoachId).ToHashSet();

			// ➕ ADD new coaches
			var toAdd = coachIds
				.Where(id => !existingIds.Contains(id))
				.Select(id => new ClinicEnquiryCoach
				{
					EnquiryId = enquiryId,
					CoachId = id,
					EpochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
				});

			// ❌ DELETE removed coaches
			var toDelete = existing
				.Where(x => !coachIds.Contains(x.CoachId));

			_context.ClinicEnquiryCoaches.AddRange(toAdd);
			_context.ClinicEnquiryCoaches.RemoveRange(toDelete);

			await _context.SaveChangesAsync();
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
