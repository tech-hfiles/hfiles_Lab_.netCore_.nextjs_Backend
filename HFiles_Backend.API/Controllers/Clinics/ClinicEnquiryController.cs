using HFiles_Backend.API.Interfaces;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

[ApiController]
[Route("api/clinics/enquiries")]
[Authorize]
public class ClinicEnquiryController : ControllerBase
{
    private readonly IClinicEnquiryRepository _repo;
    private readonly IClinicAuthorizationService _clinicAuthorizationService;
    private readonly ILogger<ClinicEnquiryController> _logger;

    public ClinicEnquiryController(
        IClinicEnquiryRepository repo,
        IClinicAuthorizationService clinicAuthorizationService,
        ILogger<ClinicEnquiryController> logger
    )
    {
        _repo = repo;
        _clinicAuthorizationService = clinicAuthorizationService;
        _logger = logger;
    }

	// =====================================================
	// GET : All Enquiries (Filter + Pagination)
	// =====================================================
	[HttpGet("{clinicId}")]
	public async Task<IActionResult> GetAll(
	 int clinicId,
	 [FromQuery] int page = 1,
	 [FromQuery] int pageSize = 10,
	 [FromQuery] EnquiryStatus? status = null,
	 [FromQuery] PaymentStatus? paymentStatus = null
 )
	{
		HttpContext.Items["Log-Category"] = "Clinic Enquiry";

		try
		{
			bool isAuthorized = await _clinicAuthorizationService
				.IsClinicAuthorized(clinicId, User);

			if (!isAuthorized)
			{
				_logger.LogWarning("Unauthorized enquiry list access for ClinicId {ClinicId}", clinicId);
				return Unauthorized(ApiResponseFactory.Fail("You are not authorized to view enquiries."));
			}

			if (page < 1) page = 1;
			if (pageSize < 1) pageSize = 10;

			// -----------------
			// Fetch
			// -----------------
			var enquiries = await _repo.GetAllAsync(clinicId);

			// -----------------
			// FILTERING
			// -----------------
			var filteredEnquiries = enquiries
				.Where(e => e.Status != EnquiryStatus.Member);

            if (status!=null)
            {
                filteredEnquiries = filteredEnquiries
                    .Where(e => e.Status == status.Value);
            }

            if (paymentStatus != null)
            {
                filteredEnquiries = filteredEnquiries
                    .Where(e => e.Payment == paymentStatus.Value);
            }
			// -----------------
			// COUNT AFTER FILTER
			// -----------------
			int totalRecords = filteredEnquiries.Count();
			filteredEnquiries = filteredEnquiries.OrderBy(e => e.FollowUpDate).ToList();
			// -----------------
			// PAGINATION (FILTERED!)
			// -----------------
			var today = DateTime.Today;

			var pagedData = filteredEnquiries
				.OrderByDescending(e => e.FollowUpDate.HasValue && e.FollowUpDate.Value.Date == today)
				.ThenByDescending(e => e.FollowUpDate)
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
			_logger.LogError(ex, "Error fetching enquiries for ClinicId {ClinicId}", clinicId);
			return StatusCode(500, ApiResponseFactory.Fail(ex.Message));
		}
	}


	// =====================================================
	// GET : Enquiry Detail
	// =====================================================
	[HttpGet("detail/{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        HttpContext.Items["Log-Category"] = "Clinic Enquiry";

        var enquiry = await _repo.GetByIdAsync(id);
        if (enquiry == null)
            return NotFound(ApiResponseFactory.Fail("Enquiry not found."));

        bool isAuthorized = await _clinicAuthorizationService
            .IsClinicAuthorized(enquiry.ClinicId, User);

        if (!isAuthorized)
        {
            _logger.LogWarning("Unauthorized enquiry detail access. EnquiryId {Id}", id);
            return Unauthorized(ApiResponseFactory.Fail("You are not authorized to view this enquiry."));
        }

        return Ok(ApiResponseFactory.Success(enquiry));
    }

    // =====================================================
    // POST : Create Enquiry
    // =====================================================
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClinicEnquiryDto dto)
    {
        HttpContext.Items["Log-Category"] = "Clinic Enquiry";

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(ApiResponseFactory.Fail(errors));
        }

