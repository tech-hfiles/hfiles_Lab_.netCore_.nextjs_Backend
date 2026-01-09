using HFiles_Backend.API.DTOs.Clinics;
using HFiles_Backend.API.Interfaces;
using HFiles_Backend.API.Services;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Enums;
using HFiles_Backend.Domain.Interfaces;
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
	private readonly IEmailTemplateService _emailTemplateService;
	private readonly EmailService _emailService;
	private readonly IClinicRepository _clinicRepository;  // ADD THIS LINE

	public ClinicEnquiryController(
		IEmailTemplateService emailTemplateService,
			IClinicRepository clinicRepository,  // ADD THIS PARAMETER

	 EmailService emailService,
		IClinicEnquiryRepository repo,
        IClinicAuthorizationService clinicAuthorizationService,
        ILogger<ClinicEnquiryController> logger
    )
    {
		_emailTemplateService = emailTemplateService;
		_emailService = emailService;
		_repo = repo;
        _clinicAuthorizationService = clinicAuthorizationService;
        _logger = logger;
		_clinicRepository = clinicRepository;  // ADD THIS ASSIGNMENT

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
		[FromQuery] PaymentStatus? paymentStatus = null,
		[FromQuery] int? coachId = null
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

			// Fetch with coach data
			var enquiries = await _repo.GetAllAsync(clinicId);

			// Filtering
			var filteredEnquiries = enquiries
				.Where(e => e.Status != EnquiryStatus.Member);

			if (status != null)
			{
				filteredEnquiries = filteredEnquiries
					.Where(e => e.Status == status.Value);
			}

			if (paymentStatus != null)
			{
				filteredEnquiries = filteredEnquiries
					.Where(e => e.Payment == paymentStatus.Value);
			}

			// Filter by coach if coachId is provided
			if (coachId != null)
			{
				filteredEnquiries = filteredEnquiries
					.Where(e => e.AssignedCoaches.Any(ac => ac.CoachId == coachId.Value));
			}

			// Count after filter
			int totalRecords = filteredEnquiries.Count();

			// Pagination
			var today = DateTime.Today;
			var pagedData = filteredEnquiries
				.OrderByDescending(e => e.FollowUpDate.HasValue && e.FollowUpDate.Value.Date == today)
				.ThenByDescending(e => e.FollowUpDate)
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.Select(e => new
				{
					e.Id,
					e.Firstname,
					e.Lastname,
					e.Email,
					e.Contact,
					e.DateOfBirth,
					e.Source,
					e.FollowUpDate,
					e.FitnessGoal,
					e.Status,
					e.Payment,
					e.AppointmentDate,
					e.AppointmentTime,
					e.FirstCall,
					e.SecondCall,
					e.Remark,
					e.EpochTime,
					PricingPackage = e.PricingPackageId == null
		? null
		: new
		{
			e.PricingPackage.Id,
			e.PricingPackage.ProgramCategory,
			e.PricingPackage.ProgramName,
			e.PricingPackage.DurationMonths,
			e.PricingPackage.PriceInr
		},
					// Map assigned coaches - following the correct navigation path
					AssignedCoaches = e.AssignedCoaches.Select(ac => new
					{
						MappingId = ac.Id,
						ClinicMemberId = ac.CoachId,
						CoachType = ac.ClinicMember?.Coach,
						CoachName = $"{ac.ClinicMember?.User?.FirstName} {ac.ClinicMember?.User?.LastName}".Trim(),
						Email = ac.ClinicMember?.User?.Email,
						Contact = ac.ClinicMember?.User?.PhoneNumber
					}).ToList()
				})
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
			PricingPackageId = dto.PricingPackageId,

			FirstCall = false,
            SecondCall = false,
            EpochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
		// trial done 
		if (false)
		{
			try
			{
				// Get clinic details
				var clinic = await _clinicRepository.GetClinicByIdAsync(dto.ClinicId);

				// Format date and time
				var appointmentDateFormatted = dto.AppointmentDate.Value.ToString("dd-MM-yyyy");
				var appointmentTimeFormatted = dto.AppointmentTime.Value.ToString(@"hh\:mm");

				// Generate email template
				var emailTemplate = _emailTemplateService.GenerateTrialAppointmentConfirmationEmailTemplate(
					dto.Firstname,
					clinic?.ClinicName ?? "Clinic",
					appointmentDateFormatted,
					appointmentTimeFormatted,
					dto.FitnessGoal
				);

				// Send email
				await _emailService.SendEmailAsync(
					dto.Email,
					$"Trial Appointment Confirmation - {clinic?.ClinicName}",
					emailTemplate
				);

				_logger.LogInformation(
					"Trial appointment confirmation email sent to {Email} for {Date} at {Time}",
					dto.Email,
					appointmentDateFormatted,
					appointmentTimeFormatted
				);
			}
			catch (Exception emailEx)
			{
				_logger.LogError(
					emailEx,
					"Failed to send trial appointment email to {Email}",
					dto.Email
				);
				// Don't fail the entire operation if email fails
			}
		}


		await _repo.AddAsync(enquiry);

        return Ok(ApiResponseFactory.Success(enquiry, "Enquiry created successfully."));
    }

	// =====================================================
	// POST : Update Enquiry
	// =====================================================

	[HttpPut("{id}")]
	public async Task<IActionResult> Update(
	int id,
	[FromBody] UpdateClinicEnquiryDto dto)
	{
		if (dto == null)
			return BadRequest("Request body is required.");

		var enquiry = await _repo.GetByIdAsync(id);
		if (enquiry == null)
			return NotFound();

		if (dto.Status.HasValue)
			enquiry.Status = dto.Status.Value;

		if (dto.Payment.HasValue)
			enquiry.Payment = dto.Payment.Value;

		if (dto.FirstCall.HasValue)
			enquiry.FirstCall = dto.FirstCall.Value;

		if (dto.SecondCall.HasValue)
			enquiry.SecondCall = dto.SecondCall.Value;

		if (!string.IsNullOrWhiteSpace(dto.Remark))
			enquiry.Remark = dto.Remark;

		if (dto.AppointmentDate.HasValue)
			enquiry.AppointmentDate = dto.AppointmentDate.Value;

		if (dto.AppointmentTime.HasValue)
			enquiry.AppointmentTime = dto.AppointmentTime.Value;

		if (dto.FollowUpDate.HasValue)
			enquiry.FollowUpDate = dto.FollowUpDate.Value;

		await _repo.UpdateAsync(enquiry);

		return Ok(ApiResponseFactory.Success(enquiry, "Updated"));
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

	// =====================================================
	// GET : Get Assigned Coaches for an Enquiry
	// =====================================================
	[HttpGet("{id}/coaches")]
	public async Task<IActionResult> GetEnquiryCoaches(int id)
	{
		HttpContext.Items["Log-Category"] = "Clinic Enquiry Coach";

		// Check if the enquiry exists
		var enquiry = await _repo.GetByIdAsync(id);
		if (enquiry == null)
			return NotFound(ApiResponseFactory.Fail("Enquiry not found."));

		// Authorization check
		bool isAuthorized = await _clinicAuthorizationService
			.IsClinicAuthorized(enquiry.ClinicId, User);

		if (!isAuthorized)
		{
			_logger.LogWarning("Unauthorized coach list access. EnquiryId {Id}", id);
			return Unauthorized(ApiResponseFactory.Fail("Not authorized."));
		}

		// Get all assigned coaches for this enquiry
		var coaches = await _repo.GetEnquiryCoachesAsync(id);

		// Map to API response
		var result = coaches.Select(ec => new
		{
			MappingId = ec.Id,
			ClinicMemberId = ec.CoachId,
			UserId = ec.ClinicMember?.UserId,
			CoachName = $"{ec.ClinicMember?.User?.FirstName} {ec.ClinicMember?.User?.LastName}".Trim(),
			Email = ec.ClinicMember?.User?.Email,
			Contact = ec.ClinicMember?.User?.PhoneNumber,
			CoachType = ec.ClinicMember?.Coach,   // e.g., "Personal Trainer"
			Role = ec.ClinicMember?.Role,
			AssignedAt = DateTimeOffset.FromUnixTimeSeconds(ec.EpochTime).UtcDateTime
		});

		return Ok(ApiResponseFactory.Success(result));
	}








	[HttpPost("assign-coach")]
	public async Task<IActionResult> AssignCoach([FromBody] AddEnquiryCoachRequest request)
	{
		HttpContext.Items["Log-Category"] = "Clinic Enquiry Coach";

		if (request == null)
			return BadRequest(ApiResponseFactory.Fail("Request body is null or invalid."));

		if (!ModelState.IsValid)
			return BadRequest(ApiResponseFactory.Fail(ModelState.Values
				.SelectMany(v => v.Errors)
				.Select(e => e.ErrorMessage)
				.ToList()));

		var enquiry = await _repo.GetByIdAsync(request.ClinicEnquiryId);
		if (enquiry == null)
			return NotFound(ApiResponseFactory.Fail("Enquiry not found."));

		bool isAuthorized = await _clinicAuthorizationService
			.IsClinicAuthorized(enquiry.ClinicId, User);

		if (!isAuthorized)
			return Unauthorized(ApiResponseFactory.Fail("Not authorized to assign coaches."));

		try
		{
			await _repo.AssignCoachAsync(request.ClinicEnquiryId, request.CoachId);
		}
		catch (Exception ex)
		{
			return BadRequest(ApiResponseFactory.Fail(ex.Message));
		}

		return Ok(ApiResponseFactory.Success(new
		{
			EnquiryId = request.ClinicEnquiryId,
			AssignedCoachId = request.CoachId
		}, "Coach assigned successfully."));
	}



	[HttpPut("{enquiryId}/coaches")]
	public async Task<IActionResult> SyncCoaches(
	int enquiryId,
	[FromBody] SyncEnquiryCoachesRequest request)
	{
		if (request == null || request.CoachIds == null)
			return BadRequest(ApiResponseFactory.Fail("CoachIds required"));

		var enquiry = await _repo.GetByIdAsync(enquiryId);
		if (enquiry == null)
			return NotFound(ApiResponseFactory.Fail("Enquiry not found"));

		bool isAuthorized = await _clinicAuthorizationService
			.IsClinicAuthorized(enquiry.ClinicId, User);

		if (!isAuthorized)
			return Unauthorized(ApiResponseFactory.Fail("Not authorized"));

		await _repo.SyncCoachesAsync(enquiryId, request.CoachIds);

		return Ok(ApiResponseFactory.Success("Coaches synced successfully"));
	}


}
