
using HFiles_Backend.Application.Common;
using HFiles_Backend.Domain.DTOs.Clinics;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Enums;
using HFiles_Backend.Domain.Interfaces.Clinics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using HFiles_Backend.API.Controllers.Clinics;

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
			if (!ModelState.IsValid)
				return BadRequest(ModelState);

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
			[FromBody] High5AppointmentDto dto)
		{
			if (!ModelState.IsValid)
				return BadRequest(ModelState);

			var existing = await _appointmentService.GetAppointmentByIdAsync(id);

			if (existing == null)
				return NotFound(ApiResponseFactory.Fail("High5 appointment not found."));

			existing.PackageDate = dto.PackageDate;
			existing.PackageTime = dto.PackageTime;
			existing.PackageName = dto.PackageName;
			existing.PackageId = dto.PackageId;
			existing.CoachId = dto.CoachId;
			existing.Status = dto.Status;

			var updated = await _appointmentService.UpdateAppointmentAsync(existing);

			if (!updated)
				return StatusCode(
					500,
					ApiResponseFactory.Fail("Failed to update appointment.")
				);

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
			public string Time { get; set; }
			public int? CoachId { get; set; }
			public string CoachName { get; set; }  // ADD THIS LINE
			public string Status { get; set; }
			public long? EpochTime { get; set; }
			public string PatientName { get; set; }
			public string Source { get; set; }
			public PaymentStatus? PaymentStatus { get; set; }
		}

		[HttpGet("clinic/{clinicId}/all-appointments")]
		public async Task<IActionResult> GetAllAppointmentsByClinic(
	int clinicId,
	[FromQuery] int page = 1,
	[FromQuery] int pageSize = 10,
	[FromQuery] EnquiryStatus? status = null,
	[FromQuery] PaymentStatus? paymentStatus = null)
		{
			try
			{
				var today = DateTime.Today;

				// --- High5Appointments (Today only) --- 
				var high5Result = await _appointmentService.GetAppointmentsByClinicIdWithUserAsync(clinicId);

				var high5List = high5Result
					.Where(h => h.PackageDate.Date == today)
					.Select(h => new AppointmentMergedDto
					{
						Id = h.Id,
						ClinicId = h.ClinicId,
						UserId = h.UserId,
						PackageId = h.PackageId,
						PackageName = h.PackageName,
						Date = h.PackageDate,
						Time = h.PackageTime.ToString(@"hh\:mm"),
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

				// --- Enquiries (Today only) --- 
				var enquiriesResult = await _enquiryRepo.GetAllAsync(clinicId);

				var filteredEnquiries = enquiriesResult
					.Where(e => e.Status != EnquiryStatus.Member)
					.Where(e => e.AppointmentDate.HasValue && e.AppointmentDate.Value.Date == today);

				if (status != null)
					filteredEnquiries = filteredEnquiries.Where(e => e.Status == status.Value);

				if (paymentStatus != null)
					filteredEnquiries = filteredEnquiries.Where(e => e.Payment == paymentStatus.Value);

				var enquiryList = filteredEnquiries.Select(e => new AppointmentMergedDto
				{
					Id = e.Id,
					ClinicId = e.ClinicId,
					UserId = null,
					PackageId = null,
					PackageName = null,
					Date = e.AppointmentDate,
					Time = e.AppointmentDate.HasValue ? e.AppointmentDate.Value.ToString("HH:mm") : "",
					CoachId = null,
					Status = e.Status.ToString(),
					EpochTime = null,
					PatientName = e.Firstname+" "+e.Lastname,

					Source = "Enquiry",
					PaymentStatus = e.Payment
				}).ToList();

				// --- Merge --- 
				var mergedList = high5List
					.Concat(enquiryList)
					.OrderBy(x => x.Time)
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
					Data = pagedData
				};

				return Ok(ApiResponseFactory.Success(result));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error fetching today's appointments for ClinicId {ClinicId}", clinicId);
				return StatusCode(500, ApiResponseFactory.Fail(ex.Message));
			}
		}



	}
}