        bool isAuthorized = await _clinicAuthorizationService
            .IsClinicAuthorized(dto.ClinicId, User);

        if (!isAuthorized)
            return Unauthorized(ApiResponseFactory.Fail("Only authorized clinic users can create enquiries."));

        var enquiry = new ClinicEnquiry
        {
            ClinicId = dto.ClinicId,
            UserId = dto.UserId,
            Firstname = dto.Firstname,
            Lastname = dto.Lastname,
            Email = dto.Email,
            Contact = dto.Contact,
            DateOfBirth = dto.DateOfBirth,
            Source = dto.Source,
            FollowUpDate = dto.FollowUpDate,
            FitnessGoal = dto.FitnessGoal,
            Status = dto.Status ?? EnquiryStatus.Inquiry,
            Payment = dto.Payment ?? PaymentStatus.NA,
            AppointmentDate = dto.AppointmentDate,
            AppointmentTime = dto.AppointmentTime,
            Remark = dto.Remark,
            FirstCall = false,
            SecondCall = false,
            EpochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        await _repo.AddAsync(enquiry);

        return Ok(ApiResponseFactory.Success(enquiry, "Enquiry created successfully."));
    }

	// =====================================================
	// POST : Update Enquiry
	// =====================================================

	[HttpPut("{id}")]
	public async Task<IActionResult> Update(
		int id,
		[FromBody] CreateClinicEnquiryDto dto)
	{
		HttpContext.Items["Log-Category"] = "Clinic Enquiry";

		var enquiry = await _repo.GetByIdAsync(id);
		if (enquiry == null)
			return NotFound(ApiResponseFactory.Fail("Enquiry not found."));

		bool isAuthorized = await _clinicAuthorizationService
			.IsClinicAuthorized(enquiry.ClinicId, User);

		if (!isAuthorized)
			return Unauthorized(ApiResponseFactory.Fail("Not authorized."));

		// ================================
		// 🔹 UPDATE ONLY WHAT IS SENT
		// ================================

		if (dto.Status.HasValue)
			enquiry.Status = dto.Status.Value;

		if (dto.Payment.HasValue)
			enquiry.Payment = dto.Payment.Value;

		if (dto.FirstCall.HasValue)
			enquiry.FirstCall = dto.FirstCall.Value;

		if (dto.SecondCall.HasValue)
			enquiry.SecondCall = dto.SecondCall.Value;

		if (dto.Remark != null)
			enquiry.Remark = dto.Remark;

		// ❌ Ignore ClinicId & UserId in UPDATE
		// ❌ Ignore other fields intentionally

		await _repo.UpdateAsync(enquiry);

		return Ok(ApiResponseFactory.Success(
			enquiry,
			"Enquiry updated successfully."
		));
	}
	// =====================================================
	// GET : TODAY APPOINTMENTS (NO DATE FROM FRONTEND)
	// =====================================================
	[HttpGet("{clinicId}/appointments/today")]
	public async Task<IActionResult> GetTodayAppointments(int clinicId)
	{
		HttpContext.Items["Log-Category"] = "Clinic Enquiry";

		bool isAuthorized = await _clinicAuthorizationService
			.IsClinicAuthorized(clinicId, User);

		if (!isAuthorized)
		{
			_logger.LogWarning("Unauthorized today appointment access for ClinicId {ClinicId}", clinicId);
			return Unauthorized(ApiResponseFactory.Fail("You are not authorized."));
		}

		var today = DateTime.Today; // local date (recommended)

		var enquiries = await _repo.GetTodayAppointmentsAsync(clinicId, today);

		return Ok(ApiResponseFactory.Success(enquiries));
	}




}
