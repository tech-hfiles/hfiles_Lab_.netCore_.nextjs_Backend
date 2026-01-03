
using HFiles_Backend.API.Controllers.Clinics;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Domain.DTOs.Clinics;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Enums;
using HFiles_Backend.Domain.Interfaces.Clinics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace HFiles_Backend.API.Controllers.Clinics
{

	[ApiController]
	[Route("api/clinics/high5-appointments")]
	public class High5AppointmentController : ControllerBase
	{
		private readonly IClinicHigh5AppointmentService _appointmentService;
		private readonly ILogger<High5AppointmentController> _logger;
		private readonly IClinicEnquiryRepository _enquiryRepo; // Add this


		public High5AppointmentController(
			IClinicHigh5AppointmentService appointmentService,
			ILogger<High5AppointmentController> logger,
			IClinicEnquiryRepository enquiryRepo
			)
		{
			_appointmentService = appointmentService;
			_logger = logger;
			_enquiryRepo = enquiryRepo;
		}

		// ================= Create =================
		[HttpPost]
		public async Task<IActionResult> Create([FromBody] High5AppointmentDto dto)
		{
			//if (!ModelState.IsValid)
			//	return BadRequest(ModelState);

			var appointment = new High5Appointment
			{
				ClinicId = dto.ClinicId,
				UserId = dto.UserId,
				PackageId = dto.PackageId,
				PackageName = dto.PackageName,
				PackageDate = dto.PackageDate,
				PackageTime = dto.PackageTime,
				CoachId = dto.CoachId,
				Status = dto.Status,
				EpochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()

			};

			var result = await _appointmentService.CreateAppointmentAsync(appointment);

			return Ok(ApiResponseFactory.Success(result));
		}

		// ================= Read =================
		[HttpGet("{id:int}")]
		public async Task<IActionResult> GetById(int id)
		{
			var result = await _appointmentService.GetAppointmentByIdAsync(id);

			if (result == null)
				return NotFound(ApiResponseFactory.Fail("High5 appointment not found."));

			return Ok(ApiResponseFactory.Success(result));
		}

		[HttpGet("clinic/{clinicId:int}")]
		public async Task<IActionResult> GetByClinic(int clinicId)
		{
			var result = await _appointmentService
				.GetAppointmentsByClinicIdAsync(clinicId);

			return Ok(ApiResponseFactory.Success(result));
		}

		[HttpGet("user/{userId:int}")]
		public async Task<IActionResult> GetByUser(int userId)
		{
			var result = await _appointmentService
				.GetAppointmentsByUserIdAsync(userId);

			return Ok(ApiResponseFactory.Success(result));
		}

		// ================= Update =================
		[HttpPut("{id:int}")]
		public async Task<IActionResult> Update(
	int id,
	[FromBody] High5AppointmentUpdateDto dto)	
		{
			var existing = await _appointmentService.GetAppointmentByIdAsync(id);

			if (existing == null)
				return NotFound(ApiResponseFactory.Fail("High5 appointment not found."));

			// Only update fields that are present in the payload
			if (dto.PackageDate.HasValue) existing.PackageDate = dto.PackageDate.Value;
			if (dto.PackageTime.HasValue) existing.PackageTime = dto.PackageTime.Value;
			if (!string.IsNullOrEmpty(dto.PackageName)) existing.PackageName = dto.PackageName;
			if (dto.PackageId.HasValue) existing.PackageId = dto.PackageId.Value;
			if (dto.CoachId.HasValue) existing.CoachId = dto.CoachId.Value;
			if (dto.Status.HasValue) existing.Status = dto.Status.Value;

			var updated = await _appointmentService.UpdateAppointmentAsync(existing);

			if (!updated)
				return StatusCode(500, ApiResponseFactory.Fail("Failed to update appointment."));

			return Ok(ApiResponseFactory.Success("High5 appointment updated successfully."));
		}


		public class AppointmentMergedDto
		{
			public int Id { get; set; }
			public int ClinicId { get; set; }
			public int? UserId { get; set; }
			public int? PackageId { get; set; }
			public string? PackageName { get; set; }
			public DateTime? Date { get; set; }
			public TimeSpan? Time { get; set; }

			public string? phone { get; set; }
			public int? CoachId { get; set; }
			public string CoachName { get; set; }  // ADD THIS LINE
			public string Status { get; set; }
			public long? EpochTime { get; set; }
			public string PatientName { get; set; }
			public string Source { get; set; }
			public string? HFID { get; set; }
			public PaymentStatus? PaymentStatus { get; set; }
		}

			[HttpGet("clinic/{clinicId}/all-appointments")]
			public async Task<IActionResult> GetAllAppointmentsByClinic(
			int clinicId,
			[FromQuery] int page = 1,	
			[FromQuery] int pageSize = 100,
			[FromQuery] EnquiryStatus? status = null,
			[FromQuery] PaymentStatus? paymentStatus = null,
			[FromQuery] DateTime? startDate = null,
			[FromQuery] DateTime? endDate = null)
			{
				try
				{
					// Use provided dates or default to today
					var filterStartDate = startDate?.Date ?? DateTime.Today;
					var filterEndDate = endDate?.Date ?? DateTime.Today;

					// Ensure startDate is not after endDate
					if (filterStartDate > filterEndDate)
					{
						return BadRequest(ApiResponseFactory.Fail("Start date cannot be after end date"));
					}

					// --- High5Appointments (Filtered by date range) --- 
					var high5Result = await _appointmentService.GetAppointmentsByClinicIdWithUserAsync(clinicId);
					var high5List = high5Result
						.Where(h => h.PackageDate.Date >= filterStartDate && h.PackageDate.Date <= filterEndDate)
						.Select(h => new AppointmentMergedDto
						{
							Id = h.Id,
							HFID = h.User.HfId,
							phone = h.User.PhoneNumber,
							ClinicId = h.ClinicId,
							UserId = h.UserId,
							PackageId = h.PackageId,
							PackageName = h.PackageName,
							Date = h.PackageDate,
							Time = h.PackageTime,
							CoachId = h.CoachId,
							CoachName = h.CoachMember?.User != null
								? $"{h.CoachMember.User.FirstName} {h.CoachMember.User.LastName}".Trim()
								: "N/A",
							Status = h.Status.ToString(),
							EpochTime = h.EpochTime,
							PatientName = h.User != null
								? $"{h.User.FirstName} {h.User.LastName}".Trim()
								: "N/A",
							Source = "High5Appointment",
							PaymentStatus = null
						}).ToList();

					// --- Enquiries (Filtered by date range) --- 
					var enquiriesResult = await _enquiryRepo.GetAllAsync(clinicId);
					var filteredEnquiries = enquiriesResult
						.Where(e => e.Status != EnquiryStatus.Member)
						.Where(e => e.AppointmentDate.HasValue &&
								   e.AppointmentDate.Value.Date >= filterStartDate &&
								   e.AppointmentDate.Value.Date <= filterEndDate);

					if (status != null)
						filteredEnquiries = filteredEnquiries.Where(e => e.Status == status.Value);

					if (paymentStatus != null)
						filteredEnquiries = filteredEnquiries.Where(e => e.Payment == paymentStatus.Value);

					var enquiryList = filteredEnquiries.Select(e => new AppointmentMergedDto
					{
						Id = e.Id,
						ClinicId = e.ClinicId,
						UserId = null,
						phone = e.Contact,
						PackageId = null,
						PackageName = null,
						Date = e.AppointmentDate,
						Time = e.AppointmentTime.Value,
						CoachId = null,
						Status = e.Status.ToString(),
						EpochTime = null,
						PatientName = e.Firstname + " " + e.Lastname,
						Source = "Enquiry",
						PaymentStatus = e.Payment
					}).ToList();

					// --- Merge --- 
					var mergedList = high5List
						.Concat(enquiryList)
						.OrderBy(x => x.Date)
						.ThenBy(x => x.Time)
						.ToList();

					// --- Pagination --- 
					int totalRecords = mergedList.Count;
					var pagedData = mergedList
						.Skip((page - 1) * pageSize)
						.Take(pageSize)
						.ToList();

					var result = new
					{
						Page = page,
						PageSize = pageSize,
						TotalRecords = totalRecords,
						TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize),
						StartDate = filterStartDate,
						EndDate = filterEndDate,
						Data = pagedData
					};

					return Ok(ApiResponseFactory.Success(result));
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error fetching appointments for ClinicId {ClinicId}", clinicId);
					return StatusCode(500, ApiResponseFactory.Fail(ex.Message));
				}
			}

		// Add these methods to your existing controller

		// ⭐ NEW: Get monthly statistics for calendar
		[HttpGet("clinic/{clinicId}/calendar-stats")]
		public async Task<IActionResult> GetCalendarStats(int clinicId)
		{
			try
			{
				// Get High5Appointments stats
				var high5Result = await _appointmentService.GetAppointmentsByClinicIdWithUserAsync(clinicId);
				var high5Stats = high5Result
					.GroupBy(h => new { h.PackageDate.Year, h.PackageDate.Month })
					.Select(g => new
					{
						Year = g.Key.Year,
						Month = g.Key.Month,
						MonthName = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMMM"),
						Count = g.Count()
					})
					.OrderByDescending(x => x.Year)
					.ThenByDescending(x => x.Month)
					.ToList();

				// Get Enquiries stats
				var enquiriesResult = await _enquiryRepo.GetAllAsync(clinicId);
				var enquiryStats = enquiriesResult
					.Where(e => e.Status != EnquiryStatus.Member && e.AppointmentDate.HasValue)
					.GroupBy(e => new { e.AppointmentDate.Value.Year, e.AppointmentDate.Value.Month })
					.Select(g => new
					{
						Year = g.Key.Year,
						Month = g.Key.Month,
						MonthName = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMMM"),
						Count = g.Count()
					})
					.OrderByDescending(x => x.Year)
					.ThenByDescending(x => x.Month)
					.ToList();

				// Merge monthly counts
				var mergedStats = high5Stats
					.Concat(enquiryStats)
					.GroupBy(s => new { s.Year, s.Month, s.MonthName })
					.Select(g => new
					{
						Year = g.Key.Year,
						Month = g.Key.Month,
						MonthName = g.Key.MonthName,
						Count = g.Sum(x => x.Count)
					})
					.OrderByDescending(x => x.Year)
					.ThenByDescending(x => x.Month)
					.ToList();

				var result = new
				{
					TotalCount = high5Result.Count() + enquiriesResult.Count(e => e.Status != EnquiryStatus.Member && e.AppointmentDate.HasValue),
					MonthlyBreakdown = mergedStats
				};

				return Ok(ApiResponseFactory.Success(result));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error fetching calendar stats for ClinicId {ClinicId}", clinicId);
				return StatusCode(500, ApiResponseFactory.Fail(ex.Message));
			}
		}

		// ⭐ NEW: Get daily appointment counts for a specific month
		[HttpGet("clinic/{clinicId}/calendar-daily/{year}/{month}")]
		public async Task<IActionResult> GetDailyAppointments(int clinicId, int year, int month)
		{
			try
			{
				var startDate = new DateTime(year, month, 1);
				var endDate = startDate.AddMonths(1).AddDays(-1);

				// Get High5Appointments
				var high5Result = await _appointmentService.GetAppointmentsByClinicIdWithUserAsync(clinicId);
				var high5Daily = high5Result
					.Where(h => h.PackageDate.Date >= startDate && h.PackageDate.Date <= endDate)
					.GroupBy(h => h.PackageDate.Date)
					.Select(g => new
					{
						Date = g.Key,
						Count = g.Count(),
					});

				// Get Enquiries
				var enquiriesResult = await _enquiryRepo.GetAllAsync(clinicId);
				var enquiryDaily = enquiriesResult
					.Where(e => e.Status != EnquiryStatus.Member)
					.Where(e => e.AppointmentDate.HasValue &&
							   e.AppointmentDate.Value.Date >= startDate &&
							   e.AppointmentDate.Value.Date <= endDate)
					.GroupBy(e => e.AppointmentDate.Value.Date)
					.Select(g => new
					{
						Date = g.Key,
						Count = g.Count(),
						
					});

				// Merge daily data
				var mergedDaily = high5Daily
					.Concat(enquiryDaily)
					.GroupBy(d => d.Date)
					.Select(g => new
					{
						Date = g.Key.ToString("yyyy-MM-dd"),
						Count = g.Sum(x => x.Count),
						
					})
					.OrderBy(d => d.Date)
					.ToList();

				return Ok(ApiResponseFactory.Success(mergedDaily));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error fetching daily appointments for ClinicId {ClinicId}", clinicId);
				return StatusCode(500, ApiResponseFactory.Fail(ex.Message));
			}
		}

		// ⭐ NEW: Get appointments for a specific date (reuse existing with single date)
		[HttpGet("clinic/{clinicId}/appointments-by-date")]
		public async Task<IActionResult> GetAppointmentsByDate(
			int clinicId,
			[FromQuery] DateTime date)
		{
			try
			{
				// Reuse your existing method with same start and end date
				return await GetAllAppointmentsByClinic(
					clinicId,
					page: 1,
					pageSize: 1000000000, // Get all appointments for the day
					status: null,
					paymentStatus: null,
					startDate: date.Date,
					endDate: date.Date
				);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error fetching appointments by date for ClinicId {ClinicId}", clinicId);
				return StatusCode(500, ApiResponseFactory.Fail(ex.Message));
			}
		}




	}
}
