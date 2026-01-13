
using HFiles_Backend.API.Controllers.Clinics;
using HFiles_Backend.API.Interfaces;
using HFiles_Backend.API.Services;
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
        private readonly IEmailTemplateService _emailTemplateService;
        private readonly EmailService _emailService;
        private readonly ISessionReminderLogRepository _reminderLogRepo; // ADD THIS


        public High5AppointmentController(
			IClinicHigh5AppointmentService appointmentService,
			ILogger<High5AppointmentController> logger,
			IClinicEnquiryRepository enquiryRepo,
            IEmailTemplateService emailTemplateService,
        EmailService emailService,
            ISessionReminderLogRepository reminderLogRepo // ADD THIS


            )
		{
			_emailTemplateService = emailTemplateService;

			_emailService = emailService;
            _appointmentService = appointmentService;
			_logger = logger;
			_enquiryRepo = enquiryRepo;
            _reminderLogRepo = reminderLogRepo; // ADD THIS

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
				PatientId = dto.PatientId,
				ClinicVisitId = dto.ClinicVisitId,
				UniqueRecordId = dto.UniqueRecordId,
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






		public class PackageAppointmentDetailDto
		{
			public int AppointmentId { get; set; }
			public string PackageName { get; set; }
			public DateTime PackageDate { get; set; }
			public TimeSpan PackageTime { get; set; }
			public int Status { get; set; }
			public string UniqueRecordId { get; set; }
			public string PatientName { get; set; }
			// Add any other fields from the JSON if needed
		}

		[HttpGet("patient-mep-history/{patientId}")]
		public async Task<IActionResult> GetPatientMEPHistory(int patientId)
		{
			try
			{
				var data = await _appointmentService.GetMEPPackagesByPatientAsync(patientId);

				if (data == null || !data.Any())
					return NotFound(ApiResponseFactory.Fail("No MEP packages found for this patient."));

				return Ok(ApiResponseFactory.Success(data, "Patient MEP history retrieved successfully."));
			}
			catch (Exception ex)
			{
				return StatusCode(500,
					ApiResponseFactory.Fail("Error parsing package data: " + ex.Message));
			}
		}


		[HttpGet("package-details/{uniqueRecordId}")]
		public async Task<IActionResult> GetPackageAppointments(string uniqueRecordId)
		{
			try
			{
				// Simply await the service call
				var data = await _appointmentService.GetAppointmentsByRecordIdAsync(uniqueRecordId);

				if (data == null || !data.Any())
					return NotFound(ApiResponseFactory.Fail("No appointments found for this package record."));

				return Ok(ApiResponseFactory.Success(data, "Package details retrieved successfully."));
			}
			catch (Exception ex)
			{
				return StatusCode(500, ApiResponseFactory.Fail(ex.Message));
			}
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


		//update
		[HttpPut("{id:int}")]
		public async Task<IActionResult> Update(int id, [FromBody] High5AppointmentUpdateDto dto)
		{
			// 1. Fetch the existing record
			var existing = await _appointmentService.GetAppointmentByIdAsync(id);
			if (existing == null)
				return NotFound(ApiResponseFactory.Fail("High5 appointment not found."));

			// 2. Determine the values to check
			int checkPackageId = dto.PackageId ?? existing.PackageId;
			DateTime checkDate = dto.PackageDate ?? existing.PackageDate;
			int checkUserId = existing.UserId;

			// 3. FIXED VALIDATION: Call the service method directly
			var isDuplicate = await _appointmentService.IsDuplicateAppointmentAsync(
				id,
				checkUserId,
				checkPackageId,
				checkDate
			);

			if (isDuplicate)
			{
				return BadRequest(ApiResponseFactory.Fail(
					$"Appointment already exists for this package on {checkDate:yyyy-MM-dd}."
				));
			}

			// 4. Update the fields
			if (dto.PackageId.HasValue) existing.PackageId = dto.PackageId.Value;
			if (!string.IsNullOrWhiteSpace(dto.PackageName)) existing.PackageName = dto.PackageName;
			if (dto.PackageDate.HasValue && dto.PackageDate.Value != DateTime.MinValue) existing.PackageDate = dto.PackageDate.Value;
			if (dto.PackageTime.HasValue) existing.PackageTime = dto.PackageTime.Value;
			if (dto.CoachId.HasValue) existing.CoachId = dto.CoachId.Value;
			if (dto.Status.HasValue) existing.Status = dto.Status.Value;

			// 5. Save changes
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

			public DateTime? followup { get; set; }
			public TimeSpan? Time { get; set; }

			public string? phone { get; set; }
			public string? CoachId { get; set; }
			public string CoachName { get; set; }  // ADD THIS LINE
            public string? CoachColor { get; set; }
            public string Status { get; set; }
			public long? EpochTime { get; set; }
			public string PatientName { get; set; }
			public string Source { get; set; }
			public string? EventType { get; set; } // "Trial" | "FollowUp"

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
[FromQuery] DateTime? endDate = null,
[FromQuery] string? type = null,
[FromQuery] int? coachId = null
)
		{
			try
			{
				// Use provided dates or default to today
				var filterStartDate = startDate?.Date ?? DateTime.Today;
				var filterEndDate = endDate?.Date ?? DateTime.Today;
				var normalizedType = type?.ToLower();

				if (filterStartDate > filterEndDate)
				{
					return BadRequest(ApiResponseFactory.Fail("Start date cannot be after end date"));
				}

				// --- High5Appointments ---
				List<AppointmentMergedDto> high5List = new();
				if (normalizedType == null || normalizedType == "appointment")
				{
					var high5Result = await _appointmentService.GetAppointmentsByClinicIdWithUserAsync(clinicId);

					high5List = high5Result
						.Where(h => h.PackageDate.Date >= filterStartDate &&
									h.PackageDate.Date <= filterEndDate)
						.Where(h => coachId == null || h.CoachId == coachId)
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
							followup = null,
							Time = h.PackageTime,
							CoachId = h.CoachId.ToString(),
							CoachName = h.CoachMember?.User != null
								? $"{h.CoachMember.User.FirstName} {h.CoachMember.User.LastName}".Trim()
								: "N/A",
							CoachColor = h.CoachMember?.Color ?? "rgba(0, 0, 0, 1)",
							Status = h.Status.ToString(),
							EpochTime = h.EpochTime,
							PatientName = h.User != null
								? $"{h.User.FirstName} {h.User.LastName}".Trim()
								: "N/A",
							Source = "High5Appointment",
							PaymentStatus = null
						})
						.ToList();
				}

				// --- Enquiries ---
				List<AppointmentMergedDto> enquiryList = new();
				if (normalizedType == null || normalizedType == "enquiry")
				{
					var enquiriesResult = await _enquiryRepo.GetAllAsync(clinicId);

					var filteredEnquiries = enquiriesResult
						.Where(e => e.Status != EnquiryStatus.Member);

					if (status != null)
						filteredEnquiries = filteredEnquiries.Where(e => e.Status == status.Value);

					if (paymentStatus != null)
						filteredEnquiries = filteredEnquiries.Where(e => e.Payment == paymentStatus.Value);

					if (coachId != null)
						filteredEnquiries = filteredEnquiries
							.Where(e => e.AssignedCoaches != null && e.AssignedCoaches.Any(ac => ac.CoachId == coachId.Value));

					foreach (var e in filteredEnquiries)
					{
						// AppointmentDate entry
						if (e.AppointmentDate.HasValue &&
							e.AppointmentDate.Value.Date >= filterStartDate &&
							e.AppointmentDate.Value.Date <= filterEndDate)
						{
							enquiryList.Add(new AppointmentMergedDto
							{
								Id = e.Id,
								ClinicId = e.ClinicId,
								UserId = null,
								phone = e.Contact,
								PackageId = e.PricingPackageId,
								PackageName = e.PricingPackage?.ProgramName,
								Date = e.AppointmentDate,
								followup = e.FollowUpDate,
								Time = e.AppointmentTime ?? TimeSpan.Zero,
								CoachId = e.AssignedCoaches != null && e.AssignedCoaches.Any()
									? string.Join(",", e.AssignedCoaches.Select(ac => ac.CoachId))
									: null,
								CoachName = e.AssignedCoaches != null && e.AssignedCoaches.Any()
									? string.Join(",", e.AssignedCoaches
										.Where(ac => ac.ClinicMember != null && ac.ClinicMember.User != null)
										.Select(ac => $"{ac.ClinicMember.User.FirstName} {ac.ClinicMember.User.LastName}"))
									: "N/A",
								CoachColor = "rgba(0, 0, 0, 1)",
								Status = e.Status.ToString(),
								EpochTime = null,
								PatientName = $"{e.Firstname} {e.Lastname}",
								Source = "Enquiry",
								EventType = "Trial",

								PaymentStatus = e.Payment
							});
						}

						// FollowUpDate entry
						if (e.FollowUpDate.HasValue &&
							e.FollowUpDate.Value.Date >= filterStartDate &&
							e.FollowUpDate.Value.Date <= filterEndDate)
						{
							enquiryList.Add(new AppointmentMergedDto
							{
								Id = e.Id,
								ClinicId = e.ClinicId,
								UserId = null,
								phone = e.Contact,
								PackageId = e.PricingPackageId,
								PackageName = e.PricingPackage?.ProgramName,
								Date = e.FollowUpDate,
								followup = e.FollowUpDate,
								Time = e.AppointmentTime ?? TimeSpan.Zero,
								CoachId = e.AssignedCoaches != null && e.AssignedCoaches.Any()
									? string.Join(",", e.AssignedCoaches.Select(ac => ac.CoachId))
									: null,
								CoachName = e.AssignedCoaches != null && e.AssignedCoaches.Any()
									? string.Join(",", e.AssignedCoaches
										.Where(ac => ac.ClinicMember != null && ac.ClinicMember.User != null)
										.Select(ac => $"{ac.ClinicMember.User.FirstName} {ac.ClinicMember.User.LastName}"))
									: "N/A",
								CoachColor = "rgba(0, 0, 0, 1)",
								Status = e.Status.ToString(),
								EpochTime = null,
								PatientName = $"{e.Firstname} {e.Lastname}",
								Source = "Enquiry",
								EventType = "FollowUp",

								PaymentStatus = e.Payment
							});
						}

						// Epoch fallback
						if ((!e.AppointmentDate.HasValue || e.AppointmentDate.Value.Date < filterStartDate || e.AppointmentDate.Value.Date > filterEndDate) &&
							(!e.FollowUpDate.HasValue || e.FollowUpDate.Value.Date < filterStartDate || e.FollowUpDate.Value.Date > filterEndDate))
						{
							var epochDate = DateTimeOffset.FromUnixTimeSeconds(e.EpochTime).Date;
							if (epochDate >= filterStartDate && epochDate <= filterEndDate)
							{
								enquiryList.Add(new AppointmentMergedDto
								{
									Id = e.Id,
									ClinicId = e.ClinicId,
									UserId = null,
									phone = e.Contact,
									PackageId = e.PricingPackageId,
									PackageName = e.PricingPackage?.ProgramName,
									Date = epochDate,
									followup = null,
									Time = e.AppointmentTime ?? TimeSpan.Zero,
									CoachId = e.AssignedCoaches != null && e.AssignedCoaches.Any()
										? string.Join(",", e.AssignedCoaches.Select(ac => ac.CoachId))
										: null,
									CoachName = e.AssignedCoaches != null && e.AssignedCoaches.Any()
										? string.Join(",", e.AssignedCoaches
											.Where(ac => ac.ClinicMember != null && ac.ClinicMember.User != null)
											.Select(ac => $"{ac.ClinicMember.User.FirstName} {ac.ClinicMember.User.LastName}"))
										: "N/A",
									CoachColor = "rgba(0, 0, 0, 1)",
									Status = e.Status.ToString(),
									EpochTime = e.EpochTime,
									PatientName = $"{e.Firstname} {e.Lastname}",
									Source = "Enquiry",
									PaymentStatus = e.Payment
								});
							}
						}
					}
				}

				// --- Merge High5 + Enquiries ---
				var mergedList = high5List
					.Concat(enquiryList)
					.OrderBy(x => x.Date)
					.ThenBy(x => x.Time)
					.ToList();
				var lastSessionCheck = high5List
	.Where(x => x.Date.HasValue) // safety
	.GroupBy(x => new { x.UserId, x.PackageId })
	.Select(g =>
	{
		var lastDate = g.Max(x => x.Date!.Value);

		return new
		{
			UserId = g.Key.UserId,
			PackageId = g.Key.PackageId,
			LastSessionDate = lastDate,
			IsWithin5Days = (lastDate.Date - DateTime.Today).TotalDays <= 5
		};
	})
	.ToList();
				// --- Log if 5 days remain ---
				foreach (var check in lastSessionCheck)
				{
					if (check.IsWithin5Days)
					{
						_logger.LogInformation(
							"UserId {UserId}, PackageId {PackageId}: Yes, 5 days remaining. Last session on {LastSessionDate}",
							check.UserId, check.PackageId, check.LastSessionDate.ToString("yyyy-MM-dd"));
					}
				}

               

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




        // ================= NEW: Send Session Ending Reminders (5 Days & 1 Day) with Duplicate Prevention =================
        [HttpPost("clinic/{clinicId}/send-session-reminders")]
        public async Task<IActionResult> SendSessionEndingReminders(int clinicId)
        {
            try
            {
                _logger.LogInformation("Starting session reminder check for ClinicId {ClinicId}", clinicId);

                // Get all appointments with user data
                var high5Result = await _appointmentService.GetAppointmentsByClinicIdWithUserAsync(clinicId);

                if (!high5Result.Any())
                {
                    return Ok(ApiResponseFactory.Success(new
                    {
                        Message = "No appointments found for this clinic",
                        EmailsSent = 0,
                        EmailsFailed = 0,
                        TotalChecked = 0,
                        Skipped = 0
                    }));
                }

                // Find sessions ending in 5 days or 1 day
                var lastSessionCheck = high5Result
                    .Where(h => h.UserId > 0 && h.PackageId > 0)
                    .Where(h => h.PackageDate.Date >= DateTime.Today)
                    .GroupBy(h => new { h.UserId, h.PackageId, h.PackageName })
                    .Select(g => new
                    {
                        UserId = g.Key.UserId,
                        PackageId = g.Key.PackageId,
                        PackageName = g.Key.PackageName,
                        LastSession = g.OrderByDescending(x => x.PackageDate).First(),
                        LastSessionDate = g.Max(x => x.PackageDate),
                        DaysRemaining = (g.Max(x => x.PackageDate).Date - DateTime.Today).Days,
                        TotalSessions = g.Count()
                    })
                    .Where(x => x.DaysRemaining == 5 || x.DaysRemaining == 1)
                    .ToList();

                var emailsSent = 0;
                var emailsFailed = 0;
                var emailsSkipped = 0; // NEW
                var emailResults = new List<object>();

                // Send email reminders
                foreach (var check in lastSessionCheck)
                {
                    try
                    {
                        var reminderType = check.DaysRemaining == 1 ? "1-Day" : "5-Day";

                        // Check if reminder already sent
                        var alreadySent = await _reminderLogRepo.HasReminderBeenSentAsync(
                            check.UserId,
                            check.PackageId,
                            reminderType,
                            check.LastSessionDate
                        );

                        if (alreadySent)
                        {
                            emailsSkipped++;
                            emailResults.Add(new
                            {
                                UserId = check.UserId,
                                PackageId = check.PackageId,
                                PackageName = check.PackageName ?? "N/A",
                                PatientName = check.LastSession.User != null
                                    ? $"{check.LastSession.User.FirstName} {check.LastSession.User.LastName}".Trim()
                                    : "Unknown",
                                Email = check.LastSession.User?.Email ?? "N/A",
                                DaysRemaining = check.DaysRemaining,
                                LastSessionDate = check.LastSessionDate.ToString("yyyy-MM-dd"),
                                ReminderType = reminderType,
                                Status = "Skipped",
                                ErrorMessage = "Email already sent for this session"
                            });

                            _logger.LogInformation(
                                "{ReminderType} reminder already sent for UserId {UserId}, PackageId {PackageId}. Skipping.",
                                reminderType,
                                check.UserId,
                                check.PackageId
                            );
                            continue;
                        }

                        // Validate user and email
                        if (check.LastSession.User == null)
                        {
                            emailsFailed++;
                            emailResults.Add(new
                            {
                                UserId = check.UserId,
                                PackageId = check.PackageId,
                                PackageName = check.PackageName ?? "N/A",
                                PatientName = "Unknown",
                                Email = "N/A",
                                DaysRemaining = check.DaysRemaining,
                                LastSessionDate = check.LastSessionDate.ToString("yyyy-MM-dd"),
                                ReminderType = reminderType,
                                Status = "Failed",
                                ErrorMessage = "User not found"
                            });

                            _logger.LogWarning(
                                "User {UserId} not found for PackageId {PackageId}",
                                check.UserId,
                                check.PackageId
                            );
                            continue;
                        }

                        if (string.IsNullOrEmpty(check.LastSession.User.Email))
                        {
                            emailsFailed++;
                            var userName = $"{check.LastSession.User.FirstName} {check.LastSession.User.LastName}".Trim();
                            emailResults.Add(new
                            {
                                UserId = check.UserId,
                                PackageId = check.PackageId,
                                PackageName = check.PackageName ?? "N/A",
                                PatientName = userName,
                                Email = "N/A",
                                DaysRemaining = check.DaysRemaining,
                                LastSessionDate = check.LastSessionDate.ToString("yyyy-MM-dd"),
                                ReminderType = reminderType,
                                Status = "Failed",
                                ErrorMessage = "Email not found"
                            });

                            _logger.LogWarning(
                                "User {UserId} ({Name}) has no email for PackageId {PackageId}",
                                check.UserId,
                                userName,
                                check.PackageId
                            );
                            continue;
                        }

                        var user = check.LastSession.User;
                        var patientName = $"{user.FirstName} {user.LastName}".Trim();
                        var clinicName = check.LastSession.Clinic?.ClinicName ?? "High 5";

                        string emailTemplate;
                        string emailSubject;

                        // Choose template based on days remaining
                        if (check.DaysRemaining == 1)
                        {
                            emailTemplate = _emailTemplateService.GenerateSessionLastDayEmailTemplate(
                                patientName: patientName,
                                programName: check.PackageName ?? "Program",
                                clinicName: clinicName,
                                teamName: $"Team {clinicName}",
                                clinicId: clinicId
                            );
                            emailSubject = "⚡ Last Day of Your Session — Ready for What's Next?";
                        }
                        else
                        {
                            emailTemplate = _emailTemplateService.GenerateSessionEndingSoonEmailTemplate(
                                patientName: patientName,
                                programName: check.PackageName ?? "Program",
                                daysRemaining: 5,
                                clinicName: clinicName,
                                teamName: $"Team {clinicName}",
                                clinicId: clinicId
                            );
                            emailSubject = "⏳ Your Session is Ending Soon — Let's Keep the Momentum Going!";
                        }

                        // Send email
                        await _emailService.SendEmailAsync(
                            user.Email,
                            emailSubject,
                            emailTemplate
                        );

                        // Log the sent email to prevent duplicates
                        await _reminderLogRepo.CreateReminderLogAsync(new SessionReminderLog
                        {
                            ClinicId = clinicId,
                            UserId = check.UserId,
                            PackageId = check.PackageId,
                            ReminderType = reminderType,
                            LastSessionDate = check.LastSessionDate,
                            EmailSentTo = user.Email,
                            PackageName = check.PackageName,
                            SentAt = DateTime.UtcNow
                        });

                        emailsSent++;
                        emailResults.Add(new
                        {
                            UserId = check.UserId,
                            PackageId = check.PackageId,
                            PackageName = check.PackageName ?? "N/A",
                            PatientName = patientName,
                            Email = user.Email,
                            DaysRemaining = check.DaysRemaining,
                            LastSessionDate = check.LastSessionDate.ToString("yyyy-MM-dd"),
                            ReminderType = reminderType,
                            Status = "Sent",
                            ErrorMessage = ""
                        });

                        _logger.LogInformation(
                            "{ReminderType} reminder email sent to {Email} ({Name}) for Package '{Package}'. Days remaining: {Days}",
                            reminderType,
                            user.Email,
                            patientName,
                            check.PackageName,
                            check.DaysRemaining
                        );
                    }
                    catch (Exception emailEx)
                    {
                        emailsFailed++;
                        var userName = check.LastSession.User != null
                            ? $"{check.LastSession.User.FirstName} {check.LastSession.User.LastName}".Trim()
                            : "Unknown";
                        var userEmail = check.LastSession.User?.Email ?? "N/A";
                        var reminderType = check.DaysRemaining == 1 ? "1-Day" : "5-Day";

                        emailResults.Add(new
                        {
                            UserId = check.UserId,
                            PackageId = check.PackageId,
                            PackageName = check.PackageName ?? "N/A",
                            PatientName = userName,
                            Email = userEmail,
                            DaysRemaining = check.DaysRemaining,
                            LastSessionDate = check.LastSessionDate.ToString("yyyy-MM-dd"),
                            ReminderType = reminderType,
                            Status = "Failed",
                            ErrorMessage = emailEx.Message
                        });

                        _logger.LogError(
                            emailEx,
                            "Failed to send {ReminderType} reminder email for PackageId {PackageId}, UserId {UserId}",
                            reminderType,
                            check.PackageId,
                            check.UserId
                        );
                    }
                }

                var response = new
                {
                    Message = $"Session reminder check completed for {high5Result.Count()} total appointments",
                    EmailsSent = emailsSent,
                    EmailsFailed = emailsFailed,
                    EmailsSkipped = emailsSkipped,
                    TotalChecked = lastSessionCheck.Count,
                    FiveDayReminders = lastSessionCheck.Count(x => x.DaysRemaining == 5),
                    OneDayReminders = lastSessionCheck.Count(x => x.DaysRemaining == 1),
                    CheckedAt = DateTime.UtcNow,
                    Details = emailResults
                };

                return Ok(ApiResponseFactory.Success(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending session reminders for ClinicId {ClinicId}", clinicId);
                return StatusCode(500, ApiResponseFactory.Fail($"Error sending session reminders: {ex.Message}"));
            }
        }













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
						Count = g.Count()
					});

				// Get Enquiries
				var enquiriesResult = await _enquiryRepo.GetAllAsync(clinicId);

				// Create a list of dates for counting
				var enquiryDates = new List<DateTime>();

				foreach (var e in enquiriesResult)
				{
					if (e.Status == EnquiryStatus.Member) continue;

					// Add AppointmentDate if exists
					if (e.AppointmentDate.HasValue)
						enquiryDates.Add(e.AppointmentDate.Value.Date);

					// Add FollowUpDate if exists
					if (e.FollowUpDate.HasValue)
						enquiryDates.Add(e.FollowUpDate.Value.Date);

					// If neither exists, fallback to EpochTime
					if (!e.AppointmentDate.HasValue && !e.FollowUpDate.HasValue)
						enquiryDates.Add(DateTimeOffset.FromUnixTimeSeconds(e.EpochTime).Date);
				}

				var enquiryDaily = enquiryDates
					.Where(d => d >= startDate && d <= endDate)
					.GroupBy(d => d)
					.Select(g => new
					{
						Date = g.Key,
						Count = g.Count()
					});

				// Merge High5 + Enquiry counts
				var mergedDaily = high5Daily
					.Concat(enquiryDaily)
					.GroupBy(d => d.Date)
					.Select(g => new
					{
						Date = g.Key.ToString("yyyy-MM-dd"),
						Count = g.Sum(x => x.Count)
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
