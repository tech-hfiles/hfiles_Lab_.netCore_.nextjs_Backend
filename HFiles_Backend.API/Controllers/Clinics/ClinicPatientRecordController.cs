using HFiles_Backend.API.Controllers.Labs;
using HFiles_Backend.API.DTOs.Clinics;
using HFiles_Backend.API.Interfaces;
using HFiles_Backend.API.Services;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Clinics.PatientRecord;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Entities.Users;
using HFiles_Backend.Domain.Enums;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Domain.Interfaces.Clinics;
using HFiles_Backend.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using OfficeOpenXml;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using System.Globalization;
using System.Text;
using static Google.Apis.Requests.BatchRequest;
using static HFiles_Backend.API.DTOs.Clinics.ReceiptDocumentDTOs;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace HFiles_Backend.API.Controllers.Clinics
{
	[Route("api/")]
	[ApiController]
	public class ClinicPatientRecordController(

		ILogger<ClinicPatientRecordController> logger,
		IClinicAuthorizationService clinicAuthorizationService,
		IClinicRepository clinicRepository,
		IClinicPatientRecordRepository clinicPatientRecordRepository,
		IClinicHigh5AppointmentService appointmentService,

		S3StorageService s3StorageService,
		IClinicVisitRepository clinicVisitRepository,
		IUserRepository userRepository,
		IUniqueIdGeneratorService uniqueIdGenerator,
		IEmailTemplateService emailTemplateService,
		EmailService emailService,
		IClinicStatisticsCacheService cacheService,
		IHttpContextAccessor httpContextAccessor  // ← ADD THIS PARAMETER

	) : ControllerBase
	{
		private readonly IClinicHigh5AppointmentService _appointmentService;

		private readonly ILogger<ClinicPatientRecordController> _logger = logger;
		private readonly IClinicAuthorizationService _clinicAuthorizationService = clinicAuthorizationService;
		private readonly IClinicRepository _clinicRepository = clinicRepository;
		private readonly IClinicPatientRecordRepository _clinicPatientRecordRepository = clinicPatientRecordRepository;
		private readonly S3StorageService _s3StorageService = s3StorageService;
		private readonly IClinicVisitRepository _clinicVisitRepository = clinicVisitRepository;
		private readonly IUserRepository _userRepository = userRepository;
		private readonly IUniqueIdGeneratorService _uniqueIdGenerator = uniqueIdGenerator;
		private readonly IEmailTemplateService _emailTemplateService = emailTemplateService;
		private readonly EmailService _emailService = emailService;
		private readonly IClinicStatisticsCacheService _cacheService = cacheService;
		private readonly IHttpContextAccessor _httpContextAccessor; // ← ADD THIS



		private const int CLINIC_ID = 8;
		private const string DOCTOR_NAME = "Dr. Varun R Kunte";
		private readonly TimeSpan APPOINTMENT_TIME = new(10, 0, 0);



		[HttpPost("clinic/patient/records")]
		[Authorize]
		public async Task<IActionResult> SavePatientRecord([FromBody] ClinicPatientRecordCreateRequest request)
		{
			HttpContext.Items["Log-Category"] = "Patient Record Save";

			if (!ModelState.IsValid)
			{
				var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
				_logger.LogWarning("Validation failed for patient record. Errors: {@Errors}", errors);
				return BadRequest(ApiResponseFactory.Fail(errors));
			}

			bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(request.ClinicId, User);
			if (!isAuthorized)
			{
				_logger.LogWarning("Unauthorized record save attempt for Clinic ID {ClinicId}", request.ClinicId);
				return Unauthorized(ApiResponseFactory.Fail("You are not authorized to save records for this clinic."));
			}

			await using var transaction = await _clinicRepository.BeginTransactionAsync();
			bool committed = false;

			try
			{
				var parsedType = request.Type;

				// ✅ FIXED LOGIC: Only check for existing record if UniqueRecordId is provided
				if (!string.IsNullOrWhiteSpace(request.UniqueRecordId))
				{
					// UPDATE MODE: Find record by UniqueRecordId and update it
					var existingRecord = await _clinicPatientRecordRepository
						.GetByUniqueRecordIdAsync(request.ClinicId, request.UniqueRecordId);

					if (existingRecord is null)
					{
						_logger.LogWarning("Record with UniqueRecordId {UniqueRecordId} not found for Clinic ID {ClinicId}",
							request.UniqueRecordId, request.ClinicId);
						return NotFound(ApiResponseFactory.Fail($"Record with ID {request.UniqueRecordId} not found."));
					}

					// Update existing record with all properties
					existingRecord.JsonData = request.JsonData;
					existingRecord.PatientId = request.PatientId;
					existingRecord.ClinicVisitId = request.ClinicVisitId;

					// ✅ ADD THESE PROPERTY UPDATES
					if (request.Reference_Id.HasValue)
						existingRecord.Reference_Id = request.Reference_Id.Value;

					if (request.payment_verify.HasValue)
						existingRecord.payment_verify = request.payment_verify.Value;

					if (request.Is_Cansel.HasValue)
						existingRecord.Is_Cansel = request.Is_Cansel.Value;

					if (request.Is_editable.HasValue)
						existingRecord.Is_editable = request.Is_editable.Value;

					await _clinicPatientRecordRepository.UpdateAsync(existingRecord);

					_logger.LogInformation("Existing record {UniqueRecordId} updated for Clinic ID {ClinicId}, Patient ID {PatientId}, Visit ID {VisitId}, Type {Type}",
						existingRecord.UniqueRecordId, request.ClinicId, request.PatientId, request.ClinicVisitId, parsedType);

					await transaction.CommitAsync();
					committed = true;

					_cacheService.InvalidateClinicStatistics(request.ClinicId);

					return Ok(ApiResponseFactory.Success("Patient record updated successfully."));
				}
				else
				{
					// CREATE MODE: Always create new record when UniqueRecordId is not provided
					string uniqueId = string.Empty;

					// Generate unique ID for specific types
					if (parsedType == RecordType.Treatment ||
						parsedType == RecordType.Prescription ||
						parsedType == RecordType.Invoice ||
						parsedType == RecordType.Receipt ||
						parsedType == RecordType.MembershipPlan ||
						parsedType == RecordType.PhysiotherapyForm)
					{
						uniqueId = await _uniqueIdGenerator.GenerateUniqueIdAsync(request.ClinicId, parsedType);
					}

					// ✅ FIX: Map all properties from request to entity
					var newRecord = new ClinicPatientRecord
					{
						ClinicId = request.ClinicId,
						PatientId = request.PatientId,
						ClinicVisitId = request.ClinicVisitId,
						Type = parsedType,
						JsonData = request.JsonData,
						UniqueRecordId = uniqueId,
						Reference_Id = request.Reference_Id ?? 0,  // Map Reference_Id
						payment_verify = request.payment_verify ?? false,  // Map payment_verify
						Is_Cansel = request.Is_Cansel ?? false,  // Map Is_Cansel
						Is_editable = request.Is_editable ?? null  // Map Is_editable
					};

					await _clinicPatientRecordRepository.SaveAsync(newRecord);

					_logger.LogInformation("New record {UniqueRecordId} created for Clinic ID {ClinicId}, Patient ID {PatientId}, Visit ID {VisitId}, Type {Type}, Reference_Id {ReferenceId}",
						uniqueId, request.ClinicId, request.PatientId, request.ClinicVisitId, parsedType, request.Reference_Id);

					await transaction.CommitAsync();
					committed = true;

					_cacheService.InvalidateClinicStatistics(request.ClinicId);

					return Ok(ApiResponseFactory.Success("Patient record created successfully."));
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error saving/updating record for Clinic ID {ClinicId}", request.ClinicId);
				return StatusCode(500, ApiResponseFactory.Fail("An error occurred while saving the record."));
			}
			finally
			{
				if (!committed && transaction.GetDbTransaction().Connection != null)
					await transaction.RollbackAsync();
			}
		}

		// Stores JSON data
		//[HttpPost("clinic/patient/records")]
		//[Authorize]
		//public async Task<IActionResult> SavePatientRecord([FromBody] ClinicPatientRecordCreateRequest request)
		//{
		//    HttpContext.Items["Log-Category"] = "Patient Record Save";

		//    if (!ModelState.IsValid)
		//    {
		//        var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
		//        _logger.LogWarning("Validation failed for patient record. Errors: {@Errors}", errors);
		//        return BadRequest(ApiResponseFactory.Fail(errors));
		//    }

		//    bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(request.ClinicId, User);
		//    if (!isAuthorized)
		//    {
		//        _logger.LogWarning("Unauthorized record save attempt for Clinic ID {ClinicId}", request.ClinicId);
		//        return Unauthorized(ApiResponseFactory.Fail("You are not authorized to save records for this clinic."));
		//    }

		//    await using var transaction = await _clinicRepository.BeginTransactionAsync();
		//    bool committed = false;

		//    try
		//    {
		//        var parsedType = request.Type;

		//        var existingRecord = await _clinicPatientRecordRepository
		//            .GetByCompositeKeyAsync(request.ClinicId, request.PatientId, request.ClinicVisitId, parsedType);

		//        if (existingRecord is not null)
		//        {
		//            existingRecord.JsonData = request.JsonData;
		//            await _clinicPatientRecordRepository.UpdateAsync(existingRecord);

		//            _logger.LogInformation("Existing record updated for Clinic ID {ClinicId}, Patient ID {PatientId}, Visit ID {VisitId}, Type {Type}",
		//                request.ClinicId, request.PatientId, request.ClinicVisitId, parsedType);
		//        }
		//        else
		//        {
		//            // Generate unique ID for new records only
		//            string uniqueId = string.Empty;

		//            // Only generate IDs for these specific types
		//            if (parsedType == RecordType.Treatment ||
		//                parsedType == RecordType.Prescription ||
		//                parsedType == RecordType.Invoice ||
		//                parsedType == RecordType.Receipt || 
		//                parsedType == RecordType.MembershipPlan ||  
		//                parsedType == RecordType.PhysiotherapyForm )
		//            {
		//                uniqueId = await _uniqueIdGenerator.GenerateUniqueIdAsync(
		//                    request.ClinicId, parsedType);
		//            }

		//            var newRecord = new ClinicPatientRecord
		//            {
		//                ClinicId = request.ClinicId,
		//                PatientId = request.PatientId,
		//                ClinicVisitId = request.ClinicVisitId,
		//                Type = parsedType,
		//                JsonData = request.JsonData,
		//                UniqueRecordId = uniqueId
		//            };

		//            await _clinicPatientRecordRepository.SaveAsync(newRecord);

		//            _logger.LogInformation("New record created for Clinic ID {ClinicId}, Patient ID {PatientId}, Visit ID {VisitId}, Type {Type}",
		//                request.ClinicId, request.PatientId, request.ClinicVisitId, parsedType);
		//        }


		//        await transaction.CommitAsync();
		//        committed = true;

		//        // INVALIDATE CACHE AFTER SUCCESSFUL SAVE - ADD THIS
		//        _cacheService.InvalidateClinicStatistics(request.ClinicId);

		//        return Ok(ApiResponseFactory.Success("Patient record saved successfully."));
		//    }
		//    catch (Exception ex)
		//    {
		//        _logger.LogError(ex, "Error saving/updating record for Clinic ID {ClinicId}", request.ClinicId);
		//        return StatusCode(500, ApiResponseFactory.Fail("An error occurred while saving the record."));
		//    }
		//    finally
		//    {
		//        if (!committed && transaction.GetDbTransaction().Connection != null)
		//            await transaction.RollbackAsync();
		//
		public class UserData
		{
			public int id { get; set; }
			public string Username { get; set; }
			public string UserEmail { get; set; }
			public string UserProfile { get; set; }
			public string DOB { get; set; }
			public string Gender { get; set; }
			public string Phone { get; set; }
		}
		public class HfidApiResponse
		{
			public bool success { get; set; }
			public UserData data { get; set; }
			public string message { get; set; }
		}
		public class MepJsonData
		{
			public PatientInfo patient { get; set; }
			public List<TreatmentInfo> treatments { get; set; }
		}

		public class PatientInfo
		{
			public string hfid { get; set; }
			public string name { get; set; }
		}

		public class TreatmentInfo
		{
			public string name { get; set; }
			public int coachId { get; set; }
			public int packageID { get; set; }
			public string couch { get; set; }

			public List<string> sessionDates { get; set; }
			public List<string> sessionTimes { get; set; }
		}

		public class AppointmentRequest
		{
			public int clinicId { get; set; }
			public int userId { get; set; }
			public int packageId { get; set; }
			public string packageName { get; set; }
			public DateTime packageDate { get; set; }
			public string packageTime { get; set; }
			public int coachId { get; set; }
			public string status { get; set; }
			public int PatientId { get; set; }

			public string UniqueRecordId { get; set; }
			public int ClinicVisitId { get; set; }
		}

		private async Task CreateAppointment(AppointmentRequest request)
		{
			using (var httpClient = new HttpClient())
			{
				string baseUrl;

#if DEBUG
				baseUrl = "https://localhost:7227/api/";
#elif STAGING
        baseUrl = "https://testlab.hfiles.co.in/api/";
#else
        baseUrl = "https://api.hfiles.co.in/api/";
#endif

				httpClient.BaseAddress = new Uri(baseUrl);

				var json = JsonConvert.SerializeObject(request);
				var content = new StringContent(json, Encoding.UTF8, "application/json");
				//var response = await _appointmentService.CreateAppointmentAsync(request);
				var response = await httpClient.PostAsync("/api/clinics/high5-appointments", content);

				// Log the status code
				_logger.LogInformation("Create appointment response: {Status}", response.StatusCode);

				// Or if you want more details:
				var responseBody = await response.Content.ReadAsStringAsync();
				_logger.LogInformation("Create appointment response: {Status}, Body: {Body}, dataa= {request}",
					response.StatusCode, responseBody, request);


				if (!response.IsSuccessStatusCode)
				{
					// Log error
					var error = await response.Content.ReadAsStringAsync();
					_logger.LogInformation("Create appointment response: {error}", error);

					Console.WriteLine($"Failed to create appointment: {error}");
				}
			}
		}


		private async Task CreateAppointmentsFromMepData(string jsonData, string packageId, int clinicId , int PatientId,int ClinicVisitId, string UniqueRecordId)
		{

			try
			{
				// Parse the JSON data
				var mepData = JsonConvert.DeserializeObject<MepJsonData>(jsonData);
				if (mepData?.patient == null || mepData?.treatments == null)
				{
					_logger.LogWarning("Invalid MEP data: patient or treatments is null");
					return;
				}

				// Get user ID by calling the HFID API
				int? userId = await GetUserIdByHfId(mepData.patient.hfid);
				if (userId == null)
				{
					_logger.LogWarning("User not found with HFID: {HFID}", mepData.patient.hfid);
					return;
				}

				_logger.LogInformation("Entered CreateAppointmentsFromMepData for UserId: {UserId}, HFID: {HFID}",
					userId, mepData.patient.hfid);

				// Loop through each treatment and create appointments
				foreach (var treatment in mepData.treatments)
				{
					

					for (int i = 0; i < treatment.sessionDates.Count; i++)
					{
						try
						{
							var appointment = new AppointmentRequest
							{
								clinicId = clinicId,
								userId = userId.Value,
								packageId = treatment.packageID,
								packageName = treatment.name,
								packageDate = DateTime.Parse(treatment.sessionDates[i]),
								packageTime = treatment.sessionTimes[i],
								coachId = treatment.coachId,
								status = "Scheduled",
								ClinicVisitId = ClinicVisitId,
								PatientId = PatientId,
								UniqueRecordId = UniqueRecordId
							};



							// Call your API or repository to create appointment
							await CreateAppointment(appointment);

							_logger.LogInformation("Successfully created appointment {Index}/{Total} for treatment {TreatmentName}",
								i + 1, treatment.sessionDates.Count, treatment.name);
						}
						catch (Exception ex)
						{
							_logger.LogError(ex,
								"Failed to create appointment {Index}/{Total} for HFID {HFID}, Treatment: {TreatmentName}, Date: {Date}",
								i + 1,
								treatment.sessionDates.Count,
								mepData.patient.hfid,
								treatment.name,
								treatment.sessionDates[i]
							);
							// Continue with next appointment instead of failing completely
						}
					}
				}

				_logger.LogInformation("Completed CreateAppointmentsFromMepData for HFID: {HFID}",
					mepData.patient.hfid);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in CreateAppointmentsFromMepData for packageId {PackageId}", packageId);
			}
		}








		private async Task<int?> GetUserIdByHfId(string hfid)
		{
			try
			{
				using (var httpClient = new HttpClient())
				{
					string baseUrl;

#if DEBUG
					baseUrl = "https://localhost:7227/api/";
#elif STAGING
        baseUrl = "https://testlab.hfiles.co.in/api/";
#else
        baseUrl = "https://api.hfiles.co.in/api/";
#endif

					httpClient.BaseAddress = new Uri(baseUrl);// Replace with your actual API base URL

					var requestBody = new { HFID = hfid };
					var json = JsonConvert.SerializeObject(requestBody);
					var content = new StringContent(json, Encoding.UTF8, "application/json");

					var response = await httpClient.PostAsync("/api/users/hfid", content);

					if (response.IsSuccessStatusCode)
					{
						var responseContent = await response.Content.ReadAsStringAsync();
						var apiResponse = JsonConvert.DeserializeObject<HfidApiResponse>(responseContent);

						return apiResponse?.data?.id;
					}
					else
					{
						Console.WriteLine($"Failed to get user ID for HFID: {hfid}");
						return null;
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error getting user ID: {ex.Message}");
				return null;
			}
		}

		// Fetch JSON Data
		[HttpGet("clinic/{clinicId}/patient/{patientId}/visit/{clinicVisitId}/records")]
		[Authorize]
		public async Task<IActionResult> GetPatientRecordsByVisit(
			[FromRoute] int clinicId,
			[FromRoute] int patientId,
			[FromRoute] int clinicVisitId)
		{

			HttpContext.Items["Log-Category"] = "Patient Record Fetch";

			if (clinicId <= 0 || patientId <= 0 || clinicVisitId <= 0)
			{
				_logger.LogWarning("Invalid IDs. ClinicId: {ClinicId}, PatientId: {PatientId}, VisitId: {VisitId}",
					clinicId, patientId, clinicVisitId);
				return BadRequest(ApiResponseFactory.Fail("Clinic ID, Patient ID, and Visit ID must be positive integers."));
			}

			bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
			if (!isAuthorized)
			{
				_logger.LogWarning("Unauthorized record fetch attempt for Clinic ID {ClinicId}", clinicId);
				return Unauthorized(ApiResponseFactory.Fail("You are not authorized to view records for this clinic."));
			}

			try
			{

				var records = await _clinicPatientRecordRepository.GetByClinicPatientVisitAsync(
					clinicId, patientId, clinicVisitId);

				var recordsList = records.ToList(); // Must materialize first



				var response = recordsList.Select(r =>
				{


					return new ClinicPatientRecordResponse
					{
						Id = r.Id,
						Type = r.Type,
						JsonData = r.JsonData,
						UniqueRecordId = r.UniqueRecordId,
						EpochTime = r.EpochTime,
						PaymentVerify = r.payment_verify
					};
				}).ToList();



				_logger.LogInformation(
					"Fetched {Count} records for Clinic ID {ClinicId}, Patient ID {PatientId}, Visit ID {VisitId}",
					response.Count, clinicId, patientId, clinicVisitId);

				return Ok(ApiResponseFactory.Success(response, "Records fetched successfully."));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error fetching records for Clinic ID {ClinicId}, Visit ID {VisitId}",
					clinicId, clinicVisitId);
				return StatusCode(500, ApiResponseFactory.Fail("An error occurred while fetching records."));
			}
		}




		// Upload Patient Images
		[HttpPost("clinic/patient/records/upload")]
		[Authorize]
		public async Task<IActionResult> UploadPatientRecordFiles(
		[FromForm] ClinicPatientRecordFileUploadRequest request)
		{
			HttpContext.Items["Log-Category"] = "Patient Record Bulk Upload";

			if (!ModelState.IsValid || request.Files == null || request.Files.Count == 0)
			{
				var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
				_logger.LogWarning("Validation failed for bulk upload. Errors: {@Errors}", errors);
				return BadRequest(ApiResponseFactory.Fail(errors));
			}

			// File type validation
			var allowedExtensions = new[] { ".pdf", ".png", ".jpg", ".jpeg", ".zip" };
			foreach (var file in request.Files)
			{
				var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
				var fileSize = file.Length;

				if (fileSize > 50 * 1024 * 1024)
					return BadRequest(ApiResponseFactory.Fail("File size exceeds the 50MB limit."));

				if (!allowedExtensions.Contains(extension) ||
					(!file.ContentType.StartsWith("application/") && !file.ContentType.StartsWith("image/")))
				{
					_logger.LogWarning("Invalid file type attempted: {FileName} with ContentType: {ContentType}",
						file.FileName, file.ContentType);
					return BadRequest(ApiResponseFactory.Fail("Unsupported file type. Only PDF, JPG, JPEG, PNG, and ZIP files are allowed."));
				}
			}

			bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(request.ClinicId, User);
			if (!isAuthorized)
			{
				_logger.LogWarning("Unauthorized bulk upload attempt for Clinic ID {ClinicId}", request.ClinicId);
				return Unauthorized(ApiResponseFactory.Fail("You are not authorized to upload records for this clinic."));
			}

			await using var transaction = await _clinicRepository.BeginTransactionAsync();
			bool committed = false;

			try
			{
				var s3Urls = new List<string>();

				foreach (var file in request.Files)
				{
					var fileName = $"{request.PatientId}_{request.ClinicVisitId}_{Path.GetFileName(file.FileName)}";
					var tempPath = Path.Combine(Path.GetTempPath(), fileName);

					using (var stream = new FileStream(tempPath, FileMode.Create))
					{
						await file.CopyToAsync(stream);
					}

					var s3Url = await _s3StorageService.UploadFileToS3(tempPath, $"clinic-records/{fileName}");
					if (s3Url != null)
						s3Urls.Add(s3Url);

					System.IO.File.Delete(tempPath);
				}

				var record = new ClinicPatientRecord
				{
					ClinicId = request.ClinicId,
					PatientId = request.PatientId,
					ClinicVisitId = request.ClinicVisitId,
					Type = request.Type,
					JsonData = System.Text.Json.JsonSerializer.Serialize(s3Urls)
				};

				await _clinicPatientRecordRepository.SaveAsync(record);
				await transaction.CommitAsync();
				committed = true;

				// INVALIDATE CACHE AFTER SUCCESSFUL UPLOAD - ADD THIS
				_cacheService.InvalidateClinicStatistics(request.ClinicId);

				_logger.LogInformation("Uploaded {Count} files for Clinic ID {ClinicId}, Patient ID {PatientId}", s3Urls.Count, request.ClinicId, request.PatientId);
				return Ok(ApiResponseFactory.Success("Files saved successfully."));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during bulk upload for Clinic ID {ClinicId}", request.ClinicId);
				return StatusCode(500, ApiResponseFactory.Fail("An error occurred while uploading files."));
			}
			finally
			{
				if (!committed && transaction.GetDbTransaction().Connection != null)
					await transaction.RollbackAsync();
			}
		}





		// Mass Send to User
		[HttpPost("clinic/patient/documents/upload")]
		[Authorize]
		public async Task<IActionResult> UploadPatientDocuments([FromForm] ClinicPatientDocumentUploadRequest request)
		{
			HttpContext.Items["Log-Category"] = "Clinic Patient Documents Upload";

			if (!ModelState.IsValid || request.Documents == null || !request.Documents.Any())
				return BadRequest(ApiResponseFactory.Fail("Invalid request. Documents are required."));

			bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(request.ClinicId, User);
			if (!isAuthorized)
				return Unauthorized(ApiResponseFactory.Fail("You are not authorized to upload documents for this clinic."));

			var visit = await _clinicVisitRepository.GetByIdAsync(request.ClinicVisitId);
			if (visit == null || visit.ClinicId != request.ClinicId)
				return NotFound(ApiResponseFactory.Fail("Clinic visit not found."));

			visit.PaymentMethod = request.PaymentMethod;

			var clinicPatient = await _clinicPatientRecordRepository.GetByIdAsync(request.PatientId);
			if (clinicPatient == null)
				return NotFound(ApiResponseFactory.Fail("Clinic patient not found."));

			var user = await _userRepository.GetUserByHFIDAsync(clinicPatient.HFID);
			if (user == null)
				return NotFound(ApiResponseFactory.Fail("User not found for provided HFID."));

			HttpContext.Items["Sent-To-UserId"] = user.Id;

			// Get clinic details
			var clinic = await _clinicRepository.GetByIdAsync(request.ClinicId);
			if (clinic == null)
			{
				_logger.LogWarning("Clinic not found for Clinic ID: {ClinicId}", request.ClinicId);
				return NotFound(ApiResponseFactory.Fail($"Clinic with ID {request.ClinicId} not found."));
			}

			var clinicName = clinic.ClinicName ?? "Clinic";

			await using var transaction = await _clinicRepository.BeginTransactionAsync();
			bool committed = false;

			try
			{
				var uploadedDocumentDetails = new List<string>();
				var patientDocumentInfoList = new List<PatientDocumentInfo>();

				foreach (var doc in request.Documents)
				{
					var uploadedExtension = Path.GetExtension(doc.PdfFile?.FileName)?.ToLower();
					var fileSize = doc.PdfFile?.Length;

					if (fileSize > 100 * 1024 * 1024)
						return BadRequest(ApiResponseFactory.Fail("File size exceeds the 100MB limit."));

					if ((doc.Type == RecordType.Invoice || doc.Type == RecordType.Receipt || doc.Type == RecordType.HfPdf) && doc.PdfFile == null)
						return BadRequest(ApiResponseFactory.Fail($"{doc.Type} PDF is required."));

					if (doc.Type == RecordType.Images)
					{
						var existingRecord = await _clinicPatientRecordRepository
							.GetReportImageRecordAsync(request.ClinicId, request.PatientId, request.ClinicVisitId);

						if (existingRecord == null)
							return BadRequest(ApiResponseFactory.Fail("ReportImage record not found for this visit."));

						existingRecord.SendToPatient = doc.SendToPatient;
						await _clinicPatientRecordRepository.UpdateAsync(existingRecord);

						// Parse image URLs from JsonData
						if (!string.IsNullOrWhiteSpace(existingRecord.JsonData))
						{
							try
							{
								var imageUrls = System.Text.Json.JsonSerializer.Deserialize<List<string>>(existingRecord.JsonData);
								if (imageUrls != null)
								{
									foreach (var imageUrl in imageUrls)
									{
										var report = new UserReport
										{
											UserId = user.Id,
											ReportName = "Images",
											ReportCategory = (int)ReportType.LabReport,
											ReportUrl = imageUrl,
											EpochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
											FileSize = 0,
											UploadedBy = "Clinic",
											UserType = user.UserReference == 0 ? "Independent" : "Dependent",
											DeletedBy = 0
										};

										await _userRepository.SaveAsync(report);
									}

									var categoryName = "Lab Reports";
									uploadedDocumentDetails.Add($"Images ({categoryName})");
									patientDocumentInfoList.Add(new PatientDocumentInfo
									{
										DocumentType = "Images",
										Category = categoryName,
										DocumentUrl = imageUrls.FirstOrDefault() ?? ""
									});
								}
							}
							catch (Exception ex)
							{
								_logger.LogError(ex, "Failed to parse image URLs from JsonData for Patient ID {PatientId}", request.PatientId);
							}
						}

						continue;
					}

					string jsonData = string.Empty;
					string? s3Url = null;

					if (doc.PdfFile != null)
					{
						var fileName = $"{doc.Type.ToString().ToLower()}_clinic{request.ClinicId}_patient{request.PatientId}_{Guid.NewGuid()}{Path.GetExtension(doc.PdfFile.FileName)}";
						var tempPath = Path.Combine(Path.GetTempPath(), fileName);

						using (var stream = new FileStream(tempPath, FileMode.Create))
							await doc.PdfFile.CopyToAsync(stream);

						s3Url = await _s3StorageService.UploadFileToS3(tempPath, $"clinic/{fileName}");

						// Delete temp file immediately after upload
						System.IO.File.Delete(tempPath);

						if (s3Url == null)
							return StatusCode(500, ApiResponseFactory.Fail("Failed to upload file to S3."));

						jsonData = System.Text.Json.JsonSerializer.Serialize(new { url = s3Url });
					}

					var record = new ClinicPatientRecord
					{
						ClinicId = request.ClinicId,
						PatientId = request.PatientId,
						ClinicVisitId = request.ClinicVisitId,
						Type = doc.Type,
						JsonData = jsonData,
						SendToPatient = doc.SendToPatient,
						Reference_Id = doc.ReferenceId ?? 0,           // Map ReferenceId to Reference_Id
						payment_verify = doc.PaymentVerify,            // Map PaymentVerify to payment_verify
						Is_editable = doc.IsEditable
					};

					await _clinicPatientRecordRepository.SaveAsync(record);

					if (s3Url != null)
					{
						var reportCategory = doc.Type switch
						{
							RecordType.Prescription => (int)ReportType.MedicationsPrescription,
							RecordType.Treatment => (int)ReportType.MedicationsPrescription,
							RecordType.Invoice => (int)ReportType.InvoicesInsurance,
							RecordType.Receipt => (int)ReportType.InvoicesInsurance,
							RecordType.Images => (int)ReportType.LabReport,
							RecordType.HfPdf => (int)ReportType.SpecialReport,
							_ => (int)ReportType.Unknown
						};

						var report = new UserReport
						{
							UserId = user.Id,
							ReportName = $"{doc.Type}",
							ReportCategory = reportCategory,
							ReportUrl = s3Url,
							EpochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
							FileSize = Math.Round((decimal)(doc.PdfFile?.Length ?? 0) / 1024, 2),
							UploadedBy = "Clinic",
							UserType = user.UserReference == 0 ? "Independent" : "Dependent",
							DeletedBy = 0
						};

						await _userRepository.SaveAsync(report);

						// Add document detail with category and URL for email
						var categoryName = doc.Type switch
						{
							RecordType.Prescription => "Medications/Prescription",
							RecordType.Treatment => "Lab Reports",
							RecordType.Invoice => "Invoices/Insurance",
							RecordType.Receipt => "Invoices/Insurance",
							RecordType.Images => "Lab Reports",
							RecordType.HfPdf => "SpecialReport",
							_ => "Unknown"
						};

						uploadedDocumentDetails.Add($"{doc.Type} ({categoryName})");
						patientDocumentInfoList.Add(new PatientDocumentInfo
						{
							DocumentType = doc.Type.ToString(),
							Category = categoryName,
							DocumentUrl = s3Url
						});
					}
				}

				await _clinicVisitRepository.UpdateAsync(visit);

				// Commit transaction BEFORE sending email (so data is saved regardless of email success)
				await transaction.CommitAsync();
				committed = true;

				// Invalidate cache after successful upload
				_cacheService.InvalidateClinicStatistics(request.ClinicId);

				var patientName = clinicPatient?.PatientName ?? $"{user?.FirstName} {user?.LastName}" ?? "N/A";
				string appointmentDate = visit?.AppointmentDate != null
					? visit.AppointmentDate.ToString("dd-MM-yyyy")
					: "N/A";

				string appointmentTime = visit != null
				   ? visit.AppointmentTime.ToString(@"hh\:mm")
				   : "N/A";

				var uploadedDocs = request.Documents.Select(d => d.Type.ToString()).ToList();

				var documentsFormatted = string.Join("\n", uploadedDocumentDetails.Select((doc, index) =>
					$"{index + 1}. {doc}"));

				var userNotificationMessage = $"{clinicName} has uploaded {uploadedDocumentDetails.Count} document(s) to your account. You can view them in the All Reports section under the following categories:\n\n{documentsFormatted}";

				// ==================== SEND EMAIL NOTIFICATION ====================
				// Only send email if email is provided
				bool emailSent = false;
				string? emailSentTo = null;

				if (!string.IsNullOrWhiteSpace(user?.Email))
				{
					try
					{
						var patientFirstName = user.FirstName ?? "Patient";

						var emailTemplate = _emailTemplateService.GeneratePatientDocumentsUploadedEmailTemplate(
							patientFirstName,
							patientDocumentInfoList,
							clinicName,
							appointmentDate,
							appointmentTime
						);

						// Send email without attachments (view buttons in email instead)
						await _emailService.SendEmailAsync(
							user.Email,
							$"Documents Uploaded - {clinicName}",
							emailTemplate
						);

						emailSent = true;
						emailSentTo = user.Email;

						_logger.LogInformation(
							"Document upload email sent successfully to {Email} with {Count} documents",
							user.Email, uploadedDocumentDetails.Count);
					}
					catch (Exception emailEx)
					{
						_logger.LogError(emailEx,
							"Failed to send document upload email to {Email} for Patient {PatientId}",
							user.Email, request.PatientId);
						// Don't fail the entire operation if email fails
					}
				}
				else
				{
					_logger.LogInformation(
						"Email not provided for Patient {PatientName} (HFID: {HFID}). Skipping email notification.",
						patientName, clinicPatient?.HFID);
				}

				// Response + Notification
				var response = new
				{
					PatientName = patientName,
					PatientHFID = clinicPatient?.HFID,
					ClinicName = clinicName,
					Email = string.IsNullOrWhiteSpace(user?.Email) ? "Not provided" : user.Email,
					AppointmentDate = appointmentDate,
					AppointmentTime = appointmentTime,
					UploadedDocuments = uploadedDocs,
					DocumentDetails = uploadedDocumentDetails,
					SentAt = DateTime.UtcNow,

					// Email status
					EmailSent = emailSent,
					SentToEmail = emailSentTo,

					NotificationMessage =
					 $"Documents ({string.Join(", ", uploadedDocs)}) uploaded for patient {patientName} on HF account {clinicPatient?.HFID} for {appointmentDate} at {appointmentTime}.",
					UserNotificationMessage = userNotificationMessage,

					NotificationContext = new
					{
						ClinicId = request.ClinicId,
						ClinicName = clinicName,
						PatientId = request.PatientId,
						ClinicVisitId = request.ClinicVisitId,
						HFID = clinicPatient?.HFID,
						PatientName = patientName,
						AppointmentDate = appointmentDate,
						AppointmentTime = appointmentTime,
						UploadedDocuments = uploadedDocs,
						DocumentDetails = uploadedDocumentDetails,
						EmailStatus = emailSent
							? "Email sent"
							: string.IsNullOrWhiteSpace(user?.Email)
								? "No email provided"
								: "Email sending failed"
					}
				};

				_logger.LogInformation(
					"Uploaded documents ({Documents}) for Clinic ID {ClinicId}, Patient ID {PatientId}, Appointment on {AppointmentDate} {AppointmentTime}. Email sent: {EmailSent}",
					string.Join(", ", uploadedDocs), request.ClinicId, request.PatientId, appointmentDate, appointmentTime, emailSent
				);

				return Ok(ApiResponseFactory.Success(response, "Documents uploaded successfully."));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error uploading documents for Clinic ID {ClinicId}", request.ClinicId);
				return StatusCode(500, ApiResponseFactory.Fail("An error occurred while uploading documents."));
			}
			finally
			{
				if (!committed && transaction.GetDbTransaction().Connection != null)
					await transaction.RollbackAsync();
			}
		}


		[HttpPut("clinic/patient/documents/verify-payment/{receiptRecordId}")]
		[Authorize]
		public async Task<IActionResult> VerifyPayment(int receiptRecordId)
		{
			try
			{
				var receiptRecord = await _clinicPatientRecordRepository.GetRecordByIdAsync(receiptRecordId);
				if (receiptRecord == null)
					return NotFound(ApiResponseFactory.Fail("Receipt record not found."));

				if (receiptRecord.Type != RecordType.Receipt)
					return BadRequest(ApiResponseFactory.Fail("Record is not a receipt type."));

				// Check if UniqueRecordId is null or empty
				if (string.IsNullOrWhiteSpace(receiptRecord.UniqueRecordId))
					return BadRequest(ApiResponseFactory.Fail("Receipt does not have a valid UniqueRecordId."));

				// Check if another receipt with the same UniqueRecordId already exists with payment verified
				var existingVerifiedReceipt = await _clinicPatientRecordRepository
					.GetRecordByUniqueRecordIdAsync(receiptRecord.UniqueRecordId);

				if (existingVerifiedReceipt != null &&
					existingVerifiedReceipt.Id != receiptRecordId &&
					existingVerifiedReceipt.payment_verify == true &&
					existingVerifiedReceipt.Is_editable == true)
				{
					return BadRequest(ApiResponseFactory.Fail("A receipt with this UniqueRecordId is already verified and editable."));
				}

				// Update Receipt
				receiptRecord.payment_verify = true;
				receiptRecord.Is_editable = true;
				await _clinicPatientRecordRepository.UpdateAsync(receiptRecord);

				// Update Invoice
				if (receiptRecord.Reference_Id.HasValue && receiptRecord.Reference_Id.Value > 0)
				{
					var invoiceRecord = await _clinicPatientRecordRepository.GetRecordByIdAsync(receiptRecord.Reference_Id.Value);
					if (invoiceRecord != null && invoiceRecord.Type == RecordType.Invoice)
					{
						invoiceRecord.payment_verify = true;
						invoiceRecord.Is_editable = true;
						await _clinicPatientRecordRepository.UpdateAsync(invoiceRecord);

						// Update Package/MEP
						if (invoiceRecord.Reference_Id.HasValue && invoiceRecord.Reference_Id.Value > 0)
						{
							var packageRecord = await _clinicPatientRecordRepository.GetRecordByIdAsync(invoiceRecord.Reference_Id.Value);

                            // After updating Package/MEP (around line 56)
                            if (packageRecord != null && packageRecord.Type == RecordType.MembershipPlan)
                            {
                                packageRecord.payment_verify = true;
                                packageRecord.Is_editable = true;
                                await _clinicPatientRecordRepository.UpdateAsync(packageRecord);

                                // Extract the data you need here
                                string packageJsonData = packageRecord.JsonData;
                                string packageUniqueRecordId = packageRecord.UniqueRecordId;
                                string packageId = packageRecord.Id.ToString();
                                int clinicId = 36; // or get from receiptRecord if it has a ClinicId property

								// Create appointments
								await CreateAppointmentsFromMepData(
									packageJsonData,
									packageId,
									clinicId,
									packageRecord.PatientId,
									packageRecord.ClinicVisitId,
									packageRecord.UniqueRecordId
								);
                                if (clinicId != 36)
                                {
                                    try
								{
									// Parse JSON data
									// Use System.Text.Json (built-in)
									var mepData = System.Text.Json.JsonSerializer.Deserialize<MepJsonData>(
										packageJsonData,
										new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
									);
									if (mepData?.treatments != null && mepData.treatments.Count > 0)
									{

										string coachName = ""; 
										int coachIndex = packageJsonData.IndexOf("\"coach\":");
										if (coachIndex != -1)
										{
											int startQuote = packageJsonData.IndexOf("\"", coachIndex + 8);
											if (startQuote != -1)
											{
												int endQuote = packageJsonData.IndexOf("\"", startQuote + 1);
												if (endQuote != -1)
												{
													 coachName = packageJsonData.Substring(startQuote + 1, endQuote - startQuote - 1);
												}
											}
										}
										var firstTreatment = mepData.treatments[0];
										var firstSessionDate = firstTreatment.sessionDates?.FirstOrDefault();
										var firstSessionTime = firstTreatment.sessionTimes?.FirstOrDefault();

										if (!string.IsNullOrEmpty(firstSessionDate) && !string.IsNullOrEmpty(firstSessionTime))
										{
											// Get clinic details
											var clinic = await _clinicRepository.GetClinicByIdAsync(clinicId);

											// Format date (convert from yyyy-MM-dd to dd-MM-yyyy)
											DateTime.TryParseExact(firstSessionDate, "yyyy-MM-dd", null,
												System.Globalization.DateTimeStyles.None, out var parsedDate);
											var formattedDate = parsedDate.ToString("dd-MM-yyyy");

											// Format time (HH:mm:ss to hh:mm tt)
											TimeSpan.TryParse(firstSessionTime, out var parsedTime);
											var formattedTime = DateTime.Today.Add(parsedTime).ToString("hh:mm tt");

											// Generate email template
											var emailTemplate = _emailTemplateService.GenerateFirstSessionConfirmationEmailTemplate(
												mepData.patient.name,
												coachName,
												formattedDate,
												formattedTime,
												clinic?.ClinicName ?? "High 5"
											);

											// Get user email from HFID
											var user = await _userRepository.GetUserByHFIDAsync(mepData.patient.hfid);
											if (user != null && !string.IsNullOrEmpty(user.Email))
											{
												await _emailService.SendEmailAsync(
													user.Email,
													"Your Fitness Journey Begins Today! 🔥",
													emailTemplate
												);

												_logger.LogInformation(
													"First session confirmation email sent to {Email} for {Date} at {Time}",
													user.Email,
													formattedDate,
													formattedTime
												);
											}
										}
									}
								}
								catch (Exception emailEx)
								{
									_logger.LogError(
										emailEx,
										"Failed to send first session confirmation email for package {PackageId}",
										packageId
									);
									// Don't fail the entire operation if email fails
								}
                                }
                                else
                                {
                                    _logger.LogInformation("Email sending skipped for clinicId {ClinicId}", clinicId);
                                }

                            }
							//if (packageRecord != null && packageRecord.Type == RecordType.MembershipPlan)
							//                     {
							//                         packageRecord.payment_verify = true;
							//                         packageRecord.Is_editable = true;
							//                         await _clinicPatientRecordRepository.UpdateAsync(packageRecord);
							//                     }
						}
					}
				}


				return Ok(ApiResponseFactory.Success("Payment verification updated successfully."));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error updating payment verification");
				return StatusCode(500, ApiResponseFactory.Fail("An error occurred."));
			}
		}





		// Patient History
		[HttpGet("clinic/{clinicId}/patient/{patientId}/history")]
		[Authorize]
		public async Task<IActionResult> GetPatientHistory(
			   int clinicId,
			   int patientId,
			   [FromQuery] string? startDate,
			   [FromQuery] string? endDate,
			   [FromQuery] string? categories,
			   [FromServices] ClinicPatientRecordRepository clinicPatientRecordRepository)
		{
			HttpContext.Items["Log-Category"] = "Patient History Fetch";

			if (clinicId <= 0 || patientId <= 0)
			{
				_logger.LogWarning("Invalid Clinic ID or Patient ID. ClinicId: {ClinicId}, PatientId: {PatientId}", clinicId, patientId);
				return BadRequest(ApiResponseFactory.Fail("Clinic ID and Patient ID must be valid."));
			}

			bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
			if (!isAuthorized)
			{
				_logger.LogWarning("Unauthorized history fetch attempt for Clinic ID {ClinicId}", clinicId);
				return Unauthorized(ApiResponseFactory.Fail("You are not authorized to view this patient's history."));
			}

			// Parse date filters
			DateTime? start = null;
			DateTime? end = null;

			if (!string.IsNullOrEmpty(startDate))
			{
				if (!DateTime.TryParseExact(startDate, "dd-MM-yyyy", null, DateTimeStyles.None, out var parsedStart))
				{
					_logger.LogWarning("Invalid startDate format: {StartDate}", startDate);
					return BadRequest(ApiResponseFactory.Fail("Invalid startDate format. Expected dd-MM-yyyy."));
				}
				start = parsedStart;
			}

			if (!string.IsNullOrEmpty(endDate))
			{
				if (!DateTime.TryParseExact(endDate, "dd-MM-yyyy", null, DateTimeStyles.None, out var parsedEnd))
				{
					_logger.LogWarning("Invalid endDate format: {EndDate}", endDate);
					return BadRequest(ApiResponseFactory.Fail("Invalid endDate format. Expected dd-MM-yyyy."));
				}
				end = parsedEnd;
			}

			// Parse category filters
			var categoryFilters = new List<string>();
			if (!string.IsNullOrEmpty(categories))
			{
				categoryFilters = categories.Split(',', StringSplitOptions.RemoveEmptyEntries)
					.Select(c => c.Trim().ToLowerInvariant())
					.ToList();

				// Validate categories
				var validCategories = new List<string>
				{
					"dtr consent", "tmd/tmjp consent", "photo consent",
					"arthrose functional screening consent", "prescription",
					"treatment", "invoice", "receipt"
				};

				var invalidCategories = categoryFilters.Except(validCategories).ToList();
				if (invalidCategories.Any())
				{
					_logger.LogWarning("Invalid categories provided: {InvalidCategories}", string.Join(", ", invalidCategories));
					return BadRequest(ApiResponseFactory.Fail($"Invalid categories: {string.Join(", ", invalidCategories)}. Valid categories are: {string.Join(", ", validCategories)}"));
				}
			}

			await using var transaction = await _clinicRepository.BeginTransactionAsync();
			bool committed = false;

			try
			{
				var history = await clinicPatientRecordRepository.GetPatientHistoryWithFiltersAsync(
					clinicId, patientId, start, end, categoryFilters);

				if (history == null)
				{
					_logger.LogInformation("No history found for Clinic ID {ClinicId}, Patient ID {PatientId}", clinicId, patientId);
					return NotFound(ApiResponseFactory.Fail("No history found for this patient."));
				}

				await transaction.CommitAsync();
				committed = true;

				var userProfile = await _userRepository.GetUserByHFIDAsync(history.HfId);

				var response = new
				{
					history.PatientName,
					history.HfId,
					userProfile?.ProfilePhoto,
					history.Email,

					TotalVisits = history.Visits.Count,
					DateRange = new
					{
						StartDate = start?.ToString("dd-MM-yyyy"),
						EndDate = end?.ToString("dd-MM-yyyy")
					},
					AppliedFilters = new
					{
						Categories = categoryFilters.Any() ? categoryFilters : new List<string> { "all" }
					},
					history.Visits
				};

				_logger.LogInformation("Fetched filtered history for Clinic ID {ClinicId}, Patient ID {PatientId}. Visits: {VisitCount}, Filters: {Filters}",
					clinicId, patientId, history.Visits.Count, string.Join(",", categoryFilters));

				return Ok(ApiResponseFactory.Success(response, "Patient history fetched successfully."));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error fetching patient history for Clinic ID {ClinicId}", clinicId);
				return StatusCode(500, ApiResponseFactory.Fail("An error occurred while fetching patient history."));
			}
			finally
			{
				if (!committed && transaction.GetDbTransaction().Connection != null)
					await transaction.RollbackAsync();
			}
		}





		// GET: Preview next available unique IDs for all record types
		[HttpGet("clinic/{clinicId}/patient/{patientId}/visit/{clinicVisitId}/next-ids")]
		[Authorize]
		public async Task<IActionResult> GetNextAvailableIds(
			[FromRoute] int clinicId,
			[FromRoute] int patientId,
			[FromRoute] int clinicVisitId)
		{
			HttpContext.Items["Log-Category"] = "Next Available IDs Preview";

			if (clinicId <= 0 || patientId <= 0 || clinicVisitId <= 0)
			{
				_logger.LogWarning("Invalid IDs. ClinicId: {ClinicId}, PatientId: {PatientId}, VisitId: {VisitId}",
					clinicId, patientId, clinicVisitId);
				return BadRequest(ApiResponseFactory.Fail("Clinic ID, Patient ID, and Visit ID must be positive integers."));
			}

			bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
			if (!isAuthorized)
			{
				_logger.LogWarning("Unauthorized next IDs preview attempt for Clinic ID {ClinicId}", clinicId);
				return Unauthorized(ApiResponseFactory.Fail("You are not authorized to view IDs for this clinic."));
			}

			try
			{
				// Check which record types already exist for this visit
				var existingRecords = await _clinicPatientRecordRepository
					.GetByClinicPatientVisitAsync(clinicId, patientId, clinicVisitId);

				var existingTypes = existingRecords.Select(r => r.Type).ToHashSet();

				var response = new NextAvailableIdsResponse();

				// Only show next ID for record types that don't exist yet
				if (!existingTypes.Contains(RecordType.Treatment))
				{
					response.TreatmentId = await _uniqueIdGenerator
						.GetNextAvailableIdAsync(clinicId, RecordType.Treatment);
				}

				if (!existingTypes.Contains(RecordType.Prescription))
				{
					response.PrescriptionId = await _uniqueIdGenerator
						.GetNextAvailableIdAsync(clinicId, RecordType.Prescription);
				}

				if (!existingTypes.Contains(RecordType.Invoice))
				{
					response.InvoiceId = await _uniqueIdGenerator
						.GetNextAvailableIdAsync(clinicId, RecordType.Invoice);
				}

				if (!existingTypes.Contains(RecordType.Receipt))
				{
					response.ReceiptId = await _uniqueIdGenerator
						.GetNextAvailableIdAsync(clinicId, RecordType.Receipt);
				}

				_logger.LogInformation(
					"Retrieved next available IDs for Clinic ID {ClinicId}, Patient ID {PatientId}, Visit ID {VisitId}",
					clinicId, patientId, clinicVisitId);

				return Ok(ApiResponseFactory.Success(response,
					"Next available IDs retrieved successfully."));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex,
					"Error retrieving next IDs for Clinic ID {ClinicId}, Visit ID {VisitId}",
					clinicId, clinicVisitId);
				return StatusCode(500, ApiResponseFactory.Fail(
					"An error occurred while retrieving next available IDs."));
			}
		}


		// ****************************************************************************************************************************


		//        // Generate PDFs for prescriptions
		//        [HttpPost("generate-bulk-pdfs")]
		//        //[Authorize]
		//        public async Task<IActionResult> GenerateBulkPrescriptionPdfs([FromBody] BulkPdfGenerationRequest request)
		//        {
		//            HttpContext.Items["Log-Category"] = "Bulk Prescription PDF Generation";

		//            if (!ModelState.IsValid || !request.PrescriptionIds.Any())
		//            {
		//                _logger.LogWarning("Invalid request. PrescriptionIds are required.");
		//                return BadRequest(ApiResponseFactory.Fail("PrescriptionIds are required and must not be empty."));
		//            }

		//            // Authorization check for clinic
		//            //bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(request.ClinicId, User);
		//            //if (!isAuthorized)
		//            //{
		//            //    _logger.LogWarning("Unauthorized bulk PDF generation attempt for Clinic ID {ClinicId}", request.ClinicId);
		//            //    return Unauthorized(ApiResponseFactory.Fail("You are not authorized to generate prescriptions for this clinic."));
		//            //}

		//            var response = new BulkPdfGenerationResponse();
		//            var transaction = await _clinicRepository.BeginTransactionAsync();
		//            var committed = false;

		//            try
		//            {
		//                // Download Chromium once for all PDFs
		//                await new BrowserFetcher().DownloadAsync();

		//                using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
		//                {
		//                    Headless = true,
		//                    Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
		//                });

		//                foreach (int prescriptionId in request.PrescriptionIds)
		//                {
		//                    try
		//                    {
		//                        response.TotalProcessed++;

		//                        // Get prescription record by ID
		//                        var prescriptionRecord = await GetPrescriptionRecordById(prescriptionId, request.ClinicId);
		//                        if (prescriptionRecord == null)
		//                        {
		//                            response.Failed++;
		//                            response.FailedRecords.Add(new FailedRecord
		//                            {
		//                                PrescriptionId = prescriptionId,
		//                                Reason = "Prescription record not found"
		//                            });
		//                            continue;
		//                        }

		//                        // Get related data
		//                        var visit = await _clinicVisitRepository.GetByIdAsync(prescriptionRecord.ClinicVisitId);
		//                        var clinicPatient = await _clinicPatientRecordRepository.GetByIdAsync(prescriptionRecord.PatientId);
		//                        var user = clinicPatient != null ? await _userRepository.GetUserByHFIDAsync(clinicPatient.HFID) : null;

		//                        if (visit == null || clinicPatient == null || user == null)
		//                        {
		//                            response.Failed++;
		//                            response.FailedRecords.Add(new FailedRecord
		//                            {
		//                                PrescriptionId = prescriptionId,
		//                                Reason = "Missing related data (visit, patient, or user)"
		//                            });
		//                            continue;
		//                        }

		//                        // Parse prescription JSON
		//                        var prescriptionData = JsonConvert.DeserializeObject<PrescriptionJsonPayload>(prescriptionRecord.JsonData);
		//                        if (prescriptionData?.Patient == null || prescriptionData.Medications == null)
		//                        {
		//                            response.Failed++;
		//                            response.FailedRecords.Add(new FailedRecord
		//                            {
		//                                PrescriptionId = prescriptionId,
		//                                Reason = "Invalid prescription JSON data"
		//                            });
		//                            continue;
		//                        }

		//                        // Generate PDF for this prescription
		//                        var success = await ProcessSinglePrescription(
		//                            prescriptionRecord, visit, clinicPatient, user, prescriptionData, browser, response);

		//                        if (success)
		//                        {
		//                            response.Successful++;
		//                        }
		//                        else
		//                        {
		//                            response.Failed++;
		//                            response.FailedRecords.Add(new FailedRecord
		//                            {
		//                                PrescriptionId = prescriptionId,
		//                                Reason = "PDF generation or upload failed"
		//                            });
		//                        }
		//                    }
		//                    catch (Exception ex)
		//                    {
		//                        _logger.LogError(ex, "Error processing prescription ID {PrescriptionId}", prescriptionId);
		//                        response.Failed++;
		//                        response.FailedRecords.Add(new FailedRecord
		//                        {
		//                            PrescriptionId = prescriptionId,
		//                            Reason = $"Processing error: {ex.Message}"
		//                        });
		//                    }
		//                }

		//                await transaction.CommitAsync();
		//                committed = true;

		//                response.Message = $"Bulk PDF generation completed: {response.Successful} successful, " +
		//                                  $"{response.Failed} failed out of {response.TotalProcessed} total prescriptions.";

		//                _logger.LogInformation("Bulk prescription PDF generation completed. Success: {Success}, Failed: {Failed}",
		//                    response.Successful, response.Failed);

		//                return Ok(ApiResponseFactory.Success(response, response.Message));
		//            }
		//            catch (Exception ex)
		//            {
		//                _logger.LogError(ex, "Error during bulk PDF generation for Clinic ID {ClinicId}", request.ClinicId);
		//                return StatusCode(500, ApiResponseFactory.Fail("An error occurred during bulk PDF generation."));
		//            }
		//            finally
		//            {
		//                if (!committed && transaction.GetDbTransaction().Connection != null)
		//                    await transaction.RollbackAsync();
		//            }
		//        }







		//        [HttpPost("generate-all-unsent")]
		//        //[Authorize]
		//        public async Task<IActionResult> GenerateAllUnsentPrescriptionPdfs([FromBody] GenerateUnsentRequest request)
		//        {
		//            HttpContext.Items["Log-Category"] = "Generate All Unsent Prescription PDFs";

		//            //bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(request.ClinicId, User);
		//            //if (!isAuthorized)
		//            //{
		//            //    _logger.LogWarning("Unauthorized unsent PDF generation attempt for Clinic ID {ClinicId}", request.ClinicId);
		//            //    return Unauthorized(ApiResponseFactory.Fail("You are not authorized to generate prescriptions for this clinic."));
		//            //}

		//            try
		//            {
		//                // Get all unsent prescription records for the clinic
		//                var unsentPrescriptions = await GetUnsentPrescriptionRecords(request.ClinicId);

		//                if (!unsentPrescriptions.Any())
		//                {
		//                    _logger.LogInformation("No unsent prescriptions found for Clinic ID {ClinicId}", request.ClinicId);
		//                    return Ok(ApiResponseFactory.Success(new BulkPdfGenerationResponse
		//                    {
		//                        TotalProcessed = 0,
		//                        Successful = 0,
		//                        Failed = 0,
		//                        Message = "No unsent prescriptions found."
		//                    }, "No unsent prescriptions found."));
		//                }

		//                var bulkRequest = new BulkPdfGenerationRequest
		//                {
		//                    ClinicId = request.ClinicId,
		//                    PrescriptionIds = unsentPrescriptions.Select(p => p.Id).ToList()
		//                };

		//                // Reuse the bulk generation logic
		//                return await GenerateBulkPrescriptionPdfs(bulkRequest);
		//            }
		//            catch (Exception ex)
		//            {
		//                _logger.LogError(ex, "Error generating all unsent prescriptions for Clinic ID {ClinicId}", request.ClinicId);
		//                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while generating unsent prescriptions."));
		//            }
		//        }

		//        private async Task<ClinicPatientRecord?> GetPrescriptionRecordById(int prescriptionId, int clinicId)
		//        {
		//            // This method would need to be added to your repository
		//            // For now, we'll get all prescription records and filter
		//            var allRecords = await _clinicPatientRecordRepository.GetPrescriptionRecordsByClinicIdAsync(clinicId);
		//            return allRecords.FirstOrDefault(r => r.Id == prescriptionId && r.Type == RecordType.Prescription);
		//        }

		//        private async Task<List<ClinicPatientRecord>> GetUnsentPrescriptionRecords(int clinicId)
		//        {
		//            // This method would need to be added to your repository
		//            // For now, we'll get all prescription records and filter
		//            var allRecords = await _clinicPatientRecordRepository.GetPrescriptionRecordsByClinicIdAsync(clinicId);
		//            return allRecords.Where(r => r.Type == RecordType.Prescription && !r.SendToPatient).ToList();
		//        }

		//        private async Task<bool> ProcessSinglePrescription(
		//            ClinicPatientRecord prescriptionRecord,
		//            ClinicVisit visit,
		//            ClinicPatient clinicPatient,
		//            Domain.Entities.Users.User user,
		//            PrescriptionJsonPayload prescriptionData,
		//            IBrowser browser,
		//            BulkPdfGenerationResponse response)
		//        {
		//            try
		//            {
		//                // Generate HTML content
		//                var htmlContent = GeneratePrescriptionHtml(prescriptionData, visit.AppointmentDate, visit.AppointmentTime);

		//                // Generate PDF
		//                using var page = await browser.NewPageAsync();
		//                await page.SetContentAsync(htmlContent);

		//                var pdfOptions = new PdfOptions
		//                {
		//                    Format = PaperFormat.A4,
		//                    PrintBackground = true,
		//                    MarginOptions = new MarginOptions
		//                    {
		//                        Top = "0.5in",
		//                        Bottom = "0.5in",
		//                        Left = "0.5in",
		//                        Right = "0.5in"
		//                    }
		//                };

		//                var pdfBytes = await page.PdfDataAsync(pdfOptions);
		//                if (pdfBytes == null || pdfBytes.Length == 0)
		//                {
		//                    return false;
		//                }

		//                // Save PDF to temporary file
		//                var tempFileName = $"prescription_{prescriptionRecord.ClinicId}_{prescriptionRecord.PatientId}_{prescriptionRecord.ClinicVisitId}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
		//                var tempFilePath = Path.Combine(Path.GetTempPath(), tempFileName);
		//                await System.IO.File.WriteAllBytesAsync(tempFilePath, pdfBytes);

		//                // Upload to S3
		//                var s3Key = $"clinic/{tempFileName}";
		//                var s3Url = await _s3StorageService.UploadFileToS3(tempFilePath, s3Key);

		//                if (string.IsNullOrEmpty(s3Url))
		//                {
		//                    System.IO.File.Delete(tempFilePath);
		//                    return false;
		//                }

		//                // Update prescription record
		//                prescriptionRecord.SendToPatient = true;
		//                await _clinicPatientRecordRepository.UpdateAsync(prescriptionRecord);

		//                // Create user report entry
		//                var reportName = $"Arthrose_Prescription_{prescriptionData.Patient.Name.Replace(" ", "_")}_{visit.AppointmentDate:dd-MM-yy}";
		//                var epochTime = new DateTimeOffset(visit.AppointmentDate.Date + visit.AppointmentTime).ToUnixTimeSeconds();
		//                var fileSizeKb = Math.Round((decimal)pdfBytes.Length / 1024, 2);

		//                var userReport = new UserReport
		//                {
		//                    UserId = user.Id,
		//                    ReportName = reportName,
		//                    ReportCategory = 4, // Prescription category
		//                    ReportUrl = s3Url,
		//                    EpochTime = epochTime,
		//                    FileSize = fileSizeKb,
		//                    UploadedBy = "Clinic",
		//                    ClinicId = 8, // Fixed lab ID
		//                    UserType = "Independent",
		//                    DeletedBy = 0
		//                };

		//                await _userRepository.SaveAsync(userReport);

		//                // Clean up temporary file
		//                System.IO.File.Delete(tempFilePath);

		//                // Add to successful records
		//                response.SuccessfulRecords.Add(new SuccessfulRecord
		//                {
		//                    PrescriptionId = prescriptionRecord.Id,
		//                    PatientName = prescriptionData.Patient.Name,
		//                    HFID = clinicPatient.HFID,
		//                    AppointmentDate = visit.AppointmentDate.ToString("dd-MM-yyyy"),
		//                    PrescriptionUrl = s3Url,
		//                    FileSizeKB = fileSizeKb
		//                });

		//                return true;
		//            }
		//            catch (Exception ex)
		//            {
		//                _logger.LogError(ex, "Error processing single prescription ID {PrescriptionId}", prescriptionRecord.Id);
		//                return false;
		//            }
		//        }

		//        private string GeneratePrescriptionHtml(PrescriptionJsonPayload data, DateTime appointmentDate, TimeSpan appointmentTime)
		//        {
		//            var medicationsTableRows = new StringBuilder();

		//            for (int i = 0; i < data.Medications.Count; i++)
		//            {
		//                var med = data.Medications[i];
		//                medicationsTableRows.AppendLine($@"
		//                            <tr>
		//                                <td>{i + 1}</td>
		//                                <td>
		//                                    {med.Name}
		//                                    <div class=""prescription-instruction"">
		//                                        Instruction: {med.Instruction}
		//                                    </div>
		//                                </td>
		//                                <td>{med.Dosage}</td>
		//                                <td>{med.Frequency}</td>
		//                                <td>{med.Timing}</td>
		//                            </tr>");
		//            }

		//            var formattedDob = !string.IsNullOrEmpty(data.Patient.Dob) ? data.Patient.Dob : "Not Provided";

		//            return $@"
		//        <!DOCTYPE html>
		//        <html lang=""en"">
		//          <head>
		//            <meta charset=""UTF-8"" />
		//            <style>
		//              body {{
		//                font-family: Arial, Helvetica, sans-serif;
		//                margin: 40px;
		//                color: #333;
		//                background-color: #fff;
		//              }}
		//              .header {{
		//                text-align: center;
		//                margin-bottom: 20px;
		//              }}
		//          .header img{{
		//        max-height: 130px;
		//                width: auto;
		//              }}
		//              .patient-info {{
		//                border: 1px solid #e5e7eb;
		//                border-radius: 8px;
		//                padding: 16px;
		//                margin-bottom: 24px;
		//                font-size: 13px;
		//                background-color: #fafafa;
		//              }}
		//              .patient-info table {{
		//                width: 100%;
		//                border-collapse: collapse;
		//              }}
		//              .patient-info td {{
		//                padding: 4px 8px;
		//                vertical-align: top;
		//              }}
		//              .section-title {{
		//                font-weight: bold;
		//                margin-bottom: 8px;
		//                font-size: 14px;
		//                color: #333;
		//                margin-top: 24px;
		//              }}
		//              table.prescription {{
		//                width: 100%;
		//                border-collapse: collapse;
		//                margin-top: 8px;
		//                font-size: 13px;
		//                border: 1px solid #000;
		//              }}
		//              table.prescription th,
		//              table.prescription td {{
		//                border: 1px solid #000;
		//                padding: 10px;
		//                text-align: left;
		//              }}
		//              table.prescription th {{
		//                background: #f9f9f9;
		//                font-weight: 600;
		//                color: #333;
		//              }}
		//              .prescription-instruction {{
		//                font-style: italic;
		//                color: #666;
		//                font-size: 11px;
		//              }}
		//              .additional-notes {{
		//                margin-top: 15px;
		//                padding: 10px;
		//                background-color: #f8f9fa;
		//                border-left: 4px solid #007bff;
		//                font-size: 12px;
		//              }}
		//              .additional-notes strong {{
		//                color: #333;
		//              }}
		//              .signature {{
		//                margin-top: 40px;
		//                text-align: right;
		//                font-size: 14px;
		//                color: #333;
		//              }}
		//              .signature .line {{
		//                margin-bottom: 2px;
		//                border-top: 1px solid #333;
		//                width: 116px;
		//                margin-left: auto;
		//              }}
		//              .signature p {{
		//                font-family: ""Cedarville Cursive"", cursive;
		//                font-size: 15px;
		//                color: #1a3c6e;
		//                margin: 0;
		//                padding-right: 0px;
		//              }}
		//              footer {{
		//                margin-top: 40px;
		//                font-size: 15px;
		//                display: flex;
		//                justify-content: space-between;
		//                color: #000;
		//              }}
		//              .logo-placeholder {{
		//                display: inline-block;
		//                width: 200px;
		//                height: 80px;
		//                background: linear-gradient(135deg, #e3f2fd 0%, #bbdefb 100%);
		//                border-radius: 4px;
		//                position: relative;
		//                margin: 0 auto;
		//              }}
		//              .logo-placeholder::before {{
		//                content: ""ARTHROSE CRANIOFACIAL PAIN & TMJ CENTRE"";
		//                position: absolute;
		//                top: 50%;
		//                left: 50%;
		//                transform: translate(-50%, -50%);
		//                font-size: 15px;
		//                font-weight: bold;
		//                color: #1976d2;
		//                text-align: center;
		//              }}
		//            </style>
		//          </head>
		//          <body>
		//            <!-- Header with Logo -->
		//            <div class=""header"">
		//              <img src=""https://d7cop3y0lcg80.cloudfront.net/reports/1/0604d10d087f97b877ea0ae85e9494b5df28b6e7_26-09-2025_10-12-19.png"" alt=""ARTHROSE CRANIOFACIAL PAIN & TMJ CENTRE Logo"" />
		//            </div>

		//            <!-- Patient Info -->
		//            <div class=""patient-info"">
		//              <table>
		//                <tr>
		//                  <td><strong>Patient Name:</strong> {data.Patient.Name}</td>
		//                  <td><strong>HFID:</strong> {data.Patient.Hfid}</td>
		//                </tr>
		//                <tr>
		//                  <td><strong>Gender:</strong> {data.Patient.Gender}</td>
		//                  <td><strong>PRFID:</strong> {data.Patient.Prfid}</td>
		//                </tr>
		//                <tr>
		//                  <td><strong>DOB:</strong> {formattedDob}</td>
		//                  <td><strong>Mobile:</strong> {data.Patient.Mobile}</td>
		//                </tr>
		//                <tr>
		//                  <td><strong>Consultant Coach:</strong> {data.Patient.Doctor}</td>
		//                  <td><strong>City:</strong> {data.Patient.City}</td>
		//                </tr>
		//              </table>
		//            </div>

		//            <!-- Prescription Section -->
		//            <div class=""section-title"">Prescription</div>
		//            <table class=""prescription"">
		//              <thead>
		//                <tr>
		//                  <th>S.No.</th>
		//                  <th>Medication</th>
		//                  <th>Dosage</th>
		//                  <th>Duration</th>
		//                  <th>Timing</th>
		//                </tr>
		//              </thead>
		//              <tbody>
		//                {medicationsTableRows}
		//              </tbody>
		//            </table>

		//            <!-- Additional Notes -->
		//            <div class=""additional-notes"">
		//              <strong>Additional Notes:</strong><br />
		//              {data.AdditionalNotes}
		//            </div>

		//            <!-- Signature -->
		//            <div class=""signature"">
		//              <p>{data.Patient.Doctor}</p>
		//              <div class=""line"">{data.Patient.Doctor}</div>
		//            </div>

		//            <footer>
		//              <span>www.arthrosetmjindia.com</span>
		//              <span>www.hfiles.in</span>
		//            </footer>
		//          </body>
		//        </html>";
		//        }


		//        // Historical Prescription PDF Generation APIs (2020-2024)
		//        // Add these methods to your existing ClinicPrescriptionController

		//        [HttpPost("generate-bulk-historical-pdfs")]
		//        //[Authorize]
		//        public async Task<IActionResult> GenerateBulkHistoricalPrescriptionPdfs([FromBody] BulkHistoricalPdfGenerationRequest request)
		//        {
		//            HttpContext.Items["Log-Category"] = "Bulk Historical Prescription PDF Generation";

		//            if (!ModelState.IsValid || !request.PrescriptionRecordIds.Any())
		//            {
		//                _logger.LogWarning("Invalid request. PrescriptionRecordIds are required.");
		//                return BadRequest(ApiResponseFactory.Fail("PrescriptionRecordIds are required and must not be empty."));
		//            }

		//            // Authorization check (commented out for now)
		//            //bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(request.ClinicId, User);
		//            //if (!isAuthorized)
		//            //{
		//            //    _logger.LogWarning("Unauthorized bulk PDF generation attempt for Clinic ID {ClinicId}", request.ClinicId);
		//            //    return Unauthorized(ApiResponseFactory.Fail("You are not authorized to generate prescriptions for this clinic."));
		//            //}

		//            var response = new BulkHistoricalPdfGenerationResponse();
		//            var transaction = await _clinicRepository.BeginTransactionAsync();
		//            var committed = false;

		//            try
		//            {
		//                // Download Chromium once for all PDFs
		//                await new BrowserFetcher().DownloadAsync();

		//                using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
		//                {
		//                    Headless = true,
		//                    Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
		//                });

		//                foreach (int recordId in request.PrescriptionRecordIds)
		//                {
		//                    try
		//                    {
		//                        response.TotalProcessed++;

		//                        // Get prescription record by ID
		//                        var prescriptionRecord = await GetHistoricalPrescriptionRecordById(recordId, request.ClinicId);
		//                        if (prescriptionRecord == null)
		//                        {
		//                            response.Failed++;
		//                            response.FailedRecords.Add(new FailedHistoricalPdfRecord
		//                            {
		//                                PrescriptionRecordId = recordId,
		//                                Reason = "Prescription record not found"
		//                            });
		//                            continue;
		//                        }

		//                        // Get related data
		//                        var visit = await _clinicVisitRepository.GetByIdAsync(prescriptionRecord.ClinicVisitId);
		//                        var clinicPatient = await _clinicPatientRecordRepository.GetByIdAsync(prescriptionRecord.PatientId);

		//                        if (visit == null || clinicPatient == null)
		//                        {
		//                            response.Failed++;
		//                            response.FailedRecords.Add(new FailedHistoricalPdfRecord
		//                            {
		//                                PrescriptionRecordId = recordId,
		//                                Reason = "Missing related data (visit or patient)"
		//                            });
		//                            continue;
		//                        }

		//                        // Parse prescription JSON
		//                        var prescriptionData = JsonConvert.DeserializeObject<HistoricalPrescriptionJsonPayload>(prescriptionRecord.JsonData);
		//                        if (prescriptionData?.Patient == null || prescriptionData.Medications == null)
		//                        {
		//                            response.Failed++;
		//                            response.FailedRecords.Add(new FailedHistoricalPdfRecord
		//                            {
		//                                PrescriptionRecordId = recordId,
		//                                PatientId = clinicPatient.HFID ?? "",
		//                                Reason = "Invalid prescription JSON data"
		//                            });
		//                            continue;
		//                        }

		//                        // Try to get User record (may not exist for historical data)
		//                        var user = await _userRepository.GetUserByHFIDAsync(clinicPatient.HFID);

		//                        // Generate PDF for this prescription
		//                        var success = await ProcessSingleHistoricalPrescription(
		//                            prescriptionRecord, visit, clinicPatient, user, prescriptionData, browser, response);

		//                        if (success)
		//                        {
		//                            response.Successful++;
		//                        }
		//                        else
		//                        {
		//                            response.Failed++;
		//                            response.FailedRecords.Add(new FailedHistoricalPdfRecord
		//                            {
		//                                PrescriptionRecordId = recordId,
		//                                PatientId = prescriptionData.Patient.Prfid,
		//                                Reason = "PDF generation or upload failed"
		//                            });
		//                        }
		//                    }
		//                    catch (Exception ex)
		//                    {
		//                        _logger.LogError(ex, "Error processing historical prescription record ID {RecordId}", recordId);
		//                        response.Failed++;
		//                        response.FailedRecords.Add(new FailedHistoricalPdfRecord
		//                        {
		//                            PrescriptionRecordId = recordId,
		//                            Reason = $"Processing error: {ex.Message}"
		//                        });
		//                    }
		//                }

		//                await transaction.CommitAsync();
		//                committed = true;

		//                response.Message = $"Bulk historical PDF generation completed: {response.Successful} successful, " +
		//                                  $"{response.Failed} failed, {response.SkippedNoUser} skipped (no user) " +
		//                                  $"out of {response.TotalProcessed} total prescriptions.";

		//                _logger.LogInformation("Bulk historical prescription PDF generation completed. Success: {Success}, Failed: {Failed}, NoUser: {NoUser}",
		//                    response.Successful, response.Failed, response.SkippedNoUser);

		//                return Ok(ApiResponseFactory.Success(response, response.Message));
		//            }
		//            catch (Exception ex)
		//            {
		//                _logger.LogError(ex, "Error during bulk historical PDF generation for Clinic ID {ClinicId}", request.ClinicId);
		//                return StatusCode(500, ApiResponseFactory.Fail("An error occurred during bulk historical PDF generation."));
		//            }
		//            finally
		//            {
		//                if (!committed && transaction.GetDbTransaction().Connection != null)
		//                    await transaction.RollbackAsync();
		//            }
		//        }

		//        [HttpPost("generate-all-historical-unsent")]
		//        //[Authorize]
		//        public async Task<IActionResult> GenerateAllHistoricalUnsentPrescriptionPdfs([FromBody] GenerateAllHistoricalUnsentRequest request)
		//        {
		//            HttpContext.Items["Log-Category"] = "Generate All Unsent Historical Prescription PDFs";

		//            //bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(request.ClinicId, User);
		//            //if (!isAuthorized)
		//            //{
		//            //    _logger.LogWarning("Unauthorized unsent historical PDF generation attempt for Clinic ID {ClinicId}", request.ClinicId);
		//            //    return Unauthorized(ApiResponseFactory.Fail("You are not authorized to generate prescriptions for this clinic."));
		//            //}

		//            try
		//            {
		//                // Get all unsent historical prescription records for the clinic
		//                var unsentHistoricalPrescriptions = await GetUnsentHistoricalPrescriptionRecords(request.ClinicId);

		//                if (!unsentHistoricalPrescriptions.Any())
		//                {
		//                    _logger.LogInformation("No unsent historical prescriptions found for Clinic ID {ClinicId}", request.ClinicId);
		//                    return Ok(ApiResponseFactory.Success(new BulkHistoricalPdfGenerationResponse
		//                    {
		//                        TotalProcessed = 0,
		//                        Successful = 0,
		//                        Failed = 0,
		//                        Message = "No unsent historical prescriptions found."
		//                    }, "No unsent historical prescriptions found."));
		//                }

		//                var bulkRequest = new BulkHistoricalPdfGenerationRequest
		//                {
		//                    ClinicId = request.ClinicId,
		//                    PrescriptionRecordIds = unsentHistoricalPrescriptions.Select(p => p.Id).ToList()
		//                };

		//                // Reuse the bulk generation logic
		//                return await GenerateBulkHistoricalPrescriptionPdfs(bulkRequest);
		//            }
		//            catch (Exception ex)
		//            {
		//                _logger.LogError(ex, "Error generating all unsent historical prescriptions for Clinic ID {ClinicId}", request.ClinicId);
		//                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while generating unsent historical prescriptions."));
		//            }
		//        }

		//        private async Task<ClinicPatientRecord?> GetHistoricalPrescriptionRecordById(int recordId, int clinicId)
		//        {
		//            var allRecords = await _clinicPatientRecordRepository.GetPrescriptionRecordsByClinicIdAsync(clinicId);
		//            return allRecords.FirstOrDefault(r =>
		//                r.Id == recordId &&
		//                r.Type == RecordType.Prescription &&
		//                r.UniqueRecordId == null); // Historical records have NULL UniqueRecordId
		//        }

		//        private async Task<List<ClinicPatientRecord>> GetUnsentHistoricalPrescriptionRecords(int clinicId)
		//        {
		//            var allRecords = await _clinicPatientRecordRepository.GetPrescriptionRecordsByClinicIdAsync(clinicId);
		//            return allRecords.Where(r =>
		//                r.Type == RecordType.Prescription &&
		//                !r.SendToPatient &&
		//                r.UniqueRecordId == null // Historical records have NULL UniqueRecordId
		//            ).ToList();
		//        }

		//        private async Task<bool> ProcessSingleHistoricalPrescription(
		//            ClinicPatientRecord prescriptionRecord,
		//            ClinicVisit visit,
		//            ClinicPatient clinicPatient,
		//            Domain.Entities.Users.User? user, // User may be null for historical data
		//            HistoricalPrescriptionJsonPayload prescriptionData,
		//            IBrowser browser,
		//            BulkHistoricalPdfGenerationResponse response)
		//        {
		//            try
		//            {
		//                // Generate HTML content
		//                var htmlContent = GenerateHistoricalPrescriptionHtml(prescriptionData, visit.AppointmentDate, visit.AppointmentTime);

		//                // Generate PDF
		//                using var page = await browser.NewPageAsync();
		//                await page.SetContentAsync(htmlContent);

		//                var pdfOptions = new PdfOptions
		//                {
		//                    Format = PaperFormat.A4,
		//                    PrintBackground = true,
		//                    MarginOptions = new MarginOptions
		//                    {
		//                        Top = "0.5in",
		//                        Bottom = "0.5in",
		//                        Left = "0.5in",
		//                        Right = "0.5in"
		//                    }
		//                };

		//                var pdfBytes = await page.PdfDataAsync(pdfOptions);
		//                if (pdfBytes == null || pdfBytes.Length == 0)
		//                {
		//                    return false;
		//                }

		//                // Save PDF to temporary file
		//                var tempFileName = $"historical_prescription_{prescriptionRecord.ClinicId}_{prescriptionRecord.PatientId}_{prescriptionRecord.ClinicVisitId}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
		//                var tempFilePath = Path.Combine(Path.GetTempPath(), tempFileName);
		//                await System.IO.File.WriteAllBytesAsync(tempFilePath, pdfBytes);

		//                // Upload to S3
		//                var s3Key = $"clinic/historical/{tempFileName}";
		//                var s3Url = await _s3StorageService.UploadFileToS3(tempFilePath, s3Key);

		//                if (string.IsNullOrEmpty(s3Url))
		//                {
		//                    System.IO.File.Delete(tempFilePath);
		//                    return false;
		//                }

		//                // Update prescription record
		//                prescriptionRecord.SendToPatient = true;
		//                await _clinicPatientRecordRepository.UpdateAsync(prescriptionRecord);

		//                // Create user report entry ONLY if user exists
		//                if (user != null)
		//                {
		//                    var reportName = $"Arthrose_Historical_Prescription_{prescriptionData.Patient.Name.Replace(" ", "_")}_{visit.AppointmentDate:dd-MM-yy}";
		//                    var epochTime = new DateTimeOffset(visit.AppointmentDate.Date + visit.AppointmentTime).ToUnixTimeSeconds();
		//                    var fileSizeKb = Math.Round((decimal)pdfBytes.Length / 1024, 2);

		//                    var userReport = new UserReport
		//                    {
		//                        UserId = user.Id,
		//                        ReportName = reportName,
		//                        ReportCategory = 4, // Prescription category
		//                        ReportUrl = s3Url,
		//                        EpochTime = epochTime,
		//                        FileSize = fileSizeKb,
		//                        UploadedBy = "Clinic - Historical Import",
		//                        ClinicId = 8, // Fixed lab ID
		//                        UserType = "Independent",
		//                        DeletedBy = 0
		//                    };

		//                    await _userRepository.SaveAsync(userReport);
		//                }
		//                else
		//                {
		//                    // No user found - just log it
		//                    _logger.LogInformation("No user found for historical prescription. PatientId: {PatientId}, HFID: {HFID}. PDF generated but UserReport not created.",
		//                        prescriptionData.Patient.Prfid, clinicPatient.HFID);
		//                    response.SkippedNoUser++;
		//                }

		//                // Clean up temporary file
		//                System.IO.File.Delete(tempFilePath);

		//                // Add to successful records
		//                response.SuccessfulRecords.Add(new SuccessfulHistoricalPdfRecord
		//                {
		//                    PrescriptionRecordId = prescriptionRecord.Id,
		//                    PatientId = prescriptionData.Patient.Prfid,
		//                    PatientName = prescriptionData.Patient.Name,
		//                    HFID = clinicPatient.HFID ?? "",
		//                    AppointmentDate = visit.AppointmentDate.ToString("dd-MM-yyyy"),
		//                    PrescriptionUrl = s3Url,
		//                    FileSizeKB = Math.Round((decimal)pdfBytes.Length / 1024, 2),
		//                    IsHistorical = true
		//                });

		//                return true;
		//            }
		//            catch (Exception ex)
		//            {
		//                _logger.LogError(ex, "Error processing single historical prescription record ID {RecordId}", prescriptionRecord.Id);
		//                return false;
		//            }
		//        }

		//        private string GenerateHistoricalPrescriptionHtml(HistoricalPrescriptionJsonPayload data, DateTime appointmentDate, TimeSpan appointmentTime)
		//        {
		//            var medicationsTableRows = new StringBuilder();

		//            for (int i = 0; i < data.Medications.Count; i++)
		//            {
		//                var med = data.Medications[i];
		//                medicationsTableRows.AppendLine($@"
		//                    <tr>
		//                        <td>{i + 1}</td>
		//                        <td>
		//                            {med.Name}
		//                            <div class=""prescription-instruction"">
		//                                Instruction: {med.Instruction}
		//                            </div>
		//                        </td>
		//                        <td>{med.Dosage}</td>
		//                        <td>{med.Frequency}</td>
		//                        <td>{med.Timing}</td>
		//                    </tr>");
		//            }

		//            var formattedDob = !string.IsNullOrEmpty(data.Patient.Dob) ? data.Patient.Dob : "";

		//            return $@"
		//<!DOCTYPE html>
		//<html lang=""en"">
		//  <head>
		//    <meta charset=""UTF-8"" />
		//    <style>
		//      body {{
		//        font-family: Arial, Helvetica, sans-serif;
		//        margin: 40px;
		//        color: #333;
		//        background-color: #fff;
		//      }}
		//      .header {{
		//        text-align: center;
		//        margin-bottom: 20px;
		//      }}
		//  .header img{{
		//max-height: 130px;
		//        width: auto;
		//      }}
		//      .patient-info {{
		//        border: 1px solid #e5e7eb;
		//        border-radius: 8px;
		//        padding: 16px;
		//        margin-bottom: 24px;
		//        font-size: 13px;
		//        background-color: #fafafa;
		//      }}
		//      .patient-info table {{
		//        width: 100%;
		//        border-collapse: collapse;
		//      }}
		//      .patient-info td {{
		//        padding: 4px 8px;
		//        vertical-align: top;
		//      }}
		//      .section-title {{
		//        font-weight: bold;
		//        margin-bottom: 8px;
		//        font-size: 14px;
		//        color: #333;
		//        margin-top: 24px;
		//      }}
		//      table.prescription {{
		//        width: 100%;
		//        border-collapse: collapse;
		//        margin-top: 8px;
		//        font-size: 13px;
		//        border: 1px solid #000;
		//      }}
		//      table.prescription th,
		//      table.prescription td {{
		//        border: 1px solid #000;
		//        padding: 10px;
		//        text-align: left;
		//      }}
		//      table.prescription th {{
		//        background: #f9f9f9;
		//        font-weight: 600;
		//        color: #333;
		//      }}
		//      .prescription-instruction {{
		//        font-style: italic;
		//        color: #666;
		//        font-size: 11px;
		//      }}
		//      .additional-notes {{
		//        margin-top: 15px;
		//        padding: 10px;
		//        background-color: #f8f9fa;
		//        border-left: 4px solid #007bff;
		//        font-size: 12px;
		//      }}
		//      .additional-notes strong {{
		//        color: #333;
		//      }}
		//      .signature {{
		//        margin-top: 40px;
		//        text-align: right;
		//        font-size: 14px;
		//        color: #333;
		//      }}
		//      .signature .line {{
		//        margin-bottom: 2px;
		//        border-top: 1px solid #333;
		//        width: 116px;
		//        margin-left: auto;
		//      }}
		//      .signature p {{
		//        font-family: ""Cedarville Cursive"", cursive;
		//        font-size: 15px;
		//        color: #1a3c6e;
		//        margin: 0;
		//        padding-right: 0px;
		//      }}
		//      footer {{
		//        margin-top: 40px;
		//        font-size: 15px;
		//        display: flex;
		//        justify-content: space-between;
		//        color: #000;
		//      }}
		//      .logo-placeholder {{
		//        display: inline-block;
		//        width: 200px;
		//        height: 80px;
		//        background: linear-gradient(135deg, #e3f2fd 0%, #bbdefb 100%);
		//        border-radius: 4px;
		//        position: relative;
		//        margin: 0 auto;
		//      }}
		//      .logo-placeholder::before {{
		//        content: ""ARTHROSE CRANIOFACIAL PAIN & TMJ CENTRE"";
		//        position: absolute;
		//        top: 50%;
		//        left: 50%;
		//        transform: translate(-50%, -50%);
		//        font-size: 15px;
		//        font-weight: bold;
		//        color: #1976d2;
		//        text-align: center;
		//      }}
		//    </style>
		//  </head>
		//  <body>
		//    <!-- Header with Logo -->
		//    <div class=""header"">
		//      <img src=""https://d7cop3y0lcg80.cloudfront.net/reports/1/0604d10d087f97b877ea0ae85e9494b5df28b6e7_26-09-2025_10-12-19.png"" alt=""ARTHROSE CRANIOFACIAL PAIN & TMJ CENTRE Logo"" />
		//    </div>

		//    <!-- Patient Info -->
		//    <div class=""patient-info"">
		//      <table>
		//        <tr>
		//          <td><strong>Patient Name:</strong> {data.Patient.Name}</td>
		//          <td><strong>HFID:</strong> {data.Patient.Hfid}</td>
		//        </tr>
		//        <tr>
		//          <td><strong>Gender:</strong> {data.Patient.Gender}</td>
		//          <td><strong>PRFID:</strong> {data.Patient.Prfid}</td>
		//        </tr>
		//        <tr>
		//          <td><strong>DOB:</strong> {formattedDob}</td>
		//          <td><strong>Mobile:</strong> {data.Patient.Mobile}</td>
		//        </tr>
		//        <tr>
		//          <td><strong>Consultant Coach:</strong> {data.Patient.Doctor}</td>
		//          <td><strong>City:</strong> {data.Patient.City}</td>
		//        </tr>
		//      </table>
		//    </div>

		//    <!-- Prescription Section -->
		//    <div class=""section-title"">Prescription</div>
		//    <table class=""prescription"">
		//      <thead>
		//        <tr>
		//          <th>S.No.</th>
		//          <th>Medication</th>
		//          <th>Dosage</th>
		//          <th>Duration</th>
		//          <th>Timing</th>
		//        </tr>
		//      </thead>
		//      <tbody>
		//        {medicationsTableRows}
		//      </tbody>
		//    </table>

		//    <!-- Additional Notes -->
		//    <div class=""additional-notes"">
		//      <strong>Additional Notes:</strong><br />
		//      {data.AdditionalNotes}
		//    </div>

		//    <!-- Signature -->
		//    <div class=""signature"">
		//      <p>{data.Patient.Doctor}</p>
		//      <div class=""line"">{data.Patient.Doctor}</div>
		//    </div>

		//    <footer>
		//      <span>www.arthrosetmjindia.com</span>
		//      <span>www.hfiles.in</span>
		//    </footer>
		//  </body>
		//</html>";
		//        }






		//        // API 1: Import treatments from Excel Arthrose
		//        [HttpPost("import-excel")]
		//        //[Authorize]
		//        public async Task<IActionResult> ImportTreatmentsFromExcel([FromForm] TreatmentImportRequest request)
		//        {
		//            HttpContext.Items["Log-Category"] = "Treatment Import";

		//            if (request.ExcelFile == null || request.ExcelFile.Length == 0)
		//                return BadRequest(ApiResponseFactory.Fail("Excel file is required."));

		//            if (!Path.GetExtension(request.ExcelFile.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase) &&
		//                !Path.GetExtension(request.ExcelFile.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
		//                return BadRequest(ApiResponseFactory.Fail("Only .csv and .xlsx files are supported."));

		//            //bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(CLINIC_ID, User);
		//            //if (!isAuthorized)
		//            //{
		//            //    _logger.LogWarning("Unauthorized treatment import attempt for Clinic ID {ClinicId}", CLINIC_ID);
		//            //    return Unauthorized(ApiResponseFactory.Fail("You are not authorized to import treatments for this clinic."));
		//            //}

		//            var response = new TreatmentImportResponse();
		//            var transaction = await _clinicRepository.BeginTransactionAsync();
		//            var committed = false;

		//            try
		//            {
		//                List<ExcelTreatmentRow> treatmentRows;

		//                if (Path.GetExtension(request.ExcelFile.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
		//                {
		//                    treatmentRows = await ProcessCsvFile(request.ExcelFile);
		//                }
		//                else
		//                {
		//                    treatmentRows = await ProcessExcelFile(request.ExcelFile);
		//                }

		//                if (!treatmentRows.Any())
		//                {
		//                    return BadRequest(ApiResponseFactory.Fail("No valid treatment data found in the file."));
		//                }

		//                // Group treatments by patient and date
		//                var groupedTreatments = treatmentRows
		//                    .GroupBy(t => new { t.PatientId, t.ParsedDate.Date })
		//                    .ToList();

		//                foreach (var group in groupedTreatments)
		//                {
		//                    response.TotalProcessed++;

		//                    try
		//                    {
		//                        var success = await ProcessTreatmentGroup(group.Key.PatientId, group.Key.Date, group.ToList(), response);
		//                        if (success)
		//                        {
		//                            response.Successful++;
		//                        }
		//                        else
		//                        {
		//                            response.Failed++;
		//                        }
		//                    }
		//                    catch (Exception ex)
		//                    {
		//                        _logger.LogError(ex, "Error processing treatment group for Patient {PatientId} on {Date}",
		//                            group.Key.PatientId, group.Key.Date);
		//                        response.Failed++;
		//                        response.SkippedReasons.Add($"Patient {group.Key.PatientId} on {group.Key.Date:dd-MM-yyyy}: Processing error - {ex.Message}");
		//                    }
		//                }

		//                await transaction.CommitAsync();
		//                committed = true;

		//                response.Message = $"Treatment import completed: {response.Successful} successful, " +
		//                                  $"{response.Failed} failed out of {response.TotalProcessed} total treatment groups. " +
		//                                  $"Processed {response.PatientsProcessed} patients with {response.VisitsCreated} visits.";

		//                _logger.LogInformation("Treatment import completed: {Added} added, {Failed} failed",
		//                    response.Successful, response.Failed);

		//                return Ok(ApiResponseFactory.Success(response, response.Message));
		//            }
		//            catch (Exception ex)
		//            {
		//                _logger.LogError(ex, "Failed to import treatments from Excel");
		//                return StatusCode(500, ApiResponseFactory.Fail("Failed to process Excel file: " + ex.Message));
		//            }
		//            finally
		//            {
		//                if (!committed && transaction.GetDbTransaction().Connection != null)
		//                    await transaction.RollbackAsync();
		//            }
		//        }






		//        // API 2: Generate treatment PDFs for all unsent treatments Arthrose
		//        [HttpPost("generate-pdfs")]
		//        //[Authorize]
		//        public async Task<IActionResult> GenerateTreatmentPdfs([FromBody] TreatmentPdfRequest request)
		//        {
		//            HttpContext.Items["Log-Category"] = "Treatment PDF Generation";

		//            //bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(CLINIC_ID, User);
		//            //if (!isAuthorized)
		//            //{
		//            //    _logger.LogWarning("Unauthorized PDF generation attempt for Clinic ID {ClinicId}", CLINIC_ID);
		//            //    return Unauthorized(ApiResponseFactory.Fail("You are not authorized to generate PDFs for this clinic."));
		//            //}

		//            var response = new TreatmentPdfResponse();
		//            var transaction = await _clinicRepository.BeginTransactionAsync();
		//            var committed = false;

		//            try
		//            {
		//                // Get all unsent treatment records
		//                var treatmentRecords = await GetUnsentTreatmentRecords();

		//                if (!treatmentRecords.Any())
		//                {
		//                    return Ok(ApiResponseFactory.Success(new TreatmentPdfResponse
		//                    {
		//                        TotalProcessed = 0,
		//                        Successful = 0,
		//                        Failed = 0,
		//                        Message = "No unsent treatment records found."
		//                    }, "No unsent treatment records found."));
		//                }

		//                // Download Chromium once for all PDFs
		//                await new BrowserFetcher().DownloadAsync();

		//                using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
		//                {
		//                    Headless = true,
		//                    Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
		//                });

		//                foreach (var treatmentRecord in treatmentRecords)
		//                {
		//                    response.TotalProcessed++;

		//                    try
		//                    {
		//                        var success = await ProcessSingleTreatmentPdf(treatmentRecord, browser, response);
		//                        if (success)
		//                        {
		//                            response.Successful++;
		//                        }
		//                        else
		//                        {
		//                            response.Failed++;
		//                            response.FailedRecords.Add(new TreatmentPdfFailed
		//                            {
		//                                RecordId = treatmentRecord.Id,
		//                                Reason = "PDF generation or upload failed"
		//                            });
		//                        }
		//                    }
		//                    catch (Exception ex)
		//                    {
		//                        _logger.LogError(ex, "Error generating PDF for treatment record {RecordId}", treatmentRecord.Id);
		//                        response.Failed++;
		//                        response.FailedRecords.Add(new TreatmentPdfFailed
		//                        {
		//                            RecordId = treatmentRecord.Id,
		//                            Reason = $"Processing error: {ex.Message}"
		//                        });
		//                    }
		//                }

		//                await transaction.CommitAsync();
		//                committed = true;

		//                response.Message = $"Treatment PDF generation completed: {response.Successful} successful, " +
		//                                  $"{response.Failed} failed out of {response.TotalProcessed} total treatments.";

		//                return Ok(ApiResponseFactory.Success(response, response.Message));
		//            }
		//            catch (Exception ex)
		//            {
		//                _logger.LogError(ex, "Error during treatment PDF generation");
		//                return StatusCode(500, ApiResponseFactory.Fail("An error occurred during PDF generation."));
		//            }
		//            finally
		//            {
		//                if (!committed && transaction.GetDbTransaction().Connection != null)
		//                    await transaction.RollbackAsync();
		//            }
		//        }

		//        private async Task<List<ExcelTreatmentRow>> ProcessCsvFile(IFormFile file)
		//        {
		//            var treatments = new List<ExcelTreatmentRow>();

		//            using var reader = new StreamReader(file.OpenReadStream());
		//            var csvContent = await reader.ReadToEndAsync();
		//            var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

		//            for (int i = 1; i < lines.Length; i++) // Skip header
		//            {
		//                var columns = lines[i].Split(',');
		//                if (columns.Length >= 9)
		//                {
		//                    var treatment = new ExcelTreatmentRow
		//                    {
		//                        PatientName = columns[0].Trim(),
		//                        PatientId = columns[1].Trim(),
		//                        DateString = columns[2].Trim(),
		//                        TreatmentName = columns[3].Trim(),
		//                        Status = columns[4].Trim(),
		//                        Cost = int.TryParse(columns[5].Trim(), out var cost) ? cost : 0,
		//                        Quantity = int.TryParse(columns[6].Trim(), out var qty) ? qty : 1,
		//                        QuantityType = columns[7].Trim(),
		//                        FinalCost = int.TryParse(columns[8].Trim(), out var finalCost) ? finalCost : 0
		//                    };

		//                    if (DateTime.TryParseExact(treatment.DateString, "dd-MM-yyyy", null, DateTimeStyles.None, out var parsedDate))
		//                    {
		//                        treatment.ParsedDate = parsedDate;
		//                        treatments.Add(treatment);
		//                    }
		//                }
		//            }

		//            return treatments;
		//        }

		//        private async Task<List<ExcelTreatmentRow>> ProcessExcelFile(IFormFile file)
		//        {
		//            var treatments = new List<ExcelTreatmentRow>();

		//            using var stream = new MemoryStream();
		//            await file.CopyToAsync(stream);

		//            ExcelPackage.License.SetNonCommercialPersonal("HFiles");
		//            using var package = new ExcelPackage(stream);
		//            var worksheet = package.Workbook.Worksheets[0];

		//            if (worksheet.Dimension == null) return treatments;

		//            var rowCount = worksheet.Dimension.End.Row;

		//            for (int row = 2; row <= rowCount; row++)
		//            {
		//                var treatment = new ExcelTreatmentRow
		//                {
		//                    PatientName = worksheet.Cells[row, 1].Text.Trim(),
		//                    PatientId = worksheet.Cells[row, 2].Text.Trim(),
		//                    DateString = worksheet.Cells[row, 3].Text.Trim(),
		//                    TreatmentName = worksheet.Cells[row, 4].Text.Trim(),
		//                    Status = worksheet.Cells[row, 5].Text.Trim(),
		//                    Cost = int.TryParse(worksheet.Cells[row, 6].Text.Trim(), out var cost) ? cost : 0,
		//                    Quantity = int.TryParse(worksheet.Cells[row, 7].Text.Trim(), out var qty) ? qty : 1,
		//                    QuantityType = worksheet.Cells[row, 8].Text.Trim(),
		//                    FinalCost = int.TryParse(worksheet.Cells[row, 9].Text.Trim(), out var finalCost) ? finalCost : 0
		//                };

		//                if (DateTime.TryParseExact(treatment.DateString, "dd-MM-yyyy", null, DateTimeStyles.None, out var parsedDate))
		//                {
		//                    treatment.ParsedDate = parsedDate;
		//                    treatments.Add(treatment);
		//                }
		//            }

		//            return treatments;
		//        }

		//        private async Task<bool> ProcessTreatmentGroup(string patientId, DateTime appointmentDate,
		//            List<ExcelTreatmentRow> treatments, TreatmentImportResponse response)
		//        {
		//            // 1. Find user by patientId
		//            var user = await _userRepository.GetUserByPatientIdAsync(patientId);
		//            if (user == null)
		//            {
		//                response.SkippedReasons.Add($"Patient {patientId}: User not found in database");
		//                return false;
		//            }

		//            // 2. Get or create clinic patient
		//            var fullName = $"{user.FirstName} {user.LastName}".Trim();
		//            var clinicPatient = await _clinicVisitRepository.GetOrCreatePatientAsync(user.HfId ?? "", fullName);

		//            // 3. Check if visit exists for this date
		//            var existingVisit = await GetExistingVisitByDate(clinicPatient.Id, appointmentDate);
		//            ClinicVisit visit;

		//            if (existingVisit == null)
		//            {
		//                // Create new visit
		//                visit = new ClinicVisit
		//                {
		//                    ClinicPatientId = clinicPatient.Id,
		//                    ClinicId = CLINIC_ID,
		//                    AppointmentDate = appointmentDate.Date,
		//                    AppointmentTime = APPOINTMENT_TIME,
		//                    PaymentMethod = null
		//                };

		//                await _clinicVisitRepository.SaveVisitAsync(visit);
		//                response.VisitsCreated++;
		//            }
		//            else
		//            {
		//                visit = existingVisit;
		//            }

		//            // 4. Create treatment JSON data
		//            var treatmentJsonData = CreateTreatmentJsonData(user, clinicPatient, treatments, patientId);

		//            // 5. Check if treatment record already exists
		//            var existingRecord = await _clinicPatientRecordRepository.GetByCompositeKeyAsync(
		//                CLINIC_ID, clinicPatient.Id, visit.Id, RecordType.Treatment);

		//            if (existingRecord != null)
		//            {
		//                response.SkippedReasons.Add($"Patient {patientId} on {appointmentDate:dd-MM-yyyy}: Treatment record already exists");
		//                return false;
		//            }

		//            // 6. Create treatment record
		//            var treatmentRecord = new ClinicPatientRecord
		//            {
		//                ClinicId = CLINIC_ID,
		//                PatientId = clinicPatient.Id,
		//                ClinicVisitId = visit.Id,
		//                Type = RecordType.Treatment,
		//                JsonData = treatmentJsonData,
		//                SendToPatient = false
		//            };

		//            await _clinicPatientRecordRepository.SaveAsync(treatmentRecord);

		//            response.PatientsProcessed++;
		//            response.AddedTreatments.Add(new AddedTreatmentSummary
		//            {
		//                PatientId = patientId,
		//                PatientName = fullName,
		//                HFID = user.HfId ?? "",
		//                AppointmentDate = appointmentDate.ToString("dd-MM-yyyy"),
		//                TreatmentCount = treatments.Count,
		//                TotalCost = treatments.Sum(t => t.FinalCost),
		//                Treatments = treatments.Select(t => t.TreatmentName).ToList()
		//            });

		//            return true;
		//        }

		//        private async Task<ClinicVisit?> GetExistingVisitByDate(int clinicPatientId, DateTime appointmentDate)
		//        {
		//            return await _clinicVisitRepository.GetExistingVisitAsync(clinicPatientId, appointmentDate);
		//        }

		//        private string CreateTreatmentJsonData(Domain.Entities.Users.User user, ClinicPatient clinicPatient,
		//            List<ExcelTreatmentRow> treatments, string patientId)
		//        {
		//            var treatmentItems = treatments.Select(t => new
		//            {
		//                name = t.TreatmentName,
		//                qtyPerDay = $"{t.Quantity} {t.QuantityType}",
		//                cost = t.Cost,
		//                status = "Not Started",
		//                total = t.FinalCost
		//            }).ToArray();

		//            var totalCost = treatments.Sum(t => t.FinalCost);

		//            var treatmentData = new
		//            {
		//                patient = new
		//                {
		//                    name = $"{user.FirstName} {user.LastName}".Trim(),
		//                    hfid = user.HfId ?? "",
		//                    gender = user.Gender ?? "",
		//                    tid = patientId,
		//                    dob = user.DOB ?? "",
		//                    mobile = user.PhoneNumber ?? "",
		//                    doctor = DOCTOR_NAME,
		//                    city = user.City ?? ""
		//                },
		//                treatments = treatmentItems,
		//                totalCost = totalCost,
		//                grandTotal = totalCost,
		//                clinicInfo = new
		//                {
		//                    name = "ARTHROSE",
		//                    subtitle = "CRANIOFACIAL PAIN & TMJ CENTRE",
		//                    website = "www.arthrosetmjindia.com"
		//                }
		//            };

		//            return JsonConvert.SerializeObject(treatmentData, Formatting.None);
		//        }

		//        private async Task<List<ClinicPatientRecord>> GetUnsentTreatmentRecords()
		//        {
		//            return await _clinicPatientRecordRepository.GetUnsentTreatmentRecordsAsync(CLINIC_ID);
		//        }

		//        private async Task<bool> ProcessSingleTreatmentPdf(ClinicPatientRecord treatmentRecord,
		//            IBrowser browser, TreatmentPdfResponse response)
		//        {
		//            try
		//            {
		//                // Get related data
		//                var visit = await _clinicVisitRepository.GetByIdAsync(treatmentRecord.ClinicVisitId);
		//                var clinicPatient = await _clinicPatientRecordRepository.GetByIdAsync(treatmentRecord.PatientId);
		//                var user = clinicPatient != null ? await _userRepository.GetUserByHFIDAsync(clinicPatient.HFID) : null;

		//                if (visit == null || clinicPatient == null || user == null)
		//                {
		//                    return false;
		//                }

		//                // Parse treatment JSON
		//                var treatmentData = JsonConvert.DeserializeObject<TreatmentJsonPayload>(treatmentRecord.JsonData);
		//                if (treatmentData?.Patient == null || treatmentData.Treatments == null)
		//                {
		//                    return false;
		//                }

		//                // Generate HTML and PDF
		//                var htmlContent = GenerateTreatmentHtml(treatmentData);

		//                using var page = await browser.NewPageAsync();
		//                await page.SetContentAsync(htmlContent);

		//                var pdfOptions = new PdfOptions
		//                {
		//                    Format = PaperFormat.A4,
		//                    PrintBackground = true,
		//                    MarginOptions = new MarginOptions
		//                    {
		//                        Top = "0.5in",
		//                        Bottom = "0.5in",
		//                        Left = "0.5in",
		//                        Right = "0.5in"
		//                    }
		//                };

		//                var pdfBytes = await page.PdfDataAsync(pdfOptions);
		//                if (pdfBytes == null || pdfBytes.Length == 0)
		//                {
		//                    return false;
		//                }

		//                // Upload to S3
		//                var tempFileName = $"treatment_{treatmentRecord.ClinicId}_{treatmentRecord.PatientId}_{treatmentRecord.ClinicVisitId}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
		//                var tempFilePath = Path.Combine(Path.GetTempPath(), tempFileName);
		//                await System.IO.File.WriteAllBytesAsync(tempFilePath, pdfBytes);

		//                var s3Key = $"clinic/{tempFileName}";
		//                var s3Url = await _s3StorageService.UploadFileToS3(tempFilePath, s3Key);

		//                if (string.IsNullOrEmpty(s3Url))
		//                {
		//                    System.IO.File.Delete(tempFilePath);
		//                    return false;
		//                }

		//                // Update record
		//                treatmentRecord.SendToPatient = true;
		//                await _clinicPatientRecordRepository.UpdateAsync(treatmentRecord);

		//                // Create user report
		//                var reportName = $"Arthrose_Treatment_{treatmentData.Patient.Name.Replace(" ", "_")}_{visit.AppointmentDate:dd-MM-yy}";
		//                var epochTime = new DateTimeOffset(visit.AppointmentDate.Date + visit.AppointmentTime).ToUnixTimeSeconds();
		//                var fileSizeKb = Math.Round((decimal)pdfBytes.Length / 1024, 2);

		//                var userReport = new UserReport
		//                {
		//                    UserId = user.Id,
		//                    ReportName = reportName,
		//                    ReportCategory = 1, // Treatment category
		//                    ReportUrl = s3Url,
		//                    EpochTime = epochTime,
		//                    FileSize = fileSizeKb,
		//                    UploadedBy = "Clinic",
		//                    ClinicId = 8,
		//                    UserType = "Independent",
		//                    DeletedBy = 0
		//                };

		//                await _userRepository.SaveAsync(userReport);

		//                // Clean up
		//                System.IO.File.Delete(tempFilePath);

		//                // Add to successful records
		//                response.SuccessfulRecords.Add(new TreatmentPdfSuccess
		//                {
		//                    RecordId = treatmentRecord.Id,
		//                    PatientName = treatmentData.Patient.Name,
		//                    HFID = clinicPatient.HFID,
		//                    AppointmentDate = visit.AppointmentDate.ToString("dd-MM-yyyy"),
		//                    TreatmentUrl = s3Url,
		//                    FileSizeKB = fileSizeKb
		//                });

		//                return true;
		//            }
		//            catch (Exception ex)
		//            {
		//                _logger.LogError(ex, "Error processing treatment PDF for record {RecordId}", treatmentRecord.Id);
		//                return false;
		//            }
		//        }

		//        private string GenerateTreatmentHtml(TreatmentJsonPayload data)
		//        {
		//            var treatmentTableRows = new StringBuilder();

		//            for (int i = 0; i < data.Treatments.Count; i++)
		//            {
		//                var treatment = data.Treatments[i];
		//                treatmentTableRows.AppendLine($@"
		//                    <tr>
		//                        <td>{i + 1}</td>
		//                        <td>{treatment.Name}</td>
		//                        <td>{treatment.QtyPerDay}</td>
		//                        <td class=""right"">{treatment.Cost:N2}</td>
		//                        <td>{treatment.Status}</td>
		//                        <td class=""right"">{treatment.Total:N2}</td>
		//                    </tr>");
		//            }

		//            var formattedDob = !string.IsNullOrEmpty(data.Patient.Dob) ? data.Patient.Dob : " ";

		//            return $@"
		//<!DOCTYPE html>
		//<html lang=""en"">
		//  <head>
		//    <meta charset=""UTF-8"" />
		//    <style>
		//      body {{
		//        font-family: Arial, Helvetica, sans-serif;
		//        margin: 40px;
		//        color: #333;
		//        background-color: #fff;
		//      }}
		//      .header {{
		//        text-align: center;
		//        margin-bottom: 20px;
		//      }}
		//      .header img {{
		//        max-height: 130px;
		//        width: auto;
		//      }}
		//      .patient-info {{
		//        border: 1px solid #e5e7eb;
		//        border-radius: 8px;
		//        padding: 16px;
		//        margin-bottom: 24px;
		//        font-size: 13px;
		//        background-color: #fafafa;
		//      }}
		//      .patient-info table {{
		//        width: 100%;
		//        border-collapse: collapse;
		//      }}
		//      .patient-info td {{
		//        padding: 4px 8px;
		//        vertical-align: top;
		//      }}
		//      .section-title {{
		//        font-weight: bold;
		//        margin-bottom: 8px;
		//        font-size: 14px;
		//        color: #333;
		//      }}
		//      table.treatment {{
		//        width: 100%;
		//        border-collapse: collapse;
		//        margin-top: 8px;
		//        font-size: 13px;
		//        border: 1px solid #000;
		//      }}
		//      table.treatment th,
		//      table.treatment td {{
		//        border: 1px solid #000;
		//        padding: 10px;
		//        text-align: left;
		//      }}
		//      table.treatment th {{
		//        background: #f9f9f9;
		//        font-weight: 600;
		//        color: #333;
		//      }}
		//      table.treatment td.right {{
		//        text-align: right;
		//      }}
		//      .totals {{
		//        font-weight: bold;
		//        background-color: #f9f9f9;
		//      }}
		//      .totals td {{
		//        border-top: 2px solid #000;
		//      }}
		//      .signature {{
		//        margin-top: 40px;
		//        text-align: right;
		//        font-size: 14px;
		//        color: #333;
		//      }}
		//      .signature .line {{
		//        margin-bottom: 2px;
		//        border-top: 1px solid #333;
		//        width: 116px;
		//        margin-left: auto;
		//      }}
		//      .signature p {{
		//        font-family: ""Cedarville Cursive"", cursive;
		//        font-size: 15px;
		//        color: #1a3c6e;
		//        margin: 0;
		//        padding-right: 0px;
		//      }}
		//      footer {{
		//        margin-top: 40px;
		//        font-size: 15px;
		//        display: flex;
		//        justify-content: space-between;
		//        color: #000;
		//      }}
		//    </style>
		//  </head>
		//  <body>
		//    <!-- Header with Logo -->
		//    <div class=""header"">
		//      <img src=""https://d7cop3y0lcg80.cloudfront.net/reports/1/0604d10d087f97b877ea0ae85e9494b5df28b6e7_26-09-2025_10-12-19.png"" alt=""ARTHROSE CRANIOFACIAL PAIN & TMJ CENTRE Logo"" />
		//    </div>

		//    <!-- Patient Info -->
		//    <div class=""patient-info"">
		//      <table>
		//        <tr>
		//          <td><strong>Patient Name:</strong> {data.Patient.Name}</td>
		//          <td><strong>HFID:</strong> {data.Patient.Hfid}</td>
		//        </tr>
		//        <tr>
		//          <td><strong>Gender:</strong> {data.Patient.Gender}</td>
		//          <td><strong>TID:</strong> {data.Patient.Tid}</td>
		//        </tr>
		//        <tr>
		//          <td><strong>DOB:</strong> {formattedDob}</td>
		//          <td><strong>Mobile:</strong> {data.Patient.Mobile}</td>
		//        </tr>
		//        <tr>
		//          <td><strong>Consultant Coach:</strong> {data.Patient.Doctor}</td>
		//          <td><strong>City:</strong> {data.Patient.City}</td>
		//        </tr>
		//      </table>
		//    </div>

		//    <!-- Treatment Plan -->
		//    <div class=""section-title"">Treatment Plan</div>
		//    <table class=""treatment"">
		//      <thead>
		//        <tr>
		//          <th>S.No.</th>
		//          <th>Treatment Name</th>
		//          <th>Qty/Day</th>
		//          <th>Cost (₹)</th>
		//          <th>Status</th>
		//          <th>Total (₹)</th>
		//        </tr>
		//      </thead>
		//      <tbody>
		//        {treatmentTableRows}
		//        <tr class=""totals"">
		//          <td colspan=""5"" class=""right"">Total:</td>
		//          <td class=""right"">{data.TotalCost:N2}</td>
		//        </tr>
		//        <tr class=""totals"">
		//          <td colspan=""5"" class=""right"">Grand Total:</td>
		//          <td class=""right"">{data.GrandTotal:N2}</td>
		//        </tr>
		//      </tbody>
		//    </table>

		//    <!-- Signature -->
		//    <div class=""signature"">
		//      <p>{data.Patient.Doctor}</p>
		//      <div class=""line"">{data.Patient.Doctor}</div>
		//    </div>

		//    <footer>
		//      <span>www.arthrosetmjindia.com</span>
		//      <span>www.hfiles.in</span>
		//    </footer>
		//  </body>
		//</html>";
		//        }




		// Add these methods to the ClinicPatientRecordController class

		// API 1: Import invoices from Excel/CSV
		//        [HttpPost("import-invoices")]
		//        //[Authorize]
		//        public async Task<IActionResult> ImportInvoicesFromExcel([FromForm] InvoiceImportRequest request)
		//        {
		//            HttpContext.Items["Log-Category"] = "Invoice Import";

		//            if (request.ExcelFile == null || request.ExcelFile.Length == 0)
		//                return BadRequest(ApiResponseFactory.Fail("Excel file is required."));

		//            if (!Path.GetExtension(request.ExcelFile.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase) &&
		//                !Path.GetExtension(request.ExcelFile.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
		//                return BadRequest(ApiResponseFactory.Fail("Only .csv and .xlsx files are supported."));

		//            //bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(CLINIC_ID, User);
		//            //if (!isAuthorized)
		//            //{
		//            //    _logger.LogWarning("Unauthorized invoice import attempt for Clinic ID {ClinicId}", CLINIC_ID);
		//            //    return Unauthorized(ApiResponseFactory.Fail("You are not authorized to import invoices for this clinic."));
		//            //}

		//            var response = new InvoiceImportResponse();
		//            var transaction = await _clinicRepository.BeginTransactionAsync();
		//            var committed = false;

		//            try
		//            {
		//                List<ExcelInvoiceRow> invoiceRows;

		//                if (Path.GetExtension(request.ExcelFile.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
		//                {
		//                    invoiceRows = await ProcessInvoiceCsvFile(request.ExcelFile);
		//                }
		//                else
		//                {
		//                    invoiceRows = await ProcessInvoiceExcelFile(request.ExcelFile);
		//                }

		//                if (!invoiceRows.Any())
		//                {
		//                    return BadRequest(ApiResponseFactory.Fail("No valid invoice data found in the file."));
		//                }

		//                // Group invoices by patient, date, and invoice ID
		//                var groupedInvoices = invoiceRows
		//                    .GroupBy(i => new { i.PatientId, i.ParsedDate.Date, i.InvoiceId })
		//                    .ToList();

		//                foreach (var group in groupedInvoices)
		//                {
		//                    response.TotalProcessed++;

		//                    try
		//                    {
		//                        var success = await ProcessInvoiceGroup(
		//                            group.Key.PatientId,
		//                            group.Key.Date,
		//                            group.Key.InvoiceId,
		//                            group.ToList(),
		//                            response);

		//                        if (success)
		//                        {
		//                            response.Successful++;
		//                        }
		//                        else
		//                        {
		//                            response.Failed++;
		//                        }
		//                    }
		//                    catch (Exception ex)
		//                    {
		//                        _logger.LogError(ex, "Error processing invoice group for Patient {PatientId} on {Date} Invoice {InvoiceId}",
		//                            group.Key.PatientId, group.Key.Date, group.Key.InvoiceId);
		//                        response.Failed++;
		//                        response.SkippedReasons.Add($"Patient {group.Key.PatientId} on {group.Key.Date:dd-MM-yyyy} Invoice {group.Key.InvoiceId}: Processing error - {ex.Message}");
		//                    }
		//                }

		//                await transaction.CommitAsync();
		//                committed = true;

		//                response.Message = $"Invoice import completed: {response.Successful} successful, " +
		//                                  $"{response.Failed} failed out of {response.TotalProcessed} total invoice groups. " +
		//                                  $"Processed {response.PatientsProcessed} patients with {response.VisitsCreated} visits.";

		//                _logger.LogInformation("Invoice import completed: {Added} added, {Failed} failed",
		//                    response.Successful, response.Failed);

		//                return Ok(ApiResponseFactory.Success(response, response.Message));
		//            }
		//            catch (Exception ex)
		//            {
		//                _logger.LogError(ex, "Failed to import invoices from Excel");
		//                return StatusCode(500, ApiResponseFactory.Fail("Failed to process Excel file: " + ex.Message));
		//            }
		//            finally
		//            {
		//                if (!committed && transaction.GetDbTransaction().Connection != null)
		//                    await transaction.RollbackAsync();
		//            }
		//        }

		//        // API 2: Generate invoice PDFs for all unsent invoices
		//        [HttpPost("generate-invoice-pdfs")]
		//        //[Authorize]
		//        public async Task<IActionResult> GenerateInvoicePdfs([FromBody] InvoicePdfRequest request)
		//        {
		//            HttpContext.Items["Log-Category"] = "Invoice PDF Generation";

		//            //bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(CLINIC_ID, User);
		//            //if (!isAuthorized)
		//            //{
		//            //    _logger.LogWarning("Unauthorized PDF generation attempt for Clinic ID {ClinicId}", CLINIC_ID);
		//            //    return Unauthorized(ApiResponseFactory.Fail("You are not authorized to generate PDFs for this clinic."));
		//            //}

		//            var response = new InvoicePdfResponse();
		//            var transaction = await _clinicRepository.BeginTransactionAsync();
		//            var committed = false;

		//            try
		//            {
		//                // Get all unsent invoice records
		//                var invoiceRecords = await GetUnsentInvoiceRecords();

		//                if (!invoiceRecords.Any())
		//                {
		//                    return Ok(ApiResponseFactory.Success(new InvoicePdfResponse
		//                    {
		//                        TotalProcessed = 0,
		//                        Successful = 0,
		//                        Failed = 0,
		//                        Message = "No unsent invoice records found."
		//                    }, "No unsent invoice records found."));
		//                }

		//                // Download Chromium once for all PDFs
		//                await new BrowserFetcher().DownloadAsync();

		//                using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
		//                {
		//                    Headless = true,
		//                    Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
		//                });

		//                foreach (var invoiceRecord in invoiceRecords)
		//                {
		//                    response.TotalProcessed++;

		//                    try
		//                    {
		//                        var success = await ProcessSingleInvoicePdf(invoiceRecord, browser, response);
		//                        if (success)
		//                        {
		//                            response.Successful++;
		//                        }
		//                        else
		//                        {
		//                            response.Failed++;
		//                            response.FailedRecords.Add(new InvoicePdfFailed
		//                            {
		//                                RecordId = invoiceRecord.Id,
		//                                Reason = "PDF generation or upload failed"
		//                            });
		//                        }
		//                    }
		//                    catch (Exception ex)
		//                    {
		//                        _logger.LogError(ex, "Error generating PDF for invoice record {RecordId}", invoiceRecord.Id);
		//                        response.Failed++;
		//                        response.FailedRecords.Add(new InvoicePdfFailed
		//                        {
		//                            RecordId = invoiceRecord.Id,
		//                            Reason = $"Processing error: {ex.Message}"
		//                        });
		//                    }
		//                }

		//                await transaction.CommitAsync();
		//                committed = true;

		//                response.Message = $"Invoice PDF generation completed: {response.Successful} successful, " +
		//                                  $"{response.Failed} failed out of {response.TotalProcessed} total invoices.";

		//                return Ok(ApiResponseFactory.Success(response, response.Message));
		//            }
		//            catch (Exception ex)
		//            {
		//                _logger.LogError(ex, "Error during invoice PDF generation");
		//                return StatusCode(500, ApiResponseFactory.Fail("An error occurred during PDF generation."));
		//            }
		//            finally
		//            {
		//                if (!committed && transaction.GetDbTransaction().Connection != null)
		//                    await transaction.RollbackAsync();
		//            }
		//        }

		//        // Helper methods for invoice processing
		//        private async Task<List<ExcelInvoiceRow>> ProcessInvoiceCsvFile(IFormFile file)
		//        {
		//            var invoices = new List<ExcelInvoiceRow>();

		//            using var reader = new StreamReader(file.OpenReadStream());
		//            var csvContent = await reader.ReadToEndAsync();
		//            var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

		//            for (int i = 1; i < lines.Length; i++) // Skip header
		//            {
		//                var columns = lines[i].Split(',');
		//                if (columns.Length >= 8)
		//                {
		//                    var invoice = new ExcelInvoiceRow
		//                    {
		//                        PatientName = columns[0].Trim(),
		//                        PatientId = columns[1].Trim(),
		//                        DateString = columns[2].Trim(),
		//                        InvoiceId = columns[3].Trim(),
		//                        ServiceName = columns[4].Trim(),
		//                        Cost = int.TryParse(columns[5].Trim(), out var cost) ? cost : 0,
		//                        QuantityValue = columns[6].Trim(),
		//                        FinalCost = int.TryParse(columns[7].Trim(), out var finalCost) ? finalCost : 0
		//                    };

		//                    if (DateTime.TryParseExact(invoice.DateString, "dd-MM-yyyy", null, DateTimeStyles.None, out var parsedDate))
		//                    {
		//                        invoice.ParsedDate = parsedDate;
		//                        invoices.Add(invoice);
		//                    }
		//                }
		//            }

		//            return invoices;
		//        }

		//        private async Task<List<ExcelInvoiceRow>> ProcessInvoiceExcelFile(IFormFile file)
		//        {
		//            var invoices = new List<ExcelInvoiceRow>();

		//            using var stream = new MemoryStream();
		//            await file.CopyToAsync(stream);

		//            ExcelPackage.License.SetNonCommercialPersonal("HFiles");
		//            using var package = new ExcelPackage(stream);
		//            var worksheet = package.Workbook.Worksheets[0];

		//            if (worksheet.Dimension == null) return invoices;

		//            var rowCount = worksheet.Dimension.End.Row;

		//            for (int row = 2; row <= rowCount; row++)
		//            {
		//                var invoice = new ExcelInvoiceRow
		//                {
		//                    PatientName = worksheet.Cells[row, 1].Text.Trim(),
		//                    PatientId = worksheet.Cells[row, 2].Text.Trim(),
		//                    DateString = worksheet.Cells[row, 3].Text.Trim(),
		//                    InvoiceId = worksheet.Cells[row, 4].Text.Trim(),
		//                    ServiceName = worksheet.Cells[row, 5].Text.Trim(),
		//                    Cost = int.TryParse(worksheet.Cells[row, 6].Text.Trim(), out var cost) ? cost : 0,
		//                    QuantityValue = worksheet.Cells[row, 7].Text.Trim(),
		//                    FinalCost = int.TryParse(worksheet.Cells[row, 8].Text.Trim(), out var finalCost) ? finalCost : 0
		//                };

		//                if (DateTime.TryParseExact(invoice.DateString, "dd-MM-yyyy", null, DateTimeStyles.None, out var parsedDate))
		//                {
		//                    invoice.ParsedDate = parsedDate;
		//                    invoices.Add(invoice);
		//                }
		//            }

		//            return invoices;
		//        }

		//        private async Task<bool> ProcessInvoiceGroup(string patientId, DateTime appointmentDate,
		//            string invoiceId, List<ExcelInvoiceRow> invoices, InvoiceImportResponse response)
		//        {
		//            // 1. Find user by patientId
		//            var user = await _userRepository.GetUserByPatientIdAsync(patientId);
		//            if (user == null)
		//            {
		//                response.SkippedReasons.Add($"Patient {patientId}: User not found in database");
		//                return false;
		//            }

		//            // 2. Get or create clinic patient
		//            var fullName = $"{user.FirstName} {user.LastName}".Trim();
		//            var clinicPatient = await _clinicVisitRepository.GetOrCreatePatientAsync(user.HfId ?? "", fullName);

		//            // 3. Check if visit exists for this date
		//            var existingVisit = await GetExistingVisitByDate(clinicPatient.Id, appointmentDate);
		//            ClinicVisit visit;

		//            if (existingVisit == null)
		//            {
		//                // Create new visit
		//                visit = new ClinicVisit
		//                {
		//                    ClinicPatientId = clinicPatient.Id,
		//                    ClinicId = CLINIC_ID,
		//                    AppointmentDate = appointmentDate.Date,
		//                    AppointmentTime = APPOINTMENT_TIME,
		//                    PaymentMethod = null
		//                };

		//                await _clinicVisitRepository.SaveVisitAsync(visit);
		//                response.VisitsCreated++;
		//            }
		//            else
		//            {
		//                visit = existingVisit;
		//            }

		//            // 4. Create invoice JSON data
		//            var invoiceJsonData = CreateInvoiceJsonData(user, clinicPatient, invoices, invoiceId, appointmentDate);

		//            // 5. Check if invoice record already exists
		//            var existingRecord = await _clinicPatientRecordRepository.GetByCompositeKeyAsync(
		//                CLINIC_ID, clinicPatient.Id, visit.Id, RecordType.Invoice);

		//            if (existingRecord != null)
		//            {
		//                response.SkippedReasons.Add($"Patient {patientId} on {appointmentDate:dd-MM-yyyy} Invoice {invoiceId}: Invoice record already exists");
		//                return false;
		//            }

		//            // 6. Create invoice record
		//            var invoiceRecord = new ClinicPatientRecord
		//            {
		//                ClinicId = CLINIC_ID,
		//                PatientId = clinicPatient.Id,
		//                ClinicVisitId = visit.Id,
		//                Type = RecordType.Invoice,
		//                JsonData = invoiceJsonData,
		//                SendToPatient = false
		//            };

		//            await _clinicPatientRecordRepository.SaveAsync(invoiceRecord);

		//            response.PatientsProcessed++;
		//            response.AddedInvoices.Add(new AddedInvoiceSummary
		//            {
		//                PatientId = patientId,
		//                PatientName = fullName,
		//                HFID = user.HfId ?? "",
		//                AppointmentDate = appointmentDate.ToString("dd-MM-yyyy"),
		//                InvoiceId = invoiceId,
		//                ServiceCount = invoices.Count,
		//                TotalCost = invoices.Sum(i => i.FinalCost),
		//                Services = invoices.Select(i => i.ServiceName).ToList()
		//            });

		//            return true;
		//        }

		//        private string CreateInvoiceJsonData(Domain.Entities.Users.User user, ClinicPatient clinicPatient,
		//            List<ExcelInvoiceRow> invoices, string invoiceId, DateTime appointmentDate)
		//        {
		//            var serviceItems = invoices.Select(i => new
		//            {
		//                name = i.ServiceName,
		//                qtyPerDay = i.QuantityValue,
		//                cost = i.Cost,
		//                total = i.FinalCost
		//            }).ToArray();

		//            var totalCost = invoices.Sum(i => i.FinalCost);

		//            var invoiceData = new
		//            {
		//                patient = new
		//                {
		//                    name = $"{user.FirstName} {user.LastName}".Trim(),
		//                    hfid = user.HfId ?? "",
		//                    gender = user.Gender ?? "",
		//                    invid = invoiceId,
		//                    dob = user.DOB ?? "",
		//                    date = appointmentDate.ToString("dd/MM/yyyy"),
		//                    mobile = user.PhoneNumber ?? "",
		//                    city = user.City ?? ""
		//                },
		//                services = serviceItems,
		//                totalCost = totalCost,
		//                grandTotal = totalCost,
		//                paid = totalCost,
		//                clinicInfo = new
		//                {
		//                    name = "ARTHROSE",
		//                    subtitle = "CRANIOFACIAL PAIN & TMJ CENTRE",
		//                    website = "www.arthrosetmjindia.com"
		//                }
		//            };

		//            return JsonConvert.SerializeObject(invoiceData, Formatting.None);
		//        }

		//        private async Task<List<ClinicPatientRecord>> GetUnsentInvoiceRecords()
		//        {
		//            return await _clinicPatientRecordRepository.GetUnsentInvoiceRecordsAsync(CLINIC_ID); // Gets only UNSENT invoices
		//        }

		//        private async Task<bool> ProcessSingleInvoicePdf(ClinicPatientRecord invoiceRecord,
		//            IBrowser browser, InvoicePdfResponse response)
		//        {
		//            try
		//            {
		//                // Get related data
		//                var visit = await _clinicVisitRepository.GetByIdAsync(invoiceRecord.ClinicVisitId);
		//                var clinicPatient = await _clinicPatientRecordRepository.GetByIdAsync(invoiceRecord.PatientId);
		//                var user = clinicPatient != null ? await _userRepository.GetUserByHFIDAsync(clinicPatient.HFID) : null;

		//                if (visit == null || clinicPatient == null || user == null)
		//                {
		//                    return false;
		//                }

		//                // Parse invoice JSON
		//                var invoiceData = JsonConvert.DeserializeObject<InvoiceJsonPayload>(invoiceRecord.JsonData);
		//                if (invoiceData?.Patient == null || invoiceData.Services == null)
		//                {
		//                    return false;
		//                }

		//                // Generate HTML and PDF
		//                var htmlContent = GenerateInvoiceHtml(invoiceData);

		//                using var page = await browser.NewPageAsync();
		//                await page.SetContentAsync(htmlContent);

		//                var pdfOptions = new PdfOptions
		//                {
		//                    Format = PaperFormat.A4,
		//                    PrintBackground = true,
		//                    MarginOptions = new MarginOptions
		//                    {
		//                        Top = "0.5in",
		//                        Bottom = "0.5in",
		//                        Left = "0.5in",
		//                        Right = "0.5in"
		//                    }
		//                };

		//                var pdfBytes = await page.PdfDataAsync(pdfOptions);
		//                if (pdfBytes == null || pdfBytes.Length == 0)
		//                {
		//                    return false;
		//                }

		//                // Upload to S3
		//                var tempFileName = $"invoice_clinic{invoiceRecord.ClinicId}_patient{invoiceRecord.PatientId}_{Guid.NewGuid()}.pdf";
		//                var tempFilePath = Path.Combine(Path.GetTempPath(), tempFileName);
		//                await System.IO.File.WriteAllBytesAsync(tempFilePath, pdfBytes);

		//                var s3Key = $"clinic/{tempFileName}";
		//                var s3Url = await _s3StorageService.UploadFileToS3(tempFilePath, s3Key);

		//                if (string.IsNullOrEmpty(s3Url))
		//                {
		//                    System.IO.File.Delete(tempFilePath);
		//                    return false;
		//                }

		//                // Update invoice record to mark as sent
		//                invoiceRecord.SendToPatient = true;
		//                await _clinicPatientRecordRepository.UpdateAsync(invoiceRecord);

		//                // Create user report
		//                var reportName = $"Arthrose_Invoice_{invoiceData.Patient.Name.Replace(" ", "_")}_{visit.AppointmentDate:dd-MM-yy}";
		//                var epochTime = new DateTimeOffset(visit.AppointmentDate.Date + visit.AppointmentTime).ToUnixTimeSeconds();
		//                var fileSizeKb = Math.Round((decimal)pdfBytes.Length / 1024, 2);

		//                var userReport = new UserReport
		//                {
		//                    UserId = user.Id,
		//                    ReportName = reportName,
		//                    ReportCategory = 8, // Invoice category
		//                    ReportUrl = s3Url,
		//                    EpochTime = epochTime,
		//                    FileSize = fileSizeKb,
		//                    UploadedBy = "Clinic",
		//                    ClinicId = 8,
		//                    UserType = "Independent",
		//                    DeletedBy = 0
		//                };

		//                await _userRepository.SaveAsync(userReport);

		//                // Create another clinic patient record with the PDF URL
		//                var pdfRecord = new ClinicPatientRecord
		//                {
		//                    ClinicId = CLINIC_ID,
		//                    PatientId = invoiceRecord.PatientId,
		//                    ClinicVisitId = invoiceRecord.ClinicVisitId,
		//                    Type = RecordType.Invoice,
		//                    JsonData = JsonConvert.SerializeObject(new { url = s3Url }),
		//                    SendToPatient = true
		//                };

		//                await _clinicPatientRecordRepository.SaveAsync(pdfRecord);

		//                // Clean up
		//                System.IO.File.Delete(tempFilePath);

		//                // Add to successful records
		//                response.SuccessfulRecords.Add(new InvoicePdfSuccess
		//                {
		//                    RecordId = invoiceRecord.Id,
		//                    PatientName = invoiceData.Patient.Name,
		//                    HFID = clinicPatient.HFID,
		//                    AppointmentDate = visit.AppointmentDate.ToString("dd-MM-yyyy"),
		//                    InvoiceUrl = s3Url,
		//                    FileSizeKB = fileSizeKb
		//                });

		//                return true;
		//            }
		//            catch (Exception ex)
		//            {
		//                _logger.LogError(ex, "Error processing invoice PDF for record {RecordId}", invoiceRecord.Id);
		//                return false;
		//            }
		//        }

		//        private async Task<ClinicVisit?> GetExistingVisitByDate(int clinicPatientId, DateTime appointmentDate)
		//        {
		//            return await _clinicVisitRepository.GetExistingVisitAsync(clinicPatientId, appointmentDate);
		//        }

		//        private string GenerateInvoiceHtml(InvoiceJsonPayload data)
		//        {
		//            var serviceTableRows = new StringBuilder();

		//            for (int i = 0; i < data.Services.Count; i++)
		//            {
		//                var service = data.Services[i];
		//                serviceTableRows.AppendLine($@"
		//            <tr>
		//                <td>{i + 1}</td>
		//                <td>{service.Name}</td>
		//                <td>{service.QtyPerDay}</td>
		//                <td class=""right"">{service.Cost:0.00}</td>
		//                <td class=""right"">{service.Total:0.00}</td>
		//            </tr>");
		//            }

		//            var formattedDob = !string.IsNullOrEmpty(data.Patient.Dob) ? data.Patient.Dob : "Invalid Date";

		//            return $@"
		//<!DOCTYPE html>
		//<html lang=""en"">
		//  <head>
		//    <meta charset=""UTF-8"" />
		//    <style>
		//      body {{
		//        font-family: Arial, Helvetica, sans-serif;
		//        margin: 40px;
		//        color: #333;
		//        background-color: #fff;
		//      }}
		//      .header {{
		//        text-align: center;
		//        margin-bottom: 20px;
		//      }}
		//      .header img {{
		//        max-height: 130px;
		//        width: auto;
		//      }}
		//      .patient-info {{
		//        border: 1px solid #e5e7eb;
		//        border-radius: 8px;
		//        padding: 16px;
		//        margin-bottom: 24px;
		//        font-size: 13px;
		//        background-color: #fafafa;
		//      }}
		//      .patient-info table {{
		//        width: 100%;
		//        border-collapse: collapse;
		//      }}
		//      .patient-info td {{
		//        padding: 4px 8px;
		//        vertical-align: top;
		//      }}
		//      .section-title {{
		//        font-weight: bold;
		//        margin-bottom: 8px;
		//        font-size: 14px;
		//        color: #333;
		//      }}
		//      table.invoice {{
		//        width: 100%;
		//        border-collapse: collapse;
		//        margin-top: 8px;
		//        font-size: 13px;
		//        border: 1px solid #000;
		//      }}
		//      table.invoice th,
		//      table.invoice td {{
		//        border: 1px solid #000;
		//        padding: 10px;
		//        text-align: left;
		//      }}
		//      table.invoice th {{
		//        background: #f9f9f9;
		//        font-weight: 600;
		//        color: #333;
		//      }}
		//      table.invoice td.right {{
		//        text-align: right;
		//      }}
		//      .totals {{
		//        font-weight: bold;
		//        background-color: #f9f9f9;
		//      }}
		//      .totals td {{
		//        border-top: 2px solid #000;
		//      }}
		//      .signature {{
		//        margin-top: 40px;
		//        text-align: right;
		//        font-size: 14px;
		//        color: #333;
		//      }}
		//      .signature .line {{
		//        margin-bottom: 2px;
		//        border-top: 1px solid #333;
		//        width: 116px;
		//        margin-left: auto;
		//      }}
		//      .signature p {{
		//        font-family: ""Cedarville Cursive"", cursive;
		//        font-size: 15px;
		//        color: #1a3c6e;
		//        margin: 0;
		//        padding-right: 0px;
		//      }}
		//      footer {{
		//        margin-top: 40px;
		//        font-size: 15px;
		//        display: flex;
		//        justify-content: space-between;
		//        color: #000;
		//      }}
		//    </style>
		//  </head>
		//  <body>
		//    <!-- Header with Logo -->
		//    <div class=""header"">
		//      <img src=""https://d7cop3y0lcg80.cloudfront.net/reports/1/0604d10d087f97b877ea0ae85e9494b5df28b6e7_26-09-2025_10-12-19.png"" alt=""ARTHROSE CRANIOFACIAL PAIN & TMJ CENTRE Logo"" />
		//    </div>

		//    <!-- Patient Info -->
		//    <div class=""patient-info"">
		//      <table>
		//        <tr>
		//          <td><strong>Patient Name:</strong> {data.Patient.Name}</td>
		//          <td><strong>HFID:</strong> {data.Patient.Hfid}</td>
		//        </tr>
		//        <tr>
		//          <td><strong>Gender:</strong> {data.Patient.Gender}</td>
		//          <td><strong>INVID:</strong> {data.Patient.Invid}</td>
		//        </tr>
		//        <tr>
		//          <td><strong>DOB:</strong> {formattedDob}</td>
		//          <td><strong>Mobile:</strong> {data.Patient.Mobile}</td>
		//        </tr>
		//        <tr>
		//          <td><strong>Consultant Coach:</strong> {DOCTOR_NAME}</td>
		//          <td><strong>City:</strong> {data.Patient.City}</td>
		//        </tr>
		//      </table>
		//    </div>

		//    <!-- Invoice -->
		//    <div class=""section-title"">Invoice</div>
		//    <table class=""invoice"">
		//      <thead>
		//        <tr>
		//          <th>S.No.</th>
		//          <th>Service/Product</th>
		//          <th>Qty/Day</th>
		//          <th>Cost (₹)</th>
		//          <th>Total (₹)</th>
		//        </tr>
		//      </thead>
		//      <tbody>
		//        {serviceTableRows}
		//        <tr class=""totals"">
		//          <td colspan=""4"" class=""right"">Total Cost:</td>
		//          <td class=""right"">₹{data.TotalCost:0.00}</td>
		//        </tr>
		//        <tr class=""totals"">
		//          <td colspan=""4"" class=""right"">Grand Total:</td>
		//          <td class=""right"">₹{data.GrandTotal:0.00}</td>
		//        </tr>
		//        <tr class=""totals"">
		//          <td colspan=""4"" class=""right"">paid</td>
		//          <td class=""right"">₹{data.Paid:0.00}</td>
		//        </tr>
		//        <tr class=""totals"">
		//          <td colspan=""4"" class=""right"">Balance</td>
		//          <td class=""right"">₹{(data.GrandTotal - data.Paid):0.00}</td>
		//        </tr>
		//      </tbody>
		//    </table>

		//    <!-- Signature -->
		//    <div class=""signature"">
		//      <p>{DOCTOR_NAME}</p>
		//      <div class=""line"">{DOCTOR_NAME}</div>
		//    </div>

		//    <footer>
		//      <span>www.arthrosetmjindia.com</span>
		//      <span>www.hfiles.in</span>
		//    </footer>
		//  </body>
		//</html>";
		//        }




		//        // API 1: Import receipts from Excel/CSV
		//        [HttpPost("import-receipts")]
		//        //[Authorize]
		//        public async Task<IActionResult> ImportReceiptsFromExcel([FromForm] ReceiptImportRequest request)
		//        {
		//            HttpContext.Items["Log-Category"] = "Receipt Import";

		//            if (request.ExcelFile == null || request.ExcelFile.Length == 0)
		//                return BadRequest(ApiResponseFactory.Fail("Excel file is required."));

		//            if (!Path.GetExtension(request.ExcelFile.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase) &&
		//                !Path.GetExtension(request.ExcelFile.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
		//                return BadRequest(ApiResponseFactory.Fail("Only .csv and .xlsx files are supported."));

		//            var response = new ReceiptImportResponse();
		//            var transaction = await _clinicRepository.BeginTransactionAsync();
		//            var committed = false;

		//            try
		//            {
		//                List<ExcelReceiptRow> receiptRows;

		//                if (Path.GetExtension(request.ExcelFile.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
		//                {
		//                    receiptRows = await ProcessReceiptCsvFile(request.ExcelFile);
		//                }
		//                else
		//                {
		//                    receiptRows = await ProcessReceiptExcelFile(request.ExcelFile);
		//                }

		//                if (!receiptRows.Any())
		//                {
		//                    return BadRequest(ApiResponseFactory.Fail("No valid receipt data found in the file."));
		//                }

		//                // Group receipts by patient and date
		//                var groupedReceipts = receiptRows
		//                    .GroupBy(r => new { r.PatientId, r.ParsedDate.Date, r.ReceiptId })
		//                    .ToList();

		//                foreach (var group in groupedReceipts)
		//                {
		//                    response.TotalProcessed++;

		//                    try
		//                    {
		//                        var success = await ProcessReceiptGroup(
		//                            group.Key.PatientId,
		//                            group.Key.Date,
		//                            group.Key.ReceiptId,
		//                            group.ToList(),
		//                            response);

		//                        if (success)
		//                        {
		//                            response.Successful++;
		//                        }
		//                        else
		//                        {
		//                            response.Failed++;
		//                        }
		//                    }
		//                    catch (Exception ex)
		//                    {
		//                        _logger.LogError(ex, "Error processing receipt group for Patient {PatientId} on {Date} Receipt {ReceiptId}",
		//                            group.Key.PatientId, group.Key.Date, group.Key.ReceiptId);
		//                        response.Failed++;
		//                        response.SkippedReasons.Add($"Patient {group.Key.PatientId} on {group.Key.Date:dd-MM-yyyy} Receipt {group.Key.ReceiptId}: Processing error - {ex.Message}");
		//                    }
		//                }

		//                await transaction.CommitAsync();
		//                committed = true;

		//                response.Message = $"Receipt import completed: {response.Successful} successful, " +
		//                                  $"{response.Failed} failed out of {response.TotalProcessed} total receipt groups. " +
		//                                  $"Processed {response.PatientsProcessed} patients with {response.VisitsCreated} visits.";

		//                _logger.LogInformation("Receipt import completed: {Added} added, {Failed} failed",
		//                    response.Successful, response.Failed);

		//                return Ok(ApiResponseFactory.Success(response, response.Message));
		//            }
		//            catch (Exception ex)
		//            {
		//                _logger.LogError(ex, "Failed to import receipts from Excel");
		//                return StatusCode(500, ApiResponseFactory.Fail("Failed to process Excel file: " + ex.Message));
		//            }
		//            finally
		//            {
		//                if (!committed && transaction.GetDbTransaction().Connection != null)
		//                    await transaction.RollbackAsync();
		//            }
		//        }

		//        // API 2: Generate receipt PDFs for all unsent receipts
		//        [HttpPost("generate-receipt-pdfs")]
		//        //[Authorize]
		//        public async Task<IActionResult> GenerateReceiptPdfs([FromBody] ReceiptPdfRequest request)
		//        {
		//            HttpContext.Items["Log-Category"] = "Receipt PDF Generation";

		//            var response = new ReceiptPdfResponse();
		//            var transaction = await _clinicRepository.BeginTransactionAsync();
		//            var committed = false;

		//            try
		//            {
		//                // Get all unsent receipt records
		//                var receiptRecords = await GetUnsentReceiptRecords();

		//                if (!receiptRecords.Any())
		//                {
		//                    return Ok(ApiResponseFactory.Success(new ReceiptPdfResponse
		//                    {
		//                        TotalProcessed = 0,
		//                        Successful = 0,
		//                        Failed = 0,
		//                        Message = "No unsent receipt records found."
		//                    }, "No unsent receipt records found."));
		//                }

		//                // Download Chromium once for all PDFs
		//                await new BrowserFetcher().DownloadAsync();

		//                using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
		//                {
		//                    Headless = true,
		//                    Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
		//                });

		//                foreach (var receiptRecord in receiptRecords)
		//                {
		//                    response.TotalProcessed++;

		//                    try
		//                    {
		//                        var success = await ProcessSingleReceiptPdf(receiptRecord, browser, response);
		//                        if (success)
		//                        {
		//                            response.Successful++;
		//                        }
		//                        else
		//                        {
		//                            response.Failed++;
		//                            response.FailedRecords.Add(new ReceiptPdfFailed
		//                            {
		//                                RecordId = receiptRecord.Id,
		//                                Reason = "PDF generation or upload failed"
		//                            });
		//                        }
		//                    }
		//                    catch (Exception ex)
		//                    {
		//                        _logger.LogError(ex, "Error generating PDF for receipt record {RecordId}", receiptRecord.Id);
		//                        response.Failed++;
		//                        response.FailedRecords.Add(new ReceiptPdfFailed
		//                        {
		//                            RecordId = receiptRecord.Id,
		//                            Reason = $"Processing error: {ex.Message}"
		//                        });
		//                    }
		//                }

		//                await transaction.CommitAsync();
		//                committed = true;

		//                response.Message = $"Receipt PDF generation completed: {response.Successful} successful, " +
		//                                  $"{response.Failed} failed out of {response.TotalProcessed} total receipts.";

		//                return Ok(ApiResponseFactory.Success(response, response.Message));
		//            }
		//            catch (Exception ex)
		//            {
		//                _logger.LogError(ex, "Error during receipt PDF generation");
		//                return StatusCode(500, ApiResponseFactory.Fail("An error occurred during PDF generation."));
		//            }
		//            finally
		//            {
		//                if (!committed && transaction.GetDbTransaction().Connection != null)
		//                    await transaction.RollbackAsync();
		//            }
		//        }

		//        // Helper methods for receipt processing
		//        private async Task<List<ExcelReceiptRow>> ProcessReceiptCsvFile(IFormFile file)
		//        {
		//            var receipts = new List<ExcelReceiptRow>();

		//            using var reader = new StreamReader(file.OpenReadStream());
		//            var csvContent = await reader.ReadToEndAsync();
		//            var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

		//            for (int i = 1; i < lines.Length; i++) // Skip header
		//            {
		//                var columns = lines[i].Split(',');
		//                if (columns.Length >= 7)
		//                {
		//                    var receipt = new ExcelReceiptRow
		//                    {
		//                        PatientName = columns[0].Trim(),
		//                        PatientId = columns[1].Trim(),
		//                        DateString = columns[2].Trim(),
		//                        ReceiptId = columns[3].Trim(),
		//                        InvoiceId = columns[4].Trim(),
		//                        ModeOfPayment = columns[5].Trim(),
		//                        AmountPaid = int.TryParse(columns[6].Trim(), out var amountPaid) ? amountPaid : 0
		//                    };

		//                    if (DateTime.TryParseExact(receipt.DateString, "dd-MM-yyyy", null, DateTimeStyles.None, out var parsedDate))
		//                    {
		//                        receipt.ParsedDate = parsedDate;
		//                        receipts.Add(receipt);
		//                    }
		//                }
		//            }

		//            return receipts;
		//        }

		//        private async Task<List<ExcelReceiptRow>> ProcessReceiptExcelFile(IFormFile file)
		//        {
		//            var receipts = new List<ExcelReceiptRow>();

		//            using var stream = new MemoryStream();
		//            await file.CopyToAsync(stream);

		//            ExcelPackage.License.SetNonCommercialPersonal("HFiles");
		//            using var package = new ExcelPackage(stream);
		//            var worksheet = package.Workbook.Worksheets[0];

		//            if (worksheet.Dimension == null) return receipts;

		//            var rowCount = worksheet.Dimension.End.Row;

		//            for (int row = 2; row <= rowCount; row++)
		//            {
		//                var receipt = new ExcelReceiptRow
		//                {
		//                    PatientName = worksheet.Cells[row, 1].Text.Trim(),
		//                    PatientId = worksheet.Cells[row, 2].Text.Trim(),
		//                    DateString = worksheet.Cells[row, 3].Text.Trim(),
		//                    ReceiptId = worksheet.Cells[row, 4].Text.Trim(),
		//                    InvoiceId = worksheet.Cells[row, 5].Text.Trim(),
		//                    ModeOfPayment = worksheet.Cells[row, 6].Text.Trim(),
		//                    AmountPaid = int.TryParse(worksheet.Cells[row, 7].Text.Trim(), out var amountPaid) ? amountPaid : 0
		//                };

		//                if (DateTime.TryParseExact(receipt.DateString, "dd-MM-yyyy", null, DateTimeStyles.None, out var parsedDate))
		//                {
		//                    receipt.ParsedDate = parsedDate;
		//                    receipts.Add(receipt);
		//                }
		//            }

		//            return receipts;
		//        }

		//        private async Task<bool> ProcessReceiptGroup(string patientId, DateTime appointmentDate,
		//            string receiptId, List<ExcelReceiptRow> receipts, ReceiptImportResponse response)
		//        {
		//            // 1. Find user by patientId
		//            var user = await _userRepository.GetUserByPatientIdAsync(patientId);
		//            if (user == null)
		//            {
		//                response.SkippedReasons.Add($"Patient {patientId}: User not found in database");
		//                return false;
		//            }

		//            // 2. Get or create clinic patient
		//            var fullName = $"{user.FirstName} {user.LastName}".Trim();
		//            var clinicPatient = await _clinicVisitRepository.GetOrCreatePatientAsync(user.HfId ?? "", fullName);

		//            // 3. Check if visit exists for this date
		//            var existingVisit = await GetExistingVisitByDate(clinicPatient.Id, appointmentDate);
		//            ClinicVisit visit;

		//            if (existingVisit == null)
		//            {
		//                // Parse payment method from receipt data
		//                var firstReceipt = receipts.First();
		//                var paymentMethod = ParsePaymentMethod(firstReceipt.ModeOfPayment);

		//                // Create new visit
		//                visit = new ClinicVisit
		//                {
		//                    ClinicPatientId = clinicPatient.Id,
		//                    ClinicId = CLINIC_ID,
		//                    AppointmentDate = appointmentDate.Date,
		//                    AppointmentTime = APPOINTMENT_TIME,
		//                    PaymentMethod = paymentMethod
		//                };

		//                await _clinicVisitRepository.SaveVisitAsync(visit);
		//                response.VisitsCreated++;
		//            }
		//            else
		//            {
		//                visit = existingVisit;

		//                // Always update payment method from receipt data
		//                var firstReceipt = receipts.First();
		//                var parsedPaymentMethod = ParsePaymentMethod(firstReceipt.ModeOfPayment);

		//                // Update if payment method is different or not set
		//                if (visit.PaymentMethod != parsedPaymentMethod)
		//                {
		//                    visit.PaymentMethod = parsedPaymentMethod;
		//                    await _clinicVisitRepository.UpdateAsync(visit);
		//                }
		//            }

		//            // 4. Create receipt JSON data
		//            var receiptJsonData = CreateReceiptJsonData(user, clinicPatient, receipts, receiptId, appointmentDate);

		//            // 5. Check if receipt record already exists
		//            var existingRecord = await _clinicPatientRecordRepository.GetByCompositeKeyAsync(
		//                CLINIC_ID, clinicPatient.Id, visit.Id, RecordType.Receipt);

		//            if (existingRecord != null)
		//            {
		//                response.SkippedReasons.Add($"Patient {patientId} on {appointmentDate:dd-MM-yyyy} Receipt {receiptId}: Receipt record already exists");
		//                return false;
		//            }

		//            // 6. Create receipt record
		//            var receiptRecord = new ClinicPatientRecord
		//            {
		//                ClinicId = CLINIC_ID,
		//                PatientId = clinicPatient.Id,
		//                ClinicVisitId = visit.Id,
		//                Type = RecordType.Receipt,
		//                JsonData = receiptJsonData,
		//                SendToPatient = false
		//            };

		//            await _clinicPatientRecordRepository.SaveAsync(receiptRecord);

		//            response.PatientsProcessed++;
		//            response.AddedReceipts.Add(new AddedReceiptSummary
		//            {
		//                PatientId = patientId,
		//                PatientName = fullName,
		//                HFID = user.HfId ?? "",
		//                AppointmentDate = appointmentDate.ToString("dd-MM-yyyy"),
		//                ReceiptId = receiptId,
		//                InvoiceId = receipts.First().InvoiceId,
		//                ModeOfPayment = receipts.First().ModeOfPayment,
		//                AmountPaid = receipts.Sum(r => r.AmountPaid)
		//            });

		//            return true;
		//        }

		//        private PaymentMethod? ParsePaymentMethod(string modeOfPayment)
		//        {
		//            if (string.IsNullOrWhiteSpace(modeOfPayment))
		//                return null;

		//            return modeOfPayment.ToUpper().Trim() switch
		//            {
		//                "CARD" => PaymentMethod.DebitCard,
		//                "DEBIT CARD" => PaymentMethod.DebitCard,
		//                "CREDIT CARD" => PaymentMethod.CreditCard,
		//                "CASH" => PaymentMethod.Cash,
		//                "WALLET" => PaymentMethod.Wallet,
		//                "BANK TRANSFER" => PaymentMethod.BankTransfer,
		//                "CHECK" => PaymentMethod.Check,
		//                "CHEQUE" => PaymentMethod.Check,
		//                _ => null // Unknown payment method, store as null
		//            };
		//        }

		//        // FIXED: Date formatting in CreateReceiptJsonData
		//        private string CreateReceiptJsonData(Domain.Entities.Users.User user, ClinicPatient clinicPatient,
		//            List<ExcelReceiptRow> receipts, string receiptId, DateTime appointmentDate)
		//        {
		//            var firstReceipt = receipts.First();
		//            var totalAmount = receipts.Sum(r => r.AmountPaid);

		//            // Convert payment mode
		//            var modeOfPayment = firstReceipt.ModeOfPayment.ToUpper() switch
		//            {
		//                "CARD" => "Debit Card",
		//                "CASH" => "Cash",
		//                "WALLET" => "Wallet",
		//                _ => firstReceipt.ModeOfPayment
		//            };

		//            var serviceItems = receipts.Select(r => new
		//            {
		//                name = "Service",
		//                qtyPerDay = "1 QTY",
		//                cost = r.AmountPaid,
		//                total = r.AmountPaid,
		//                ModeOfPayment = modeOfPayment,
		//                ChequeNo = "--"
		//            }).ToArray();

		//            var receiptData = new
		//            {
		//                patient = new
		//                {
		//                    name = $"{user.FirstName} {user.LastName}".Trim(),
		//                    uhid = user.HfId ?? "",
		//                    gender = user.Gender ?? "",
		//                    receiptId = receiptId,
		//                    dob = user.DOB ?? "",
		//                    doctor = DOCTOR_NAME,
		//                    mobile = user.PhoneNumber ?? "",
		//                    city = user.City ?? ""
		//                },
		//                receipt = new
		//                {
		//                    // FIXED: Changed from appointmentDate.ToString("MMM") to dd/MM/yyyy format
		//                    date = appointmentDate.ToString("dd/MM/yyyy"),
		//                    receiptNumber = receiptId,
		//                    modeOfPayment = modeOfPayment,
		//                    chequeNo = "--",
		//                    amountPaid = totalAmount,
		//                    amountInWords = ConvertAmountToWords(totalAmount)
		//                },
		//                services = serviceItems,
		//                clinicInfo = new
		//                {
		//                    name = "Arthrose",
		//                    subtitle = "CRANIOFACIAL PAIN & TMJ CENTRE",
		//                    website = "www.arthrosetmjindia.com"
		//                }
		//            };

		//            return JsonConvert.SerializeObject(receiptData, Formatting.None);
		//        }

		//        private async Task<ClinicVisit?> GetExistingVisitByDate(int clinicPatientId, DateTime appointmentDate)
		//        {
		//            return await _clinicVisitRepository.GetExistingVisitAsync(clinicPatientId, appointmentDate);
		//        }

		//        private string ConvertAmountToWords(int amount)
		//        {
		//            // Simple implementation - you can enhance this with a proper number-to-words converter
		//            return $"{amount} Only";
		//        }

		//        private async Task<List<ClinicPatientRecord>> GetUnsentReceiptRecords()
		//        {
		//            return await _clinicPatientRecordRepository.GetUnsentReceiptRecordsAsync(CLINIC_ID);
		//        }

		//        private async Task<bool> ProcessSingleReceiptPdf(ClinicPatientRecord receiptRecord,
		//            IBrowser browser, ReceiptPdfResponse response)
		//        {
		//            try
		//            {
		//                // Get related data
		//                var visit = await _clinicVisitRepository.GetByIdAsync(receiptRecord.ClinicVisitId);
		//                var clinicPatient = await _clinicPatientRecordRepository.GetByIdAsync(receiptRecord.PatientId);
		//                var user = clinicPatient != null ? await _userRepository.GetUserByHFIDAsync(clinicPatient.HFID) : null;

		//                if (visit == null || clinicPatient == null || user == null)
		//                {
		//                    return false;
		//                }

		//                // Parse receipt JSON
		//                var receiptData = JsonConvert.DeserializeObject<ReceiptJsonPayload>(receiptRecord.JsonData);
		//                if (receiptData?.Patient == null || receiptData.Receipt == null)
		//                {
		//                    return false;
		//                }

		//                // Generate HTML and PDF
		//                var htmlContent = GenerateReceiptHtml(receiptData);

		//                using var page = await browser.NewPageAsync();
		//                await page.SetContentAsync(htmlContent);

		//                var pdfOptions = new PdfOptions
		//                {
		//                    Format = PaperFormat.A4,
		//                    PrintBackground = true,
		//                    MarginOptions = new MarginOptions
		//                    {
		//                        Top = "0.5in",
		//                        Bottom = "0.5in",
		//                        Left = "0.5in",
		//                        Right = "0.5in"
		//                    }
		//                };

		//                var pdfBytes = await page.PdfDataAsync(pdfOptions);
		//                if (pdfBytes == null || pdfBytes.Length == 0)
		//                {
		//                    return false;
		//                }

		//                // Upload to S3
		//                var tempFileName = $"receipt_clinic{receiptRecord.ClinicId}_patient{receiptRecord.PatientId}_{Guid.NewGuid()}.pdf";
		//                var tempFilePath = Path.Combine(Path.GetTempPath(), tempFileName);
		//                await System.IO.File.WriteAllBytesAsync(tempFilePath, pdfBytes);

		//                var s3Key = $"clinic/{tempFileName}";
		//                var s3Url = await _s3StorageService.UploadFileToS3(tempFilePath, s3Key);

		//                if (string.IsNullOrEmpty(s3Url))
		//                {
		//                    System.IO.File.Delete(tempFilePath);
		//                    return false;
		//                }

		//                // Update receipt record to mark as sent
		//                receiptRecord.SendToPatient = true;
		//                await _clinicPatientRecordRepository.UpdateAsync(receiptRecord);

		//                // Create user report
		//                var reportName = $"Arthrose_Receipt_{receiptData.Patient.Name.Replace(" ", "_")}_{visit.AppointmentDate:dd-MM-yy}";
		//                var epochTime = new DateTimeOffset(visit.AppointmentDate.Date + visit.AppointmentTime).ToUnixTimeSeconds();
		//                var fileSizeKb = Math.Round((decimal)pdfBytes.Length / 1024, 2);

		//                var userReport = new UserReport
		//                {
		//                    UserId = user.Id,
		//                    ReportName = reportName,
		//                    ReportCategory = 8, // Receipt category
		//                    ReportUrl = s3Url,
		//                    EpochTime = epochTime,
		//                    FileSize = fileSizeKb,
		//                    UploadedBy = "Clinic",
		//                    ClinicId = 8,
		//                    UserType = "Independent",
		//                    DeletedBy = 0
		//                };

		//                await _userRepository.SaveAsync(userReport);

		//                // Create another clinic patient record with the PDF URL (similar to prescription pattern)
		//                var pdfRecord = new ClinicPatientRecord
		//                {
		//                    ClinicId = CLINIC_ID,
		//                    PatientId = receiptRecord.PatientId,
		//                    ClinicVisitId = receiptRecord.ClinicVisitId,
		//                    Type = RecordType.Receipt,
		//                    JsonData = JsonConvert.SerializeObject(new { url = s3Url }),
		//                    SendToPatient = true
		//                };

		//                await _clinicPatientRecordRepository.SaveAsync(pdfRecord);

		//                // Clean up
		//                System.IO.File.Delete(tempFilePath);

		//                // Add to successful records
		//                response.SuccessfulRecords.Add(new ReceiptPdfSuccess
		//                {
		//                    RecordId = receiptRecord.Id,
		//                    PatientName = receiptData.Patient.Name,
		//                    HFID = clinicPatient.HFID,
		//                    AppointmentDate = visit.AppointmentDate.ToString("dd-MM-yyyy"),
		//                    ReceiptUrl = s3Url,
		//                    FileSizeKB = fileSizeKb
		//                });

		//                return true;
		//            }
		//            catch (Exception ex)
		//            {
		//                _logger.LogError(ex, "Error processing receipt PDF for record {RecordId}", receiptRecord.Id);
		//                return false;
		//            }
		//        }

		//        private string GenerateReceiptHtml(ReceiptJsonPayload data)
		//        {
		//            var formattedDob = !string.IsNullOrEmpty(data.Patient.Dob) ? data.Patient.Dob : "Invalid Date";

		//            return $@"
		//<!DOCTYPE html>
		//<html lang=""en"">
		//  <head>
		//    <meta charset=""UTF-8"" />
		//    <style>
		//      body {{
		//        font-family: Arial, Helvetica, sans-serif;
		//        margin: 40px;
		//        color: #333;
		//        background-color: #fff;
		//      }}
		//      .header {{
		//        text-align: center;
		//        margin-bottom: 20px;
		//      }}
		//      .header img {{
		//        max-height: 130px;
		//        width: auto;
		//      }}
		//      .patient-info {{
		//        border: 1px solid #e5e7eb;
		//        border-radius: 8px;
		//        padding: 16px;
		//        margin-bottom: 24px;
		//        font-size: 13px;
		//        background-color: #fafafa;
		//      }}
		//      .patient-info table {{
		//        width: 100%;
		//        border-collapse: collapse;
		//      }}
		//      .patient-info td {{
		//        padding: 4px 8px;
		//        vertical-align: top;
		//      }}
		//      .section-title {{
		//        font-weight: bold;
		//        margin-bottom: 8px;
		//        font-size: 14px;
		//        color: #333;
		//      }}
		//      table.receipt {{
		//        width: 100%;
		//        border-collapse: collapse;
		//        margin-top: 8px;
		//        font-size: 13px;
		//        border: 1px solid #000;
		//      }}
		//      table.receipt th,
		//      table.receipt td {{
		//        border: 1px solid #000;
		//        padding: 10px;
		//        text-align: left;
		//      }}
		//      table.receipt th {{
		//        background: #f9f9f9;
		//        font-weight: 600;
		//        color: #333;
		//      }}
		//      table.receipt td.right {{
		//        text-align: right;
		//      }}
		//      table.receipt td.center {{
		//        text-align: center;
		//      }}
		//      .amount-paid {{
		//        color: #28a745;
		//        font-weight: bold;
		//      }}
		//      .receipt-details {{
		//        margin-top: 20px;
		//        border: 1px solid #28a745;
		//        border-left: 4px solid #28a745;
		//        padding: 15px;
		//        background-color: #f8f9fa;
		//        font-size: 14px;
		//      }}
		//      .receipt-details .thanks {{
		//        margin-bottom: 10px;
		//      }}
		//      .receipt-details .amount {{
		//        font-weight: bold;
		//      }}
		//      .signature {{
		//        margin-top: 40px;
		//        text-align: right;
		//        font-size: 14px;
		//        color: #333;
		//      }}
		//      .signature .line {{
		//        margin-bottom: 2px;
		//        border-top: 1px solid #333;
		//        width: 116px;
		//        margin-left: auto;
		//      }}
		//      .signature p {{
		//        font-family: ""Cedarville Cursive"", cursive;
		//        font-size: 15px;
		//        color: #1a3c6e;
		//        margin: 0;
		//        padding-right: 0px;
		//      }}
		//      footer {{
		//        margin-top: 40px;
		//        font-size: 15px;
		//        display: flex;
		//        justify-content: space-between;
		//        color: #000;
		//      }}
		//    </style>
		//  </head>
		//  <body>
		//    <!-- Header with Logo -->
		//    <div class=""header"">
		//      <img src=""https://d7cop3y0lcg80.cloudfront.net/reports/1/0604d10d087f97b877ea0ae85e9494b5df28b6e7_26-09-2025_10-12-19.png"" alt=""ARTHROSE CRANIOFACIAL PAIN & TMJ CENTRE Logo"" />
		//    </div>

		//    <!-- Patient Info -->
		//    <div class=""patient-info"">
		//      <table>
		//        <tr>
		//          <td><strong>Patient Name:</strong> {data.Patient.Name}</td>
		//          <td><strong>HFID:</strong> {data.Patient.Uhid}</td>
		//        </tr>
		//        <tr>
		//          <td><strong>Gender:</strong> {data.Patient.Gender}</td>
		//          <td><strong>RCID:</strong> {data.Patient.ReceiptId}</td>
		//        </tr>
		//        <tr>
		//          <td><strong>DOB:</strong> {formattedDob}</td>
		//          <td><strong>Mobile:</strong> {data.Patient.Mobile}</td>
		//        </tr>
		//        <tr>
		//          <td><strong>Consultant Coach:</strong> {data.Patient.Doctor}</td>
		//          <td><strong>City:</strong> {data.Patient.City}</td>
		//        </tr>
		//      </table>
		//    </div>

		//    <!-- Receipt -->
		//    <div class=""section-title"">Receipt</div>
		//    <table class=""receipt"">
		//      <thead>
		//        <tr>
		//          <th>Date</th>
		//          <th>Receipt Number</th>
		//          <th>Mode Of Payment</th>
		//          <th>Cheque No.</th>
		//          <th>Amount Paid (₹)</th>
		//        </tr>
		//      </thead>
		//      <tbody>
		//        <tr>
		//          <td>{data.Receipt.Date}</td>
		//          <td>{data.Receipt.ReceiptNumber}</td>
		//          <td>{data.Receipt.ModeOfPayment}</td>
		//          <td class=""center"">{data.Receipt.ChequeNo}</td>
		//          <td class=""right amount-paid"">₹{data.Receipt.AmountPaid:0.00}</td>
		//        </tr>
		//      </tbody>
		//    </table>

		//    <!-- Receipt Details -->
		//    <div class=""receipt-details"">
		//      <div class=""thanks"">
		//        <strong>Received with thanks from:</strong> {data.Patient.Name}
		//      </div>
		//      <div class=""amount"">
		//        <strong>The sum of Rupees:</strong> {data.Receipt.AmountInWords}
		//        <span style=""color: #28a745"">(₹{data.Receipt.AmountPaid:0.00})</span> /-
		//      </div>
		//    </div>

		//    <!-- Signature -->
		//    <div class=""signature"">
		//      <p>{DOCTOR_NAME}</p>
		//      <div class=""line"">{DOCTOR_NAME}</div>
		//    </div>

		//    <footer>
		//      <span>www.arthrosetmjindia.com</span>
		//      <span>www.hfiles.in</span>
		//    </footer>
		//  </body>
		//</html>";
		//        }







		//********************************************************************************************************



		// ===== UPDATE CONSTANTS (Use same as 2025) =====
		//private const int CLINIC_ID = 8; // Same ARTHROSE clinic
		//private const string DOCTOR_NAME = "Dr. Varun R Kunte"; // Same doctor
		//private readonly TimeSpan APPOINTMENT_TIME = new(10, 0, 0); // Same time

		// ===== API 1: Import 2019 treatments from Excel =====    
		[HttpPost("import-treatments-2019")]
		//[Authorize]
		public async Task<IActionResult> ImportTreatments2019FromExcel([FromForm] TreatmentImportRequest request)
		{
			HttpContext.Items["Log-Category"] = "2019 Treatment Import";

			if (request.ExcelFile == null || request.ExcelFile.Length == 0)
				return BadRequest(ApiResponseFactory.Fail("Excel file is required."));

			if (!Path.GetExtension(request.ExcelFile.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase) &&
				!Path.GetExtension(request.ExcelFile.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
				return BadRequest(ApiResponseFactory.Fail("Only .csv and .xlsx files are supported."));

			//bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(CLINIC_ID, User);
			//if (!isAuthorized)
			//{
			//    _logger.LogWarning("Unauthorized treatment import attempt for Clinic ID {ClinicId}", CLINIC_ID);
			//    return Unauthorized(ApiResponseFactory.Fail("You are not authorized to import treatments for this clinic."));
			//}

			var response = new TreatmentImportResponse();
			var transaction = await _clinicRepository.BeginTransactionAsync();
			var committed = false;

			try
			{
				List<ExcelTreatmentRow> treatmentRows;

				if (Path.GetExtension(request.ExcelFile.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
				{
					treatmentRows = await ProcessCsvFile2019(request.ExcelFile);
				}
				else
				{
					treatmentRows = await ProcessExcelFile2019(request.ExcelFile);
				}

				if (!treatmentRows.Any())
				{
					return BadRequest(ApiResponseFactory.Fail("No valid treatment data found in the file."));
				}

				// Group treatments by patient and date
				var groupedTreatments = treatmentRows
					.GroupBy(t => new { t.PatientId, t.ParsedDate.Date })
					.ToList();

				foreach (var group in groupedTreatments)
				{
					response.TotalProcessed++;

					try
					{
						var success = await ProcessTreatmentGroup2019(
							group.Key.PatientId,
							group.Key.Date,
							group.ToList(),
							response);

						if (success)
						{
							response.Successful++;
						}
						else
						{
							response.Failed++;
						}
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Error processing treatment group for Patient {PatientId} on {Date}",
							group.Key.PatientId, group.Key.Date);
						response.Failed++;
						response.SkippedReasons.Add($"Patient {group.Key.PatientId} on {group.Key.Date:dd-MM-yyyy}: Processing error - {ex.Message}");
					}
				}

				await transaction.CommitAsync();
				committed = true;

				response.Message = $"2019 Treatment import completed: {response.Successful} successful, " +
								  $"{response.Failed} failed out of {response.TotalProcessed} total treatment groups. " +
								  $"Processed {response.PatientsProcessed} patients with {response.VisitsCreated} visits.";

				_logger.LogInformation("2019 Treatment import completed: {Added} added, {Failed} failed",
					response.Successful, response.Failed);

				// Invalidate cache
				_cacheService.InvalidateClinicStatistics(CLINIC_ID);

				return Ok(ApiResponseFactory.Success(response, response.Message));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to import 2019 treatments from Excel");
				return StatusCode(500, ApiResponseFactory.Fail("Failed to process Excel file: " + ex.Message));
			}
			finally
			{
				if (!committed && transaction.GetDbTransaction().Connection != null)
					await transaction.RollbackAsync();
			}
		}

		// ===== Helper: Process CSV file for 2019 =====
		private async Task<List<ExcelTreatmentRow>> ProcessCsvFile2019(IFormFile file)
		{
			var treatments = new List<ExcelTreatmentRow>();

			using var reader = new StreamReader(file.OpenReadStream());
			var csvContent = await reader.ReadToEndAsync();
			var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

			for (int i = 1; i < lines.Length; i++) // Skip header
			{
				var columns = lines[i].Split(',');
				if (columns.Length >= 9)
				{
					var treatment = new ExcelTreatmentRow
					{
						PatientName = columns[0].Trim(),
						PatientId = columns[1].Trim(),
						DateString = columns[2].Trim(),
						TreatmentName = columns[3].Trim(),
						Status = columns[4].Trim(),
						Cost = int.TryParse(columns[5].Trim(), out var cost) ? cost : 0,
						Quantity = int.TryParse(columns[6].Trim(), out var qty) ? qty : 1,
						QuantityType = columns[7].Trim(),
						FinalCost = int.TryParse(columns[8].Trim(), out var finalCost) ? finalCost : 0
					};

					// Try multiple date formats
					if (DateTime.TryParseExact(treatment.DateString, "dd-MM-yyyy", null, DateTimeStyles.None, out var parsedDate) ||
						DateTime.TryParseExact(treatment.DateString, "MM/dd/yyyy", null, DateTimeStyles.None, out parsedDate) ||
						DateTime.TryParseExact(treatment.DateString, "yyyy-MM-dd", null, DateTimeStyles.None, out parsedDate))
					{
						treatment.ParsedDate = parsedDate;
						treatments.Add(treatment);
					}
					else
					{
						_logger.LogWarning("Invalid date format for row {Row}: {DateString}", i + 1, treatment.DateString);
					}
				}
			}

			return treatments;
		}

		// ===== Helper: Process Excel file for 2019 =====
		private async Task<List<ExcelTreatmentRow>> ProcessExcelFile2019(IFormFile file)
		{
			var treatments = new List<ExcelTreatmentRow>();

			using var stream = new MemoryStream();
			await file.CopyToAsync(stream);

			ExcelPackage.License.SetNonCommercialPersonal("HFiles");
			using var package = new ExcelPackage(stream);
			var worksheet = package.Workbook.Worksheets[0];

			if (worksheet.Dimension == null) return treatments;

			var rowCount = worksheet.Dimension.End.Row;

			for (int row = 2; row <= rowCount; row++)
			{
				var treatment = new ExcelTreatmentRow
				{
					PatientName = worksheet.Cells[row, 1].Text.Trim(),
					PatientId = worksheet.Cells[row, 2].Text.Trim(),
					DateString = worksheet.Cells[row, 3].Text.Trim(),
					TreatmentName = worksheet.Cells[row, 4].Text.Trim(),
					Status = worksheet.Cells[row, 5].Text.Trim(),
					Cost = int.TryParse(worksheet.Cells[row, 6].Text.Trim(), out var cost) ? cost : 0,
					Quantity = int.TryParse(worksheet.Cells[row, 7].Text.Trim(), out var qty) ? qty : 1,
					QuantityType = worksheet.Cells[row, 8].Text.Trim(),
					FinalCost = int.TryParse(worksheet.Cells[row, 9].Text.Trim(), out var finalCost) ? finalCost : 0
				};

				// Try multiple date formats
				if (DateTime.TryParseExact(treatment.DateString, "dd-MM-yyyy", null, DateTimeStyles.None, out var parsedDate) ||
					DateTime.TryParseExact(treatment.DateString, "MM/dd/yyyy", null, DateTimeStyles.None, out parsedDate) ||
					DateTime.TryParseExact(treatment.DateString, "yyyy-MM-dd", null, DateTimeStyles.None, out parsedDate))
				{
					treatment.ParsedDate = parsedDate;
					treatments.Add(treatment);
				}
				else
				{
					_logger.LogWarning("Invalid date format for row {Row}: {DateString}", row, treatment.DateString);
				}
			}

			return treatments;
		}

		// ===== Helper: Process treatment group for 2019 =====
		private async Task<bool> ProcessTreatmentGroup2019(
			string patientId,
			DateTime appointmentDate,
			List<ExcelTreatmentRow> treatments,
			TreatmentImportResponse response)
		{
			// 1. Find user by patientId
			var user = await _userRepository.GetUserByPatientIdAsync(patientId);
			if (user == null)
			{
				response.SkippedReasons.Add($"Patient {patientId}: User not found in database");
				return false;
			}

			// 2. Get or create clinic patient
			var fullName = $"{user.FirstName} {user.LastName}".Trim();
			var clinicPatient = await _clinicVisitRepository.GetOrCreatePatientAsync(user.HfId ?? "", fullName);

			// 3. Check if visit exists for this date
			var existingVisit = await GetExistingVisitByDate(clinicPatient.Id, appointmentDate);
			ClinicVisit visit;

			if (existingVisit == null)
			{
				// Create new visit
				visit = new ClinicVisit
				{
					ClinicPatientId = clinicPatient.Id,
					ClinicId = CLINIC_ID,
					AppointmentDate = appointmentDate.Date,
					AppointmentTime = APPOINTMENT_TIME,
					PaymentMethod = null
				};

				await _clinicVisitRepository.SaveVisitAsync(visit);
				response.VisitsCreated++;
			}
			else
			{
				visit = existingVisit;
			}

			// 4. Create treatment JSON data (same format as 2025)
			var treatmentJsonData = CreateTreatmentJsonData2019(user, clinicPatient, treatments, patientId);

			// 5. Check if treatment record already exists
			var existingRecord = await _clinicPatientRecordRepository.GetByCompositeKeyAsync(
				CLINIC_ID, clinicPatient.Id, visit.Id, RecordType.Treatment);

			if (existingRecord != null)
			{
				response.SkippedReasons.Add($"Patient {patientId} on {appointmentDate:dd-MM-yyyy}: Treatment record already exists");
				return false;
			}

			// 6. Create treatment record
			var treatmentRecord = new ClinicPatientRecord
			{
				ClinicId = CLINIC_ID,
				PatientId = clinicPatient.Id,
				ClinicVisitId = visit.Id,
				Type = RecordType.Treatment,
				JsonData = treatmentJsonData,
				SendToPatient = false
			};

			await _clinicPatientRecordRepository.SaveAsync(treatmentRecord);

			response.PatientsProcessed++;
			response.AddedTreatments.Add(new AddedTreatmentSummary
			{
				PatientId = patientId,
				PatientName = fullName,
				HFID = user.HfId ?? "",
				AppointmentDate = appointmentDate.ToString("dd-MM-yyyy"),
				TreatmentCount = treatments.Count,
				TotalCost = treatments.Sum(t => t.FinalCost),
				Treatments = treatments.Select(t => t.TreatmentName).ToList()
			});

			return true;
		}

		// ===== Helper: Create treatment JSON data (SAME as 2025) =====
		private string CreateTreatmentJsonData2019(
			Domain.Entities.Users.User user,
			ClinicPatient clinicPatient,
			List<ExcelTreatmentRow> treatments,
			string patientId)
		{
			var treatmentItems = treatments.Select(t => new
			{
				name = t.TreatmentName,
				qtyPerDay = $"{t.Quantity} {t.QuantityType}",
				cost = t.Cost,
				status = t.Status ?? "Not Started",
				total = t.FinalCost
			}).ToArray();

			var totalCost = treatments.Sum(t => t.FinalCost);

			var treatmentData = new
			{
				patient = new
				{
					name = $"{user.FirstName} {user.LastName}".Trim(),
					hfid = user.HfId ?? "",
					gender = user.Gender ?? "",
					tid = patientId,
					dob = user.DOB ?? "",
					mobile = user.PhoneNumber ?? "",
					doctor = DOCTOR_NAME, // Same as 2025
					city = user.City ?? ""
				},
				treatments = treatmentItems,
				totalCost = totalCost,
				grandTotal = totalCost,
				clinicInfo = new
				{
					name = "ARTHROSE", // Same as 2025
					subtitle = "CRANIOFACIAL PAIN & TMJ CENTRE", // Same as 2025
					website = "www.arthrosetmjindia.com" // Same as 2025
				}
			};

			return JsonConvert.SerializeObject(treatmentData, Formatting.None);
		}

		// ===== API 2: Generate treatment PDFs for 2019 =====
		[HttpPost("generate-treatment-pdfs-2019")]
		[Authorize]
		public async Task<IActionResult> GenerateTreatmentPdfs2019([FromBody] TreatmentPdfRequest request)
		{
			HttpContext.Items["Log-Category"] = "2019 Treatment PDF Generation";

			//bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(CLINIC_ID, User);
			//if (!isAuthorized)
			//{
			//    _logger.LogWarning("Unauthorized PDF generation attempt for Clinic ID {ClinicId}", CLINIC_ID);
			//    return Unauthorized(ApiResponseFactory.Fail("You are not authorized to generate PDFs for this clinic."));
			//}

			var response = new TreatmentPdfResponse();
			var transaction = await _clinicRepository.BeginTransactionAsync();
			var committed = false;

			try
			{
				// Get all unsent treatment records for CLINIC_ID = 8
				var treatmentRecords = await _clinicPatientRecordRepository.GetUnsentTreatmentRecordsAsync(CLINIC_ID);

				if (!treatmentRecords.Any())
				{
					return Ok(ApiResponseFactory.Success(new TreatmentPdfResponse
					{
						TotalProcessed = 0,
						Successful = 0,
						Failed = 0,
						Message = "No unsent treatment records found."
					}, "No unsent treatment records found."));
				}

				_logger.LogInformation("Found {Count} unsent treatment records", treatmentRecords.Count);

				// Download Chromium once for all PDFs
				await new BrowserFetcher().DownloadAsync();

				using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
				{
					Headless = true,
					Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
				});

				foreach (var treatmentRecord in treatmentRecords)
				{
					response.TotalProcessed++;

					try
					{
						var success = await ProcessSingleTreatmentPdf2019(treatmentRecord, browser, response);
						if (success)
						{
							response.Successful++;
						}
						else
						{
							response.Failed++;
							response.FailedRecords.Add(new TreatmentPdfFailed
							{
								RecordId = treatmentRecord.Id,
								Reason = "PDF generation or upload failed"
							});
						}
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Error generating PDF for treatment record {RecordId}", treatmentRecord.Id);
						response.Failed++;
						response.FailedRecords.Add(new TreatmentPdfFailed
						{
							RecordId = treatmentRecord.Id,
							Reason = $"Processing error: {ex.Message}"
						});
					}
				}

				await transaction.CommitAsync();
				committed = true;

				response.Message = $"Treatment PDF generation completed: {response.Successful} successful, " +
								  $"{response.Failed} failed out of {response.TotalProcessed} total treatments.";

				_logger.LogInformation("Treatment PDF generation completed. Success: {Success}, Failed: {Failed}",
					response.Successful, response.Failed);

				return Ok(ApiResponseFactory.Success(response, response.Message));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during treatment PDF generation");
				return StatusCode(500, ApiResponseFactory.Fail("An error occurred during PDF generation."));
			}
			finally
			{
				if (!committed && transaction.GetDbTransaction().Connection != null)
					await transaction.RollbackAsync();
			}
		}

		// ===== Helper: Process single treatment PDF (SAME naming as 2025) =====
		private async Task<bool> ProcessSingleTreatmentPdf2019(
			ClinicPatientRecord treatmentRecord,
			IBrowser browser,
			TreatmentPdfResponse response)
		{
			try
			{
				// Get related data
				var visit = await _clinicVisitRepository.GetByIdAsync(treatmentRecord.ClinicVisitId);
				var clinicPatient = await _clinicPatientRecordRepository.GetByIdAsync(treatmentRecord.PatientId);
				var user = clinicPatient != null ? await _userRepository.GetUserByHFIDAsync(clinicPatient.HFID) : null;

				if (visit == null || clinicPatient == null || user == null)
				{
					_logger.LogWarning("Missing related data for treatment record {RecordId}", treatmentRecord.Id);
					return false;
				}

				// Parse treatment JSON
				var treatmentData = JsonConvert.DeserializeObject<TreatmentJsonPayload>(treatmentRecord.JsonData);
				if (treatmentData?.Patient == null || treatmentData.Treatments == null)
				{
					_logger.LogWarning("Invalid treatment JSON data for record {RecordId}", treatmentRecord.Id);
					return false;
				}

				// Generate HTML and PDF
				var htmlContent = GenerateTreatmentHtml2019(treatmentData);

				using var page = await browser.NewPageAsync();
				await page.SetContentAsync(htmlContent);

				var pdfOptions = new PdfOptions
				{
					Format = PaperFormat.A4,
					PrintBackground = true,
					MarginOptions = new MarginOptions
					{
						Top = "0.5in",
						Bottom = "0.5in",
						Left = "0.5in",
						Right = "0.5in"
					}
				};

				var pdfBytes = await page.PdfDataAsync(pdfOptions);
				if (pdfBytes == null || pdfBytes.Length == 0)
				{
					_logger.LogWarning("PDF generation returned empty bytes for record {RecordId}", treatmentRecord.Id);
					return false;
				}

				// Upload to S3 (same path structure as 2025)
				var tempFileName = $"treatment_{treatmentRecord.ClinicId}_{treatmentRecord.PatientId}_{treatmentRecord.ClinicVisitId}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
				var tempFilePath = Path.Combine(Path.GetTempPath(), tempFileName);
				await System.IO.File.WriteAllBytesAsync(tempFilePath, pdfBytes);

				var s3Key = $"clinic/{tempFileName}";
				var s3Url = await _s3StorageService.UploadFileToS3(tempFilePath, s3Key);

				if (string.IsNullOrEmpty(s3Url))
				{
					System.IO.File.Delete(tempFilePath);
					_logger.LogWarning("S3 upload failed for record {RecordId}", treatmentRecord.Id);
					return false;
				}

				// Update record
				treatmentRecord.SendToPatient = true;
				await _clinicPatientRecordRepository.UpdateAsync(treatmentRecord);

				// Create user report (SAME naming as 2025)
				var reportName = $"Arthrose_Treatment_{treatmentData.Patient.Name.Replace(" ", "_")}_{visit.AppointmentDate:dd-MM-yy}";
				var epochTime = new DateTimeOffset(visit.AppointmentDate.Date + visit.AppointmentTime).ToUnixTimeSeconds();
				var fileSizeKb = Math.Round((decimal)pdfBytes.Length / 1024, 2);

				var userReport = new UserReport
				{
					UserId = user.Id,
					ReportName = reportName,
					ReportCategory = 1, // Treatment category
					ReportUrl = s3Url,
					EpochTime = epochTime,
					FileSize = fileSizeKb,
					UploadedBy = "Clinic",
					ClinicId = 8, // Same as 2025
					UserType = "Independent",
					DeletedBy = 0
				};

				await _userRepository.SaveAsync(userReport);

				// Clean up
				System.IO.File.Delete(tempFilePath);

				// Add to successful records
				response.SuccessfulRecords.Add(new TreatmentPdfSuccess
				{
					RecordId = treatmentRecord.Id,
					PatientName = treatmentData.Patient.Name,
					HFID = clinicPatient.HFID,
					AppointmentDate = visit.AppointmentDate.ToString("dd-MM-yyyy"),
					TreatmentUrl = s3Url,
					FileSizeKB = fileSizeKb
				});

				_logger.LogInformation("Successfully generated PDF for treatment record {RecordId}", treatmentRecord.Id);
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error processing treatment PDF for record {RecordId}", treatmentRecord.Id);
				return false;
			}
		}

		// ===== Helper: Generate HTML (SAME as 2025) =====
		private string GenerateTreatmentHtml2019(TreatmentJsonPayload data)
		{
			var treatmentTableRows = new StringBuilder();

			for (int i = 0; i < data.Treatments.Count; i++)
			{
				var treatment = data.Treatments[i];
				treatmentTableRows.AppendLine($@"
            <tr>
                <td>{i + 1}</td>
                <td>{treatment.Name}</td>
                <td>{treatment.QtyPerDay}</td>
                <td class=""right"">{treatment.Cost:N2}</td>
                <td>{treatment.Status}</td>
                <td class=""right"">{treatment.Total:N2}</td>
            </tr>");
			}

			var formattedDob = !string.IsNullOrEmpty(data.Patient.Dob) ? data.Patient.Dob : " ";

			return $@"
            <!DOCTYPE html>
            <html lang=""en"">
              <head>
                <meta charset=""UTF-8"" />
                <style>
                  body {{
                    font-family: Arial, Helvetica, sans-serif;
                    margin: 40px;
                    color: #333;
                    background-color: #fff;
                  }}
                  .header {{
                    text-align: center;
                    margin-bottom: 20px;
                  }}
                  .header img {{
                    max-height: 130px;
                    width: auto;
                  }}
                  .patient-info {{
                    border: 1px solid #e5e7eb;
                    border-radius: 8px;
                    padding: 16px;
                    margin-bottom: 24px;
                    font-size: 13px;
                    background-color: #fafafa;
                  }}
                  .patient-info table {{
                    width: 100%;
                    border-collapse: collapse;
                  }}
                  .patient-info td {{
                    padding: 4px 8px;
                    vertical-align: top;
                  }}
                  .section-title {{
                    font-weight: bold;
                    margin-bottom: 8px;
                    font-size: 14px;
                    color: #333;
                  }}
                  table.treatment {{
                    width: 100%;
                    border-collapse: collapse;
                    margin-top: 8px;
                    font-size: 13px;
                    border: 1px solid #000;
                  }}
                  table.treatment th,
                  table.treatment td {{
                    border: 1px solid #000;
                    padding: 10px;
                    text-align: left;
                  }}
                  table.treatment th {{
                    background: #f9f9f9;
                    font-weight: 600;
                    color: #333;
                  }}
                  table.treatment td.right {{
                    text-align: right;
                  }}
                  .totals {{
                    font-weight: bold;
                    background-color: #f9f9f9;
                  }}
                  .totals td {{
                    border-top: 2px solid #000;
                  }}
                  .signature {{
                    margin-top: 40px;
                    text-align: right;
                    font-size: 14px;
                    color: #333;
                  }}
                  .signature .line {{
                    margin-bottom: 2px;
                    border-top: 1px solid #333;
                    width: 116px;
                    margin-left: auto;
                  }}
                  .signature p {{
                    font-family: ""Cedarville Cursive"", cursive;
                    font-size: 15px;
                    color: #1a3c6e;
                    margin: 0;
                    padding-right: 0px;
                  }}
                  footer {{
                    margin-top: 40px;
                    font-size: 15px;
                    display: flex;
                    justify-content: space-between;
                    color: #000;
                  }}
                </style>
              </head>
              <body>
                <!-- Header with Logo (SAME as 2025) -->
                <div class=""header"">
                  <img src=""https://d7cop3y0lcg80.cloudfront.net/reports/1/0604d10d087f97b877ea0ae85e9494b5df28b6e7_26-09-2025_10-12-19.png"" alt=""ARTHROSE CRANIOFACIAL PAIN & TMJ CENTRE Logo"" />
                </div>

                <!-- Patient Info -->
                <div class=""patient-info"">
                  <table>
                    <tr>
                      <td><strong>Patient Name:</strong> {data.Patient.Name}</td>
                      <td><strong>HFID:</strong> {data.Patient.Hfid}</td>
                    </tr>
                    <tr>
                      <td><strong>Gender:</strong> {data.Patient.Gender}</td>
                      <td><strong>TID:</strong> {data.Patient.Tid}</td>
                    </tr>
                    <tr>
                      <td><strong>DOB:</strong> {formattedDob}</td>
                      <td><strong>Mobile:</strong> {data.Patient.Mobile}</td>
                    </tr>
                    <tr>
                      <td><strong>Consultant Coach:</strong> {data.Patient.Doctor}</td>
                      <td><strong>City:</strong> {data.Patient.City}</td>
                    </tr>
                  </table>
                </div>

                <!-- Treatment Plan -->
                <div class=""section-title"">Treatment Plan</div>
                <table class=""treatment"">
                  <thead>
                    <tr>
                      <th>S.No.</th>
                      <th>Treatment Name</th>
                      <th>Qty/Day</th>
                      <th>Cost (₹)</th>
                      <th>Status</th>
                      <th>Total (₹)</th>
                    </tr>
                  </thead>
                  <tbody>
                    {treatmentTableRows}
                    <tr class=""totals"">
                      <td colspan=""5"" class=""right"">Total:</td>
                      <td class=""right"">{data.TotalCost:N2}</td>
                    </tr>
                    <tr class=""totals"">
                      <td colspan=""5"" class=""right"">Grand Total:</td>
                      <td class=""right"">{data.GrandTotal:N2}</td>
                    </tr>
                  </tbody>
                </table>

                <!-- Signature -->
                <div class=""signature"">
                  <p>{data.Patient.Doctor}</p>
                  <div class=""line"">{data.Patient.Doctor}</div>
                </div>

                <footer>
                  <span>www.arthrosetmjindia.com</span>
                  <span>www.hfiles.in</span>
                </footer>
              </body>
            </html>";
		}

		// ===== Helper: Get existing visit by date =====
		private async Task<ClinicVisit?> GetExistingVisitByDate(int clinicPatientId, DateTime appointmentDate)
		{
			return await _clinicVisitRepository.GetExistingVisitAsync(clinicPatientId, appointmentDate);
		}

















		// ===== New API: Import treatments for 2020-2024 =====
		[HttpPost("import-treatments-2020-2024")]
		//[Authorize]
		public async Task<IActionResult> ImportTreatments2020To2024FromExcel([FromForm] TreatmentImportRequest request)
		{
			HttpContext.Items["Log-Category"] = "2020-2024 Treatment Import";
			if (request.ExcelFile == null || request.ExcelFile.Length == 0)
				return BadRequest(ApiResponseFactory.Fail("Excel file is required."));
			if (!Path.GetExtension(request.ExcelFile.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase) &&
				!Path.GetExtension(request.ExcelFile.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
				return BadRequest(ApiResponseFactory.Fail("Only .csv and .xlsx files are supported."));

			//bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(CLINIC_ID, User);
			//if (!isAuthorized)
			//{
			//    _logger.LogWarning("Unauthorized treatment import attempt for Clinic ID {ClinicId}", CLINIC_ID);
			//    return Unauthorized(ApiResponseFactory.Fail("You are not authorized to import treatments for this clinic."));
			//}

			var response = new TreatmentImportResponse();
			var transaction = await _clinicRepository.BeginTransactionAsync();
			var committed = false;

			try
			{
				List<ExcelTreatmentRow> treatmentRows;
				if (Path.GetExtension(request.ExcelFile.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
				{
					treatmentRows = await ProcessCsvFile2020To2024(request.ExcelFile);
				}
				else
				{
					treatmentRows = await ProcessExcelFile2020To2024(request.ExcelFile);
				}

				if (!treatmentRows.Any())
				{
					return BadRequest(ApiResponseFactory.Fail("No valid treatment data found in the file for 2020-2024."));
				}

				// Group treatments by patient and date
				var groupedTreatments = treatmentRows
					.GroupBy(t => new { t.PatientId, t.ParsedDate.Date })
					.ToList();

				foreach (var group in groupedTreatments)
				{
					response.TotalProcessed++;
					try
					{
						var success = await ProcessTreatmentGroup2020To2024(
							group.Key.PatientId,
							group.Key.Date,
							group.ToList(),
							response);
						if (success)
						{
							response.Successful++;
						}
						else
						{
							response.Failed++;
						}
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Error processing treatment group for Patient {PatientId} on {Date}", group.Key.PatientId, group.Key.Date);
						response.Failed++;
						response.SkippedReasons.Add($"Patient {group.Key.PatientId} on {group.Key.Date:dd-MM-yyyy}: Processing error - {ex.Message}");
					}
				}

				await transaction.CommitAsync();
				committed = true;

				response.Message = $"2020-2024 Treatment import completed: {response.Successful} successful, " +
								   $"{response.Failed} failed out of {response.TotalProcessed} total treatment groups. " +
								   $"Processed {response.PatientsProcessed} patients with {response.VisitsCreated} visits.";
				_logger.LogInformation("2020-2024 Treatment import completed: {Added} added, {Failed} failed", response.Successful, response.Failed);

				// Invalidate cache
				_cacheService.InvalidateClinicStatistics(CLINIC_ID);

				return Ok(ApiResponseFactory.Success(response, response.Message));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to import 2020-2024 treatments from Excel");
				return StatusCode(500, ApiResponseFactory.Fail("Failed to process Excel file: " + ex.Message));
			}
			finally
			{
				if (!committed && transaction.GetDbTransaction().Connection != null)
					await transaction.RollbackAsync();
			}
		}

		// ===== Helper: Process CSV file for 2020-2024 (filter by year) =====
		private async Task<List<ExcelTreatmentRow>> ProcessCsvFile2020To2024(IFormFile file)
		{
			var treatments = new List<ExcelTreatmentRow>();
			using var reader = new StreamReader(file.OpenReadStream());
			var csvContent = await reader.ReadToEndAsync();
			var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

			for (int i = 1; i < lines.Length; i++) // Skip header
			{
				var columns = lines[i].Split(',');
				if (columns.Length >= 9)
				{
					var treatment = new ExcelTreatmentRow
					{
						PatientName = columns[0].Trim(),
						PatientId = columns[1].Trim(),
						DateString = columns[2].Trim(),
						TreatmentName = columns[3].Trim(),
						Status = columns[4].Trim(),
						Cost = int.TryParse(columns[5].Trim(), out var cost) ? cost : 0,
						Quantity = int.TryParse(columns[6].Trim(), out var qty) ? qty : 1,
						QuantityType = columns[7].Trim(),
						FinalCost = int.TryParse(columns[8].Trim(), out var finalCost) ? finalCost : 0
					};

					// Try multiple date formats
					if (DateTime.TryParseExact(treatment.DateString, "dd-MM-yyyy", null, DateTimeStyles.None, out var parsedDate) ||
						DateTime.TryParseExact(treatment.DateString, "MM/dd/yyyy", null, DateTimeStyles.None, out parsedDate) ||
						DateTime.TryParseExact(treatment.DateString, "yyyy-MM-dd", null, DateTimeStyles.None, out parsedDate))
					{
						treatment.ParsedDate = parsedDate;

						// Filter: Only add if year is 2020-2024
						if (parsedDate.Year >= 2020 && parsedDate.Year <= 2024)
						{
							treatments.Add(treatment);
						}
					}
					else
					{
						_logger.LogWarning("Invalid date format for row {Row}: {DateString}", i + 1, treatment.DateString);
					}
				}
			}
			return treatments;
		}

		// ===== Helper: Process Excel file for 2020-2024 (filter by year) =====
		private async Task<List<ExcelTreatmentRow>> ProcessExcelFile2020To2024(IFormFile file)
		{
			var treatments = new List<ExcelTreatmentRow>();
			using var stream = new MemoryStream();
			await file.CopyToAsync(stream);
			ExcelPackage.License.SetNonCommercialPersonal("HFiles");
			using var package = new ExcelPackage(stream);
			var worksheet = package.Workbook.Worksheets[0];
			if (worksheet.Dimension == null) return treatments;

			var rowCount = worksheet.Dimension.End.Row;
			for (int row = 2; row <= rowCount; row++)
			{
				var treatment = new ExcelTreatmentRow
				{
					PatientName = worksheet.Cells[row, 1].Text.Trim(),
					PatientId = worksheet.Cells[row, 2].Text.Trim(),
					DateString = worksheet.Cells[row, 3].Text.Trim(),
					TreatmentName = worksheet.Cells[row, 4].Text.Trim(),
					Status = worksheet.Cells[row, 5].Text.Trim(),
					Cost = int.TryParse(worksheet.Cells[row, 6].Text.Trim(), out var cost) ? cost : 0,
					Quantity = int.TryParse(worksheet.Cells[row, 7].Text.Trim(), out var qty) ? qty : 1,
					QuantityType = worksheet.Cells[row, 8].Text.Trim(),
					FinalCost = int.TryParse(worksheet.Cells[row, 9].Text.Trim(), out var finalCost) ? finalCost : 0
				};

				// Try multiple date formats
				if (DateTime.TryParseExact(treatment.DateString, "dd-MM-yyyy", null, DateTimeStyles.None, out var parsedDate) ||
					DateTime.TryParseExact(treatment.DateString, "MM/dd/yyyy", null, DateTimeStyles.None, out parsedDate) ||
					DateTime.TryParseExact(treatment.DateString, "yyyy-MM-dd", null, DateTimeStyles.None, out parsedDate))
				{
					treatment.ParsedDate = parsedDate;

					// Filter: Only add if year is 2020-2024
					if (parsedDate.Year >= 2020 && parsedDate.Year <= 2024)
					{
						treatments.Add(treatment);
					}
				}
				else
				{
					_logger.LogWarning("Invalid date format for row {Row}: {DateString}", row, treatment.DateString);
				}
			}
			return treatments;
		}

		// ===== Helper: Process treatment group for 2020-2024 (same as 2019) =====
		private async Task<bool> ProcessTreatmentGroup2020To2024(
			string patientId,
			DateTime appointmentDate,
			List<ExcelTreatmentRow> treatments,
			TreatmentImportResponse response)
		{
			// Same logic as ProcessTreatmentGroup2019 (no changes needed, copy from your original code)
			// 1. Find user by patientId
			var user = await _userRepository.GetUserByPatientIdAsync(patientId);
			if (user == null)
			{
				response.SkippedReasons.Add($"Patient {patientId}: User not found in database");
				return false;
			}

			// 2. Get or create clinic patient
			var fullName = $"{user.FirstName} {user.LastName}".Trim();
			var clinicPatient = await _clinicVisitRepository.GetOrCreatePatientAsync(user.HfId ?? "", fullName);

			// 3. Check if visit exists for this date
			var existingVisit = await GetExistingVisitByDate(clinicPatient.Id, appointmentDate);
			ClinicVisit visit;
			if (existingVisit == null)
			{
				// Create new visit
				visit = new ClinicVisit
				{
					ClinicPatientId = clinicPatient.Id,
					ClinicId = CLINIC_ID,
					AppointmentDate = appointmentDate.Date,
					AppointmentTime = APPOINTMENT_TIME,
					PaymentMethod = null
				};
				await _clinicVisitRepository.SaveVisitAsync(visit);
				response.VisitsCreated++;
			}
			else
			{
				visit = existingVisit;
			}

			// 4. Create treatment JSON data (same format)
			var treatmentJsonData = CreateTreatmentJsonData2020To2024(user, clinicPatient, treatments, patientId);

			// 5. Check if treatment record already exists
			var existingRecord = await _clinicPatientRecordRepository.GetByCompositeKeyAsync(
				CLINIC_ID, clinicPatient.Id, visit.Id, RecordType.Treatment);
			if (existingRecord != null)
			{
				response.SkippedReasons.Add($"Patient {patientId} on {appointmentDate:dd-MM-yyyy}: Treatment record already exists");
				return false;
			}

			// 6. Create treatment record
			var treatmentRecord = new ClinicPatientRecord
			{
				ClinicId = CLINIC_ID,
				PatientId = clinicPatient.Id,
				ClinicVisitId = visit.Id,
				Type = RecordType.Treatment,
				JsonData = treatmentJsonData,
				SendToPatient = false
			};
			await _clinicPatientRecordRepository.SaveAsync(treatmentRecord);

			response.PatientsProcessed++;
			response.AddedTreatments.Add(new AddedTreatmentSummary
			{
				PatientId = patientId,
				PatientName = fullName,
				HFID = user.HfId ?? "",
				AppointmentDate = appointmentDate.ToString("dd-MM-yyyy"),
				TreatmentCount = treatments.Count,
				TotalCost = treatments.Sum(t => t.FinalCost),
				Treatments = treatments.Select(t => t.TreatmentName).ToList()
			});

			return true;
		}

		// ===== Helper: Create treatment JSON data for 2020-2024 (same as 2019) =====
		private string CreateTreatmentJsonData2020To2024(
			Domain.Entities.Users.User user,
			ClinicPatient clinicPatient,
			List<ExcelTreatmentRow> treatments,
			string patientId)
		{
			// Same as CreateTreatmentJsonData2019 (copy from original)
			var treatmentItems = treatments.Select(t => new
			{
				name = t.TreatmentName,
				qtyPerDay = $"{t.Quantity} {t.QuantityType}",
				cost = t.Cost,
				status = t.Status ?? "Not Started",
				total = t.FinalCost
			}).ToArray();

			var totalCost = treatments.Sum(t => t.FinalCost);

			var treatmentData = new
			{
				patient = new
				{
					name = $"{user.FirstName} {user.LastName}".Trim(),
					hfid = user.HfId ?? "",
					gender = user.Gender ?? "",
					tid = patientId,
					dob = user.DOB ?? "",
					mobile = user.PhoneNumber ?? "",
					doctor = DOCTOR_NAME,
					city = user.City ?? ""
				},
				treatments = treatmentItems,
				totalCost = totalCost,
				grandTotal = totalCost,
				clinicInfo = new
				{
					name = "ARTHROSE",
					subtitle = "CRANIOFACIAL PAIN & TMJ CENTRE",
					website = "www.arthrosetmjindia.com"
				}
			};

			return JsonConvert.SerializeObject(treatmentData, Formatting.None);
		}

		// ===== New API: Generate treatment PDFs for 2020-2024 (identical to 2019 except name/logging) =====
		[HttpPost("generate-treatment-pdfs-2020-2024")]
		//[Authorize]
		public async Task<IActionResult> GenerateTreatmentPdfs2020To2024([FromBody] TreatmentPdfRequest request)
		{
			HttpContext.Items["Log-Category"] = "2020-2024 Treatment PDF Generation";

			//bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(CLINIC_ID, User);
			//if (!isAuthorized)
			//{
			//    _logger.LogWarning("Unauthorized PDF generation attempt for Clinic ID {ClinicId}", CLINIC_ID);
			//    return Unauthorized(ApiResponseFactory.Fail("You are not authorized to generate PDFs for this clinic."));
			//}

			var response = new TreatmentPdfResponse();
			var transaction = await _clinicRepository.BeginTransactionAsync();
			var committed = false;

			try
			{
				// Get all unsent treatment records for CLINIC_ID = 8 (same as before)
				var treatmentRecords = await _clinicPatientRecordRepository.GetUnsentTreatmentRecordsAsync(CLINIC_ID);
				if (!treatmentRecords.Any())
				{
					return Ok(ApiResponseFactory.Success(new TreatmentPdfResponse { TotalProcessed = 0, Successful = 0, Failed = 0, Message = "No unsent treatment records found." }, "No unsent treatment records found."));
				}

				_logger.LogInformation("Found {Count} unsent treatment records", treatmentRecords.Count);

				// Download Chromium once for all PDFs
				await new BrowserFetcher().DownloadAsync();
				using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true, Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" } });

				foreach (var treatmentRecord in treatmentRecords)
				{
					response.TotalProcessed++;
					try
					{
						var success = await ProcessSingleTreatmentPdf2020To2024(treatmentRecord, browser, response);
						if (success)
						{
							response.Successful++;
						}
						else
						{
							response.Failed++;
							response.FailedRecords.Add(new TreatmentPdfFailed { RecordId = treatmentRecord.Id, Reason = "PDF generation or upload failed" });
						}
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Error generating PDF for treatment record {RecordId}", treatmentRecord.Id);
						response.Failed++;
						response.FailedRecords.Add(new TreatmentPdfFailed { RecordId = treatmentRecord.Id, Reason = $"Processing error: {ex.Message}" });
					}
				}

				await transaction.CommitAsync();
				committed = true;

				response.Message = $"2020-2024 Treatment PDF generation completed: {response.Successful} successful, " +
								   $"{response.Failed} failed out of {response.TotalProcessed} total treatments.";
				_logger.LogInformation("2020-2024 Treatment PDF generation completed. Success: {Success}, Failed: {Failed}", response.Successful, response.Failed);

				return Ok(ApiResponseFactory.Success(response, response.Message));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during 2020-2024 treatment PDF generation");
				return StatusCode(500, ApiResponseFactory.Fail("An error occurred during PDF generation."));
			}
			finally
			{
				if (!committed && transaction.GetDbTransaction().Connection != null)
					await transaction.RollbackAsync();
			}
		}

		// ===== Helper: Process single treatment PDF for 2020-2024 (same as 2019) =====
		private async Task<bool> ProcessSingleTreatmentPdf2020To2024(
			ClinicPatientRecord treatmentRecord,
			IBrowser browser,
			TreatmentPdfResponse response)
		{
			// Same logic as ProcessSingleTreatmentPdf2019 (copy from your original code, including GenerateTreatmentHtml2020To2024 which is identical to GenerateTreatmentHtml2019)
			try
			{
				// Get related data
				var visit = await _clinicVisitRepository.GetByIdAsync(treatmentRecord.ClinicVisitId);
				var clinicPatient = await _clinicPatientRecordRepository.GetByIdAsync(treatmentRecord.PatientId);  // Assuming this is _clinicPatientRepository or similar; adjust if needed
				var user = clinicPatient != null ? await _userRepository.GetUserByHFIDAsync(clinicPatient.HFID) : null;

				if (visit == null || clinicPatient == null || user == null)
				{
					_logger.LogWarning("Missing related data for treatment record {RecordId}", treatmentRecord.Id);
					return false;
				}

				// Parse treatment JSON
				var treatmentData = JsonConvert.DeserializeObject<TreatmentJsonPayload>(treatmentRecord.JsonData);
				if (treatmentData?.Patient == null || treatmentData.Treatments == null)
				{
					_logger.LogWarning("Invalid treatment JSON data for record {RecordId}", treatmentRecord.Id);
					return false;
				}

				// Generate HTML and PDF
				var htmlContent = GenerateTreatmentHtml2020To2024(treatmentData);  // Same as GenerateTreatmentHtml2019
				using var page = await browser.NewPageAsync();
				await page.SetContentAsync(htmlContent);
				var pdfOptions = new PdfOptions
				{
					Format = PaperFormat.A4,
					PrintBackground = true,
					MarginOptions = new MarginOptions { Top = "0.5in", Bottom = "0.5in", Left = "0.5in", Right = "0.5in" }
				};
				var pdfBytes = await page.PdfDataAsync(pdfOptions);

				if (pdfBytes == null || pdfBytes.Length == 0)
				{
					_logger.LogWarning("PDF generation returned empty bytes for record {RecordId}", treatmentRecord.Id);
					return false;
				}

				// Upload to S3 (same path structure)
				var tempFileName = $"treatment_{treatmentRecord.ClinicId}_{treatmentRecord.PatientId}_{treatmentRecord.ClinicVisitId}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
				var tempFilePath = Path.Combine(Path.GetTempPath(), tempFileName);
				await System.IO.File.WriteAllBytesAsync(tempFilePath, pdfBytes);

				var s3Key = $"clinic/{tempFileName}";
				var s3Url = await _s3StorageService.UploadFileToS3(tempFilePath, s3Key);

				if (string.IsNullOrEmpty(s3Url))
				{
					System.IO.File.Delete(tempFilePath);
					_logger.LogWarning("S3 upload failed for record {RecordId}", treatmentRecord.Id);
					return false;
				}

				// Update record
				treatmentRecord.SendToPatient = true;
				await _clinicPatientRecordRepository.UpdateAsync(treatmentRecord);

				// Create user report (same naming)
				var reportName = $"Arthrose_Treatment_{treatmentData.Patient.Name.Replace(" ", "_")}_{visit.AppointmentDate:dd-MM-yy}";
				var epochTime = new DateTimeOffset(visit.AppointmentDate.Date + visit.AppointmentTime).ToUnixTimeSeconds();
				var fileSizeKb = Math.Round((decimal)pdfBytes.Length / 1024, 2);

				var userReport = new UserReport
				{
					UserId = user.Id,
					ReportName = reportName,
					ReportCategory = 1,  // Treatment category
					ReportUrl = s3Url,
					EpochTime = epochTime,
					FileSize = fileSizeKb,
					UploadedBy = "Clinic",
					ClinicId = 8,
					UserType = "Independent",
					DeletedBy = 0
				};
				await _userRepository.SaveAsync(userReport);

				// Clean up
				System.IO.File.Delete(tempFilePath);

				// Add to successful records
				response.SuccessfulRecords.Add(new TreatmentPdfSuccess
				{
					RecordId = treatmentRecord.Id,
					PatientName = treatmentData.Patient.Name,
					HFID = clinicPatient.HFID,
					AppointmentDate = visit.AppointmentDate.ToString("dd-MM-yyyy"),
					TreatmentUrl = s3Url,
					FileSizeKB = fileSizeKb
				});

				_logger.LogInformation("Successfully generated PDF for treatment record {RecordId}", treatmentRecord.Id);
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error processing treatment PDF for record {RecordId}", treatmentRecord.Id);
				return false;
			}
		}

		// ===== Helper: Generate HTML for 2020-2024 (same as 2019) =====
		private string GenerateTreatmentHtml2020To2024(TreatmentJsonPayload data)
		{
			// Identical to GenerateTreatmentHtml2019 (copy from your original code)
			var treatmentTableRows = new StringBuilder();
			for (int i = 0; i < data.Treatments.Count; i++)
			{
				var treatment = data.Treatments[i];
				treatmentTableRows.AppendLine($@"
            <tr>
                <td>{i + 1}</td>
                <td>{treatment.Name}</td>
                <td>{treatment.QtyPerDay}</td>
                <td class=""right"">{treatment.Cost:N2}</td>
                <td>{treatment.Status}</td>
                <td class=""right"">{treatment.Total:N2}</td>
            </tr>");
			}

			var formattedDob = !string.IsNullOrEmpty(data.Patient.Dob) ? data.Patient.Dob : " ";

			return $@"
        <!DOCTYPE html>
        <html lang=""en"">
        <head>
            <meta charset=""UTF-8"" />
            <style>
                body {{ font-family: Arial, Helvetica, sans-serif; margin: 40px; color: #333; background-color: #fff; }}
                .header {{ text-align: center; margin-bottom: 20px; }}
                .header img {{ max-height: 130px; width: auto; }}
                .patient-info {{ border: 1px solid #e5e7eb; border-radius: 8px; padding: 16px; margin-bottom: 24px; font-size: 13px; background-color: #fafafa; }}
                .patient-info table {{ width: 100%; border-collapse: collapse; }}
                .patient-info td {{ padding: 4px 8px; vertical-align: top; }}
                .section-title {{ font-weight: bold; margin-bottom: 8px; font-size: 14px; color: #333; }}
                table.treatment {{ width: 100%; border-collapse: collapse; margin-top: 8px; font-size: 13px; border: 1px solid #000; }}
                table.treatment th, table.treatment td {{ border: 1px solid #000; padding: 10px; text-align: left; }}
                table.treatment th {{ background: #f9f9f9; font-weight: 600; color: #333; }}
                table.treatment td.right {{ text-align: right; }}
                .totals {{ font-weight: bold; background-color: #f9f9f9; }}
                .totals td {{ border-top: 2px solid #000; }}
                .signature {{ margin-top: 40px; text-align: right; font-size: 14px; color: #333; }}
                .signature .line {{ margin-bottom: 2px; border-top: 1px solid #333; width: 116px; margin-left: auto; }}
                .signature p {{ font-family: ""Cedarville Cursive"", cursive; font-size: 15px; color: #1a3c6e; margin: 0; padding-right: 0px; }}
                footer {{ margin-top: 40px; font-size: 15px; display: flex; justify-content: space-between; color: #000; }}
            </style>
        </head>
        <body>
            <!-- Header with Logo (SAME as 2019) -->
            <div class=""header"">
                <img src=""https://d7cop3y0lcg80.cloudfront.net/reports/1/0604d10d087f97b877ea0ae85e9494b5df28b6e7_26-09-2025_10-12-19.png"" alt=""ARTHROSE CRANIOFACIAL PAIN & TMJ CENTRE Logo"" />
            </div>

            <!-- Patient Info -->
            <div class=""patient-info"">
                <table>
                    <tr>
                        <td><strong>Patient Name:</strong> {data.Patient.Name}</td>
                        <td><strong>HFID:</strong> {data.Patient.Hfid}</td>
                    </tr>
                    <tr>
                        <td><strong>Gender:</strong> {data.Patient.Gender}</td>
                        <td><strong>TID:</strong> {data.Patient.Tid}</td>
                    </tr>
                    <tr>
                        <td><strong>DOB:</strong> {formattedDob}</td>
                        <td><strong>Mobile:</strong> {data.Patient.Mobile}</td>
                    </tr>
                    <tr>
                        <td><strong>Consultant Coach:</strong> {data.Patient.Doctor}</td>
                        <td><strong>City:</strong> {data.Patient.City}</td>
                    </tr>
                </table>
            </div>

            <!-- Treatment Plan -->
            <div class=""section-title"">Treatment Plan</div>
            <table class=""treatment"">
                <thead>
                    <tr>
                        <th>S.No.</th>
                        <th>Treatment Name</th>
                        <th>Qty/Day</th>
                        <th>Cost (₹)</th>
                        <th>Status</th>
                        <th>Total (₹)</th>
                    </tr>
                </thead>
                <tbody>
                    {treatmentTableRows}
                    <tr class=""totals"">
                        <td colspan=""5"" class=""right"">Total:</td>
                        <td class=""right"">{data.TotalCost:N2}</td>
                    </tr>
                    <tr class=""totals"">
                        <td colspan=""5"" class=""right"">Grand Total:</td>
                        <td class=""right"">{data.GrandTotal:N2}</td>
                    </tr>
                </tbody>
            </table>

            <!-- Signature -->
            <div class=""signature"">
                <p>{data.Patient.Doctor}</p>
                <div class=""line"">{data.Patient.Doctor}</div>
            </div>

            <footer>
                <span>www.arthrosetmjindia.com</span>
                <span>www.hfiles.in</span>
            </footer>
        </body>
        </html>";
		}

		// Reuse the existing GetExistingVisitByDate helper (no change needed).





















		// ===== 2019 INVOICE IMPORT API =====

		// API 1: Import 2019 invoices from Excel/CSV
		[HttpPost("import-invoices-2019")]
		//[Authorize]
		public async Task<IActionResult> ImportInvoices2019FromExcel([FromForm] InvoiceImportRequest request)
		{
			HttpContext.Items["Log-Category"] = "2019 Invoice Import";

			if (request.ExcelFile == null || request.ExcelFile.Length == 0)
				return BadRequest(ApiResponseFactory.Fail("Excel file is required."));

			if (!Path.GetExtension(request.ExcelFile.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase) &&
				!Path.GetExtension(request.ExcelFile.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
				return BadRequest(ApiResponseFactory.Fail("Only .csv and .xlsx files are supported."));

			var response = new InvoiceImportResponse();
			var transaction = await _clinicRepository.BeginTransactionAsync();
			var committed = false;

			try
			{
				List<ExcelInvoiceRow> invoiceRows;

				if (Path.GetExtension(request.ExcelFile.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
				{
					invoiceRows = await ProcessInvoiceCsvFile2019(request.ExcelFile);
				}
				else
				{
					invoiceRows = await ProcessInvoiceExcelFile2019(request.ExcelFile);
				}

				if (!invoiceRows.Any())
				{
					return BadRequest(ApiResponseFactory.Fail("No valid invoice data found in the file."));
				}

				// Group invoices by patient, date, and invoice ID
				var groupedInvoices = invoiceRows
					.GroupBy(i => new { i.PatientId, i.ParsedDate.Date, i.InvoiceId })
					.ToList();

				foreach (var group in groupedInvoices)
				{
					response.TotalProcessed++;

					try
					{
						var success = await ProcessInvoiceGroup2019(
							group.Key.PatientId,
							group.Key.Date,
							group.Key.InvoiceId,
							group.ToList(),
							response);

						if (success)
						{
							response.Successful++;
						}
						else
						{
							response.Failed++;
						}
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Error processing invoice group for Patient {PatientId} on {Date} Invoice {InvoiceId}",
							group.Key.PatientId, group.Key.Date, group.Key.InvoiceId);
						response.Failed++;
						response.SkippedReasons.Add($"Patient {group.Key.PatientId} on {group.Key.Date:dd-MM-yyyy} Invoice {group.Key.InvoiceId}: Processing error - {ex.Message}");
					}
				}

				await transaction.CommitAsync();
				committed = true;

				response.Message = $"2019 Invoice import completed: {response.Successful} successful, " +
								  $"{response.Failed} failed out of {response.TotalProcessed} total invoice groups. " +
								  $"Processed {response.PatientsProcessed} patients with {response.VisitsCreated} visits.";

				_logger.LogInformation("2019 Invoice import completed: {Added} added, {Failed} failed",
					response.Successful, response.Failed);

				// Invalidate cache
				_cacheService.InvalidateClinicStatistics(CLINIC_ID);

				return Ok(ApiResponseFactory.Success(response, response.Message));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to import 2019 invoices from Excel");
				return StatusCode(500, ApiResponseFactory.Fail("Failed to process Excel file: " + ex.Message));
			}
			finally
			{
				if (!committed && transaction.GetDbTransaction().Connection != null)
					await transaction.RollbackAsync();
			}
		}

		// Helper: Process CSV file for 2019 invoices
		private async Task<List<ExcelInvoiceRow>> ProcessInvoiceCsvFile2019(IFormFile file)
		{
			var invoices = new List<ExcelInvoiceRow>();

			using var reader = new StreamReader(file.OpenReadStream());
			var csvContent = await reader.ReadToEndAsync();
			var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

			for (int i = 1; i < lines.Length; i++) // Skip header
			{
				var columns = lines[i].Split(',');
				if (columns.Length >= 8)
				{
					var invoice = new ExcelInvoiceRow
					{
						PatientName = columns[0].Trim(),
						PatientId = columns[1].Trim(),
						DateString = columns[2].Trim(),
						InvoiceId = columns[3].Trim(),
						ServiceName = columns[4].Trim(),
						Cost = int.TryParse(columns[5].Trim(), out var cost) ? cost : 0,
						QuantityValue = columns[6].Trim(),
						FinalCost = int.TryParse(columns[7].Trim(), out var finalCost) ? finalCost : 0
					};

					// Try multiple date formats
					if (DateTime.TryParseExact(invoice.DateString, "dd-MM-yyyy", null, DateTimeStyles.None, out var parsedDate) ||
						DateTime.TryParseExact(invoice.DateString, "MM/dd/yyyy", null, DateTimeStyles.None, out parsedDate) ||
						DateTime.TryParseExact(invoice.DateString, "yyyy-MM-dd", null, DateTimeStyles.None, out parsedDate))
					{
						invoice.ParsedDate = parsedDate;
						invoices.Add(invoice);
					}
					else
					{
						_logger.LogWarning("Invalid date format for row {Row}: {DateString}", i + 1, invoice.DateString);
					}
				}
			}

			return invoices;
		}

		// Helper: Process Excel file for 2019 invoices
		private async Task<List<ExcelInvoiceRow>> ProcessInvoiceExcelFile2019(IFormFile file)
		{
			var invoices = new List<ExcelInvoiceRow>();

			using var stream = new MemoryStream();
			await file.CopyToAsync(stream);

			ExcelPackage.License.SetNonCommercialPersonal("HFiles");
			using var package = new ExcelPackage(stream);
			var worksheet = package.Workbook.Worksheets[0];

			if (worksheet.Dimension == null) return invoices;

			var rowCount = worksheet.Dimension.End.Row;

			for (int row = 2; row <= rowCount; row++)
			{
				var invoice = new ExcelInvoiceRow
				{
					PatientName = worksheet.Cells[row, 1].Text.Trim(),
					PatientId = worksheet.Cells[row, 2].Text.Trim(),
					DateString = worksheet.Cells[row, 3].Text.Trim(),
					InvoiceId = worksheet.Cells[row, 4].Text.Trim(),
					ServiceName = worksheet.Cells[row, 5].Text.Trim(),
					Cost = int.TryParse(worksheet.Cells[row, 6].Text.Trim(), out var cost) ? cost : 0,
					QuantityValue = worksheet.Cells[row, 7].Text.Trim(),
					FinalCost = int.TryParse(worksheet.Cells[row, 8].Text.Trim(), out var finalCost) ? finalCost : 0
				};

				// Try multiple date formats
				if (DateTime.TryParseExact(invoice.DateString, "dd-MM-yyyy", null, DateTimeStyles.None, out var parsedDate) ||
					DateTime.TryParseExact(invoice.DateString, "MM/dd/yyyy", null, DateTimeStyles.None, out parsedDate) ||
					DateTime.TryParseExact(invoice.DateString, "yyyy-MM-dd", null, DateTimeStyles.None, out parsedDate))
				{
					invoice.ParsedDate = parsedDate;
					invoices.Add(invoice);
				}
				else
				{
					_logger.LogWarning("Invalid date format for row {Row}: {DateString}", row, invoice.DateString);
				}
			}

			return invoices;
		}

		// Helper: Process invoice group for 2019
		private async Task<bool> ProcessInvoiceGroup2019(
			string patientId,
			DateTime appointmentDate,
			string invoiceId,
			List<ExcelInvoiceRow> invoices,
			InvoiceImportResponse response)
		{
			// 1. Find user by patientId
			var user = await _userRepository.GetUserByPatientIdAsync(patientId);
			if (user == null)
			{
				response.SkippedReasons.Add($"Patient {patientId}: User not found in database");
				return false;
			}

			// 2. Get or create clinic patient
			var fullName = $"{user.FirstName} {user.LastName}".Trim();
			var clinicPatient = await _clinicVisitRepository.GetOrCreatePatientAsync(user.HfId ?? "", fullName);

			// 3. Check if visit exists for this date
			var existingVisit = await GetExistingVisitByDate(clinicPatient.Id, appointmentDate);
			ClinicVisit visit;

			if (existingVisit == null)
			{
				// Create new visit
				visit = new ClinicVisit
				{
					ClinicPatientId = clinicPatient.Id,
					ClinicId = CLINIC_ID,
					AppointmentDate = appointmentDate.Date,
					AppointmentTime = APPOINTMENT_TIME,
					PaymentMethod = null
				};

				await _clinicVisitRepository.SaveVisitAsync(visit);
				response.VisitsCreated++;
			}
			else
			{
				visit = existingVisit;
			}

			// 4. Create invoice JSON data (same format as 2025)
			var invoiceJsonData = CreateInvoiceJsonData2019(user, clinicPatient, invoices, invoiceId, appointmentDate);

			// 5. Check if invoice record already exists
			var existingRecord = await _clinicPatientRecordRepository.GetByCompositeKeyAsync(
				CLINIC_ID, clinicPatient.Id, visit.Id, RecordType.Invoice);

			if (existingRecord != null)
			{
				response.SkippedReasons.Add($"Patient {patientId} on {appointmentDate:dd-MM-yyyy} Invoice {invoiceId}: Invoice record already exists");
				return false;
			}

			// 6. Create invoice record
			var invoiceRecord = new ClinicPatientRecord
			{
				ClinicId = CLINIC_ID,
				PatientId = clinicPatient.Id,
				ClinicVisitId = visit.Id,
				Type = RecordType.Invoice,
				JsonData = invoiceJsonData,
				SendToPatient = false
			};

			await _clinicPatientRecordRepository.SaveAsync(invoiceRecord);

			response.PatientsProcessed++;
			response.AddedInvoices.Add(new AddedInvoiceSummary
			{
				PatientId = patientId,
				PatientName = fullName,
				HFID = user.HfId ?? "",
				AppointmentDate = appointmentDate.ToString("dd-MM-yyyy"),
				InvoiceId = invoiceId,
				ServiceCount = invoices.Count,
				TotalCost = invoices.Sum(i => i.FinalCost),
				Services = invoices.Select(i => i.ServiceName).ToList()
			});

			return true;
		}

		// Helper: Create invoice JSON data (SAME as 2025)
		private string CreateInvoiceJsonData2019(
			Domain.Entities.Users.User user,
			ClinicPatient clinicPatient,
			List<ExcelInvoiceRow> invoices,
			string invoiceId,
			DateTime appointmentDate)
		{
			var serviceItems = invoices.Select(i => new
			{
				name = i.ServiceName,
				qtyPerDay = i.QuantityValue,
				cost = i.Cost,
				total = i.FinalCost
			}).ToArray();

			var totalCost = invoices.Sum(i => i.FinalCost);

			var invoiceData = new
			{
				patient = new
				{
					name = $"{user.FirstName} {user.LastName}".Trim(),
					hfid = user.HfId ?? "",
					gender = user.Gender ?? "",
					invid = invoiceId,
					dob = user.DOB ?? "",
					date = appointmentDate.ToString("dd/MM/yyyy"),
					mobile = user.PhoneNumber ?? "",
					city = user.City ?? ""
				},
				services = serviceItems,
				totalCost = totalCost,
				grandTotal = totalCost,
				paid = totalCost,
				clinicInfo = new
				{
					name = "ARTHROSE",
					subtitle = "CRANIOFACIAL PAIN & TMJ CENTRE",
					website = "www.arthrosetmjindia.com"
				}
			};

			return JsonConvert.SerializeObject(invoiceData, Formatting.None);
		}

		// API 2: Generate invoice PDFs for 2019
		[HttpPost("generate-invoice-pdfs-2019")]
		//[Authorize]
		public async Task<IActionResult> GenerateInvoicePdfs2019([FromBody] InvoicePdfRequest request)
		{
			HttpContext.Items["Log-Category"] = "2019 Invoice PDF Generation";

			var response = new InvoicePdfResponse();
			var transaction = await _clinicRepository.BeginTransactionAsync();
			var committed = false;

			try
			{
				// Get all unsent invoice records for CLINIC_ID = 8
				var invoiceRecords = await _clinicPatientRecordRepository.GetUnsentInvoiceRecordsAsync(CLINIC_ID);

				if (!invoiceRecords.Any())
				{
					return Ok(ApiResponseFactory.Success(new InvoicePdfResponse
					{
						TotalProcessed = 0,
						Successful = 0,
						Failed = 0,
						Message = "No unsent invoice records found."
					}, "No unsent invoice records found."));
				}

				_logger.LogInformation("Found {Count} unsent invoice records", invoiceRecords.Count);

				// Download Chromium once for all PDFs
				await new BrowserFetcher().DownloadAsync();

				using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
				{
					Headless = true,
					Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
				});

				foreach (var invoiceRecord in invoiceRecords)
				{
					response.TotalProcessed++;

					try
					{
						var success = await ProcessSingleInvoicePdf2019(invoiceRecord, browser, response);
						if (success)
						{
							response.Successful++;
						}
						else
						{
							response.Failed++;
							response.FailedRecords.Add(new InvoicePdfFailed
							{
								RecordId = invoiceRecord.Id,
								Reason = "PDF generation or upload failed"
							});
						}
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Error generating PDF for invoice record {RecordId}", invoiceRecord.Id);
						response.Failed++;
						response.FailedRecords.Add(new InvoicePdfFailed
						{
							RecordId = invoiceRecord.Id,
							Reason = $"Processing error: {ex.Message}"
						});
					}
				}

				await transaction.CommitAsync();
				committed = true;

				response.Message = $"Invoice PDF generation completed: {response.Successful} successful, " +
								  $"{response.Failed} failed out of {response.TotalProcessed} total invoices.";

				_logger.LogInformation("Invoice PDF generation completed. Success: {Success}, Failed: {Failed}",
					response.Successful, response.Failed);

				return Ok(ApiResponseFactory.Success(response, response.Message));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during invoice PDF generation");
				return StatusCode(500, ApiResponseFactory.Fail("An error occurred during PDF generation."));
			}
			finally
			{
				if (!committed && transaction.GetDbTransaction().Connection != null)
					await transaction.RollbackAsync();
			}
		}

		// Helper: Process single invoice PDF (SAME naming as 2025)
		private async Task<bool> ProcessSingleInvoicePdf2019(
			ClinicPatientRecord invoiceRecord,
			IBrowser browser,
			InvoicePdfResponse response)
		{
			try
			{
				// Get related data
				var visit = await _clinicVisitRepository.GetByIdAsync(invoiceRecord.ClinicVisitId);
				var clinicPatient = await _clinicPatientRecordRepository.GetByIdAsync(invoiceRecord.PatientId);
				var user = clinicPatient != null ? await _userRepository.GetUserByHFIDAsync(clinicPatient.HFID) : null;

				if (visit == null || clinicPatient == null || user == null)
				{
					_logger.LogWarning("Missing related data for invoice record {RecordId}", invoiceRecord.Id);
					return false;
				}

				// Parse invoice JSON
				var invoiceData = JsonConvert.DeserializeObject<InvoiceJsonPayload>(invoiceRecord.JsonData);
				if (invoiceData?.Patient == null || invoiceData.Services == null)
				{
					_logger.LogWarning("Invalid invoice JSON data for record {RecordId}", invoiceRecord.Id);
					return false;
				}

				// Generate HTML and PDF
				var htmlContent = GenerateInvoiceHtml2019(invoiceData);

				using var page = await browser.NewPageAsync();
				await page.SetContentAsync(htmlContent);

				var pdfOptions = new PdfOptions
				{
					Format = PaperFormat.A4,
					PrintBackground = true,
					MarginOptions = new MarginOptions
					{
						Top = "0.5in",
						Bottom = "0.5in",
						Left = "0.5in",
						Right = "0.5in"
					}
				};

				var pdfBytes = await page.PdfDataAsync(pdfOptions);
				if (pdfBytes == null || pdfBytes.Length == 0)
				{
					_logger.LogWarning("PDF generation returned empty bytes for record {RecordId}", invoiceRecord.Id);
					return false;
				}

				// Upload to S3 (same path structure as 2025)
				var tempFileName = $"invoice_clinic{invoiceRecord.ClinicId}_patient{invoiceRecord.PatientId}_{Guid.NewGuid()}.pdf";
				var tempFilePath = Path.Combine(Path.GetTempPath(), tempFileName);
				await System.IO.File.WriteAllBytesAsync(tempFilePath, pdfBytes);

				var s3Key = $"clinic/{tempFileName}";
				var s3Url = await _s3StorageService.UploadFileToS3(tempFilePath, s3Key);

				if (string.IsNullOrEmpty(s3Url))
				{
					System.IO.File.Delete(tempFilePath);
					_logger.LogWarning("S3 upload failed for record {RecordId}", invoiceRecord.Id);
					return false;
				}

				// Update invoice record to mark as sent
				invoiceRecord.SendToPatient = true;
				await _clinicPatientRecordRepository.UpdateAsync(invoiceRecord);

				// Create user report (SAME naming as 2025)
				var reportName = $"Arthrose_Invoice_{invoiceData.Patient.Name.Replace(" ", "_")}_{visit.AppointmentDate:dd-MM-yy}";
				var epochTime = new DateTimeOffset(visit.AppointmentDate.Date + visit.AppointmentTime).ToUnixTimeSeconds();
				var fileSizeKb = Math.Round((decimal)pdfBytes.Length / 1024, 2);

				var userReport = new UserReport
				{
					UserId = user.Id,
					ReportName = reportName,
					ReportCategory = 8, // Invoice category
					ReportUrl = s3Url,
					EpochTime = epochTime,
					FileSize = fileSizeKb,
					UploadedBy = "Clinic",
					ClinicId = 8,
					UserType = "Independent",
					DeletedBy = 0
				};

				await _userRepository.SaveAsync(userReport);

				// Create another clinic patient record with the PDF URL
				var pdfRecord = new ClinicPatientRecord
				{
					ClinicId = CLINIC_ID,
					PatientId = invoiceRecord.PatientId,
					ClinicVisitId = invoiceRecord.ClinicVisitId,
					Type = RecordType.Invoice,
					JsonData = JsonConvert.SerializeObject(new { url = s3Url }),
					SendToPatient = true
				};

				await _clinicPatientRecordRepository.SaveAsync(pdfRecord);

				// Clean up
				System.IO.File.Delete(tempFilePath);

				// Add to successful records
				response.SuccessfulRecords.Add(new InvoicePdfSuccess
				{
					RecordId = invoiceRecord.Id,
					PatientName = invoiceData.Patient.Name,
					HFID = clinicPatient.HFID,
					AppointmentDate = visit.AppointmentDate.ToString("dd-MM-yyyy"),
					InvoiceUrl = s3Url,
					FileSizeKB = fileSizeKb
				});

				_logger.LogInformation("Successfully generated PDF for invoice record {RecordId}", invoiceRecord.Id);
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error processing invoice PDF for record {RecordId}", invoiceRecord.Id);
				return false;
			}
		}

		// Helper: Generate HTML (SAME as 2025)
		private string GenerateInvoiceHtml2019(InvoiceJsonPayload data)
		{
			var serviceTableRows = new StringBuilder();

			for (int i = 0; i < data.Services.Count; i++)
			{
				var service = data.Services[i];
				serviceTableRows.AppendLine($@"
            <tr>
                <td>{i + 1}</td>
                <td>{service.Name}</td>
                <td>{service.QtyPerDay}</td>
                <td class=""right"">{service.Cost:0.00}</td>
                <td class=""right"">{service.Total:0.00}</td>
            </tr>");
			}

			var formattedDob = !string.IsNullOrEmpty(data.Patient.Dob) ? data.Patient.Dob : "Invalid Date";

			return $@"
<!DOCTYPE html>
<html lang=""en"">
  <head>
    <meta charset=""UTF-8"" />
    <style>
      body {{
        font-family: Arial, Helvetica, sans-serif;
        margin: 40px;
        color: #333;
        background-color: #fff;
      }}
      .header {{
        text-align: center;
        margin-bottom: 20px;
      }}
      .header img {{
        max-height: 130px;
        width: auto;
      }}
      .patient-info {{
        border: 1px solid #e5e7eb;
        border-radius: 8px;
        padding: 16px;
        margin-bottom: 24px;
        font-size: 13px;
        background-color: #fafafa;
      }}
      .patient-info table {{
        width: 100%;
        border-collapse: collapse;
      }}
      .patient-info td {{
        padding: 4px 8px;
        vertical-align: top;
      }}
      .section-title {{
        font-weight: bold;
        margin-bottom: 8px;
        font-size: 14px;
        color: #333;
      }}
      table.invoice {{
        width: 100%;
        border-collapse: collapse;
        margin-top: 8px;
        font-size: 13px;
        border: 1px solid #000;
      }}
      table.invoice th,
      table.invoice td {{
        border: 1px solid #000;
        padding: 10px;
        text-align: left;
      }}
      table.invoice th {{
        background: #f9f9f9;
        font-weight: 600;
        color: #333;
      }}
      table.invoice td.right {{
        text-align: right;
      }}
      .totals {{
        font-weight: bold;
        background-color: #f9f9f9;
      }}
      .totals td {{
        border-top: 2px solid #000;
      }}
      .signature {{
        margin-top: 40px;
        text-align: right;
        font-size: 14px;
        color: #333;
      }}
      .signature .line {{
        margin-bottom: 2px;
        border-top: 1px solid #333;
        width: 116px;
        margin-left: auto;
      }}
      .signature p {{
        font-family: ""Cedarville Cursive"", cursive;
        font-size: 15px;
        color: #1a3c6e;
        margin: 0;
        padding-right: 0px;
      }}
      footer {{
        margin-top: 40px;
        font-size: 15px;
        display: flex;
        justify-content: space-between;
        color: #000;
      }}
    </style>
  </head>
  <body>
    <!-- Header with Logo -->
    <div class=""header"">
      <img src=""https://d7cop3y0lcg80.cloudfront.net/reports/1/0604d10d087f97b877ea0ae85e9494b5df28b6e7_26-09-2025_10-12-19.png"" alt=""ARTHROSE CRANIOFACIAL PAIN & TMJ CENTRE Logo"" />
    </div>

    <!-- Patient Info -->
    <div class=""patient-info"">
      <table>
        <tr>
          <td><strong>Patient Name:</strong> {data.Patient.Name}</td>
          <td><strong>HFID:</strong> {data.Patient.Hfid}</td>
        </tr>
        <tr>
          <td><strong>Gender:</strong> {data.Patient.Gender}</td>
          <td><strong>INVID:</strong> {data.Patient.Invid}</td>
        </tr>
        <tr>
          <td><strong>DOB:</strong> {formattedDob}</td>
          <td><strong>Mobile:</strong> {data.Patient.Mobile}</td>
        </tr>
        <tr>
          <td><strong>Consultant Coach:</strong> {DOCTOR_NAME}</td>
          <td><strong>City:</strong> {data.Patient.City}</td>
        </tr>
      </table>
    </div>

    <!-- Invoice -->
    <div class=""section-title"">Invoice</div>
    <table class=""invoice"">
      <thead>
        <tr>
          <th>S.No.</th>
          <th>Service/Product</th>
          <th>Qty/Day</th>
          <th>Cost (₹)</th>
          <th>Total (₹)</th>
        </tr>
      </thead>
      <tbody>
        {serviceTableRows}
        <tr class=""totals"">
          <td colspan=""4"" class=""right"">Total Cost:</td>
          <td class=""right"">₹{data.TotalCost:0.00}</td>
        </tr>
        <tr class=""totals"">
          <td colspan=""4"" class=""right"">Grand Total:</td>
          <td class=""right"">₹{data.GrandTotal:0.00}</td>
        </tr>
        <tr class=""totals"">
          <td colspan=""4"" class=""right"">paid</td>
          <td class=""right"">₹{data.Paid:0.00}</td>
        </</tr>
        <tr class=""totals"">
          <td colspan=""4"" class=""right"">Balance</td>
          <td class=""right"">₹{(data.GrandTotal - data.Paid):0.00}</td>
        </tr>
      </tbody>
    </table>

    <!-- Signature -->
    <div class=""signature"">
      <p>{DOCTOR_NAME}</p>
      <div class=""line"">{DOCTOR_NAME}</div>
    </div>

    <footer>
      <span>www.arthrosetmjindia.com</span>
      <span>www.hfiles.in</span>
    </footer>
  </body>
</html>";
		}








		// API 3: Import 2020-2024 invoices from Excel/CSV (EXACT SAME LOGIC AS 2019)
		[HttpPost("import-invoices-2020-2024")]
		//[Authorize]
		public async Task<IActionResult> ImportInvoices2020To2024FromExcel([FromForm] InvoiceImportRequest request)
		{
			HttpContext.Items["Log-Category"] = "2020-2024 Invoice Import";
			if (request.ExcelFile == null || request.ExcelFile.Length == 0)
				return BadRequest(ApiResponseFactory.Fail("Excel file is required."));
			if (!Path.GetExtension(request.ExcelFile.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase) &&
				!Path.GetExtension(request.ExcelFile.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
				return BadRequest(ApiResponseFactory.Fail("Only .csv and .xlsx files are supported."));

			var response = new InvoiceImportResponse();
			var transaction = await _clinicRepository.BeginTransactionAsync();
			var committed = false;
			try
			{
				List<ExcelInvoiceRow> invoiceRows;
				if (Path.GetExtension(request.ExcelFile.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
				{
					invoiceRows = await ProcessInvoiceCsvFile2020_2024(request.ExcelFile);
				}
				else
				{
					invoiceRows = await ProcessInvoiceExcelFile2020_2024(request.ExcelFile);
				}

				if (!invoiceRows.Any())
				{
					return BadRequest(ApiResponseFactory.Fail("No valid invoice data found in the file."));
				}

				var groupedInvoices = invoiceRows
					.GroupBy(i => new { i.PatientId, i.ParsedDate.Date, i.InvoiceId })
					.ToList();

				foreach (var group in groupedInvoices)
				{
					response.TotalProcessed++;
					try
					{
						var success = await ProcessInvoiceGroup2020_2024(
							group.Key.PatientId,
							group.Key.Date,
							group.Key.InvoiceId,
							group.ToList(),
							response);
						if (success) response.Successful++;
						else response.Failed++;
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Error processing 2020-2024 invoice group for Patient {PatientId} on {Date} Invoice {InvoiceId}",
							group.Key.PatientId, group.Key.Date, group.Key.InvoiceId);
						response.Failed++;
						response.SkippedReasons.Add($"Patient {group.Key.PatientId} on {group.Key.Date:dd-MM-yyyy} Invoice {group.Key.InvoiceId}: {ex.Message}");
					}
				}

				await transaction.CommitAsync();
				committed = true;

				response.Message = $"2020-2024 Invoice import completed: {response.Successful} successful, " +
								  $"{response.Failed} failed out of {response.TotalProcessed} groups. " +
								  $"Processed {response.PatientsProcessed} patients with {response.VisitsCreated} visits.";

				_logger.LogInformation("2020-2024 Invoice import completed: {Added} added, {Failed} failed",
					response.Successful, response.Failed);

				_cacheService.InvalidateClinicStatistics(CLINIC_ID);
				return Ok(ApiResponseFactory.Success(response, response.Message));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to import 2020-2024 invoices from Excel");
				return StatusCode(500, ApiResponseFactory.Fail("Failed to process file: " + ex.Message));
			}
			finally
			{
				if (!committed && transaction.GetDbTransaction().Connection != null)
					await transaction.RollbackAsync();
			}
		}

		// Helper: CSV → 2020-2024
		private async Task<List<ExcelInvoiceRow>> ProcessInvoiceCsvFile2020_2024(IFormFile file)
		{
			var invoices = new List<ExcelInvoiceRow>();
			using var reader = new StreamReader(file.OpenReadStream());
			var csvContent = await reader.ReadToEndAsync();
			var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

			for (int i = 1; i < lines.Length; i++)
			{
				var columns = lines[i].Split(',');
				if (columns.Length >= 8)
				{
					var invoice = new ExcelInvoiceRow
					{
						PatientName = columns[0].Trim(),
						PatientId = columns[1].Trim(),
						DateString = columns[2].Trim(),
						InvoiceId = columns[3].Trim(),
						ServiceName = columns[4].Trim(),
						Cost = int.TryParse(columns[5].Trim(), out var cost) ? cost : 0,
						QuantityValue = columns[6].Trim(),
						FinalCost = int.TryParse(columns[7].Trim(), out var finalCost) ? finalCost : 0
					};

					if (TryParseDate(invoice.DateString, out var parsedDate))
					{
						invoice.ParsedDate = parsedDate;
						invoices.Add(invoice);
					}
					else
					{
						_logger.LogWarning("Invalid date format (2020-2024) row {Row}: {Date}", i + 1, invoice.DateString);
					}
				}
			}
			return invoices;
		}

		// Helper: XLSX → 2020-2024
		private async Task<List<ExcelInvoiceRow>> ProcessInvoiceExcelFile2020_2024(IFormFile file)
		{
			var invoices = new List<ExcelInvoiceRow>();
			using var stream = new MemoryStream();
			await file.CopyToAsync(stream);
			ExcelPackage.License.SetNonCommercialPersonal("HFiles");
			using var package = new ExcelPackage(stream);
			var worksheet = package.Workbook.Worksheets[0];
			if (worksheet.Dimension == null) return invoices;

			var rowCount = worksheet.Dimension.End.Row;
			for (int row = 2; row <= rowCount; row++)
			{
				var invoice = new ExcelInvoiceRow
				{
					PatientName = worksheet.Cells[row, 1].Text.Trim(),
					PatientId = worksheet.Cells[row, 2].Text.Trim(),
					DateString = worksheet.Cells[row, 3].Text.Trim(),
					InvoiceId = worksheet.Cells[row, 4].Text.Trim(),
					ServiceName = worksheet.Cells[row, 5].Text.Trim(),
					Cost = int.TryParse(worksheet.Cells[row, 6].Text.Trim(), out var cost) ? cost : 0,
					QuantityValue = worksheet.Cells[row, 7].Text.Trim(),
					FinalCost = int.TryParse(worksheet.Cells[row, 8].Text.Trim(), out var finalCost) ? finalCost : 0
				};

				if (TryParseDate(invoice.DateString, out var parsedDate))
				{
					invoice.ParsedDate = parsedDate;
					invoices.Add(invoice);
				}
				else
				{
					_logger.LogWarning("Invalid date format (2020-2024) row {Row}: {Date}", row, invoice.DateString);
				}
			}
			return invoices;
		}

		// Shared date parser (supports all common formats)
		private bool TryParseDate(string dateString, out DateTime parsedDate)
		{
			var formats = new[] { "dd-MM-yyyy", "MM/dd/yyyy", "yyyy-MM-dd", "dd/MM/yyyy", "dd.MM.yyyy" };
			return DateTime.TryParseExact(dateString, formats, null, DateTimeStyles.None, out parsedDate);
		}

		// Process group → 2020-2024 (identical logic to 2019)
		private async Task<bool> ProcessInvoiceGroup2020_2024(
			string patientId,
			DateTime appointmentDate,
			string invoiceId,
			List<ExcelInvoiceRow> invoices,
			InvoiceImportResponse response)
		{
			var user = await _userRepository.GetUserByPatientIdAsync(patientId);
			if (user == null)
			{
				response.SkippedReasons.Add($"Patient {patientId}: User not found");
				return false;
			}

			var fullName = $"{user.FirstName} {user.LastName}".Trim();
			var clinicPatient = await _clinicVisitRepository.GetOrCreatePatientAsync(user.HfId ?? "", fullName);

			var existingVisit = await GetExistingVisitByDate(clinicPatient.Id, appointmentDate);
			ClinicVisit visit;
			if (existingVisit == null)
			{
				visit = new ClinicVisit
				{
					ClinicPatientId = clinicPatient.Id,
					ClinicId = CLINIC_ID,
					AppointmentDate = appointmentDate.Date,
					AppointmentTime = APPOINTMENT_TIME,
					PaymentMethod = null
				};
				await _clinicVisitRepository.SaveVisitAsync(visit);
				response.VisitsCreated++;
			}
			else
			{
				visit = existingVisit;
			}

			var invoiceJsonData = CreateInvoiceJsonData2020_2024(user, clinicPatient, invoices, invoiceId, appointmentDate);

			var existingRecord = await _clinicPatientRecordRepository.GetByCompositeKeyAsync(
				CLINIC_ID, clinicPatient.Id, visit.Id, RecordType.Invoice);

			if (existingRecord != null)
			{
				response.SkippedReasons.Add($"Patient {patientId} on {appointmentDate:dd-MM-yyyy} Invoice {invoiceId}: Already exists");
				return false;
			}

			var invoiceRecord = new ClinicPatientRecord
			{
				ClinicId = CLINIC_ID,
				PatientId = clinicPatient.Id,
				ClinicVisitId = visit.Id,
				Type = RecordType.Invoice,
				JsonData = invoiceJsonData,
				SendToPatient = false
			};
			await _clinicPatientRecordRepository.SaveAsync(invoiceRecord);

			response.PatientsProcessed++;
			response.AddedInvoices.Add(new AddedInvoiceSummary
			{
				PatientId = patientId,
				PatientName = fullName,
				HFID = user.HfId ?? "",
				AppointmentDate = appointmentDate.ToString("dd-MM-yyyy"),
				InvoiceId = invoiceId,
				ServiceCount = invoices.Count,
				// FIX: Use corrected total
				TotalCost = invoices.Sum(i => i.FinalCost > 0 ? i.FinalCost : i.Cost),
				Services = invoices.Select(i => i.ServiceName).ToList()
			});

			return true;
		}

		// JSON payload → IDENTICAL to 2019 (critical for PDF reuse)
		private string CreateInvoiceJsonData2020_2024(
	 Domain.Entities.Users.User user,
	 ClinicPatient clinicPatient,
	 List<ExcelInvoiceRow> invoices,
	 string invoiceId,
	 DateTime appointmentDate)
		{
			var serviceItems = invoices.Select(i => new
			{
				name = i.ServiceName,
				qtyPerDay = i.QuantityValue,
				cost = i.Cost,
				// FIX: Use FinalCost only if > 0, else fallback to Cost
				total = i.FinalCost > 0 ? i.FinalCost : i.Cost
			}).ToArray();

			// FIX: Recalculate total using the corrected logic
			var totalCost = serviceItems.Sum(x => x.total);

			var invoiceData = new
			{
				patient = new
				{
					name = $"{user.FirstName} {user.LastName}".Trim(),
					hfid = user.HfId ?? "",
					gender = user.Gender ?? "",
					invid = invoiceId,
					dob = user.DOB ?? "",
					date = appointmentDate.ToString("dd/MM/yyyy"),
					mobile = user.PhoneNumber ?? "",
					city = user.City ?? ""
				},
				services = serviceItems,
				totalCost = totalCost,
				grandTotal = totalCost,
				paid = totalCost,
				clinicInfo = new
				{
					name = "ARTHROSE",
					subtitle = "CRANIOFACIAL PAIN & TMJ CENTRE",
					website = "www.arthrosetmjindia.com"
				}
			};

			return JsonConvert.SerializeObject(invoiceData, Formatting.None);
		}

		// API 4: Generate PDFs for 2020-2024 invoices (same as 2019)
		[HttpPost("generate-invoice-pdfs-2020-2024")]
		//[Authorize]
		public async Task<IActionResult> GenerateInvoicePdfs2020To2024([FromBody] InvoicePdfRequest request)
		{
			HttpContext.Items["Log-Category"] = "2020-2024 Invoice PDF Generation";
			var response = new InvoicePdfResponse();
			var transaction = await _clinicRepository.BeginTransactionAsync();
			var committed = false;

			try
			{
				var invoiceRecords = await _clinicPatientRecordRepository.GetUnsentInvoiceRecordsAsync(CLINIC_ID);
				if (!invoiceRecords.Any())
				{
					return Ok(ApiResponseFactory.Success(new InvoicePdfResponse { Message = "No unsent invoices." }, "No unsent invoices."));
				}

				await new BrowserFetcher().DownloadAsync();
				using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
				{
					Headless = true,
					Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
				});

				foreach (var record in invoiceRecords)
				{
					response.TotalProcessed++;
					var success = await ProcessSingleInvoicePdf2020_2024(record, browser, response);
					if (success) response.Successful++;
					else response.Failed++;
				}

				await transaction.CommitAsync();
				committed = true;

				response.Message = $"2020-2024 PDF generation: {response.Successful} success, {response.Failed} failed.";
				_logger.LogInformation(response.Message);
				return Ok(ApiResponseFactory.Success(response, response.Message));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "2020-2024 PDF generation failed");
				return StatusCode(500, ApiResponseFactory.Fail("PDF generation failed."));
			}
			finally
			{
				if (!committed && transaction.GetDbTransaction().Connection != null)
					await transaction.RollbackAsync();
			}
		}

		// Reuse SAME HTML generator (2019 version works perfectly)
		private async Task<bool> ProcessSingleInvoicePdf2020_2024(
			ClinicPatientRecord invoiceRecord,
			IBrowser browser,
			InvoicePdfResponse response)
		{
			return await ProcessSingleInvoicePdf2019(invoiceRecord, browser, response); // EXACT SAME LOGIC
		}

		// Optional: Alias if you want separate logging
		// Or just call GenerateInvoiceHtml2019() directly — it's identical
		private string GenerateInvoiceHtml2020_2024(InvoiceJsonPayload data) => GenerateInvoiceHtml2019(data);




		[HttpPost("import-invoices-historical")]
		//[Authorize]
		public async Task<IActionResult> ImportInvoicesHistoricalFromExcel([FromForm] InvoiceHistoricalImportRequest request)
		{
			HttpContext.Items["Log-Category"] = "Historical Invoice Import (2020-2024)";

			if (request.ExcelFile == null || request.ExcelFile.Length == 0)
				return BadRequest(ApiResponseFactory.Fail("Excel file is required."));

			if (!Path.GetExtension(request.ExcelFile.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase) &&
				!Path.GetExtension(request.ExcelFile.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
				return BadRequest(ApiResponseFactory.Fail("Only .csv and .xlsx files are supported."));

			var response = new InvoiceHistoricalImportResponse();
			var transaction = await _clinicRepository.BeginTransactionAsync();
			var committed = false;

			try
			{
				List<ExcelInvoiceHistoricalRow> invoiceRows;

				if (Path.GetExtension(request.ExcelFile.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
				{
					invoiceRows = await ProcessInvoiceCsvHistorical(request.ExcelFile);
				}
				else
				{
					invoiceRows = await ProcessInvoiceExcelHistorical(request.ExcelFile);
				}

				if (!invoiceRows.Any())
				{
					return BadRequest(ApiResponseFactory.Fail("No valid invoice data found in the file."));
				}

				_logger.LogInformation("Processing {Count} invoice rows", invoiceRows.Count);

				// Group by patient, date, and invoice ID
				var groupedInvoices = invoiceRows
					.GroupBy(i => new { i.PatientId, i.ParsedDate.Date, i.InvoiceId })
					.ToList();

				_logger.LogInformation("Grouped into {GroupCount} invoice groups", groupedInvoices.Count);

				foreach (var group in groupedInvoices)
				{
					response.TotalProcessed++;

					try
					{
						var success = await ProcessInvoiceGroupHistorical(
							group.Key.PatientId,
							group.Key.Date,
							group.Key.InvoiceId,
							group.ToList(),
							response);

						if (success) response.Successful++;
						else response.Failed++;
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Error processing invoice group for Patient {PatientId} on {Date} Invoice {InvoiceId}",
							group.Key.PatientId, group.Key.Date, group.Key.InvoiceId);
						response.Failed++;
						response.SkippedReasons.Add($"Patient {group.Key.PatientId} on {group.Key.Date:dd-MM-yyyy} Invoice {group.Key.InvoiceId}: {ex.Message}");
					}
				}

				await transaction.CommitAsync();
				committed = true;

				response.Message = $"Historical invoice import completed: {response.Successful} successful, " +
								  $"{response.Failed} failed out of {response.TotalProcessed} groups. " +
								  $"Processed {response.PatientsProcessed} patients with {response.VisitsCreated} visits.";

				_logger.LogInformation("Historical invoice import completed: {Added} added, {Failed} failed",
					response.Successful, response.Failed);

				_cacheService.InvalidateClinicStatistics(CLINIC_ID);
				return Ok(ApiResponseFactory.Success(response, response.Message));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to import historical invoices");
				return StatusCode(500, ApiResponseFactory.Fail("Failed to process file: " + ex.Message));
			}
			finally
			{
				if (!committed && transaction.GetDbTransaction().Connection != null)
					await transaction.RollbackAsync();
			}
		}

		// ===== Helper: Process CSV File =====

		private async Task<List<ExcelInvoiceHistoricalRow>> ProcessInvoiceCsvHistorical(IFormFile file)
		{
			var invoices = new List<ExcelInvoiceHistoricalRow>();
			using var reader = new StreamReader(file.OpenReadStream());
			var csvContent = await reader.ReadToEndAsync();
			var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

			for (int i = 1; i < lines.Length; i++)  // Skip header
			{
				var columns = lines[i].Split(',');
				if (columns.Length >= 8)
				{
					var invoice = new ExcelInvoiceHistoricalRow
					{
						PatientName = columns[0].Trim(),
						PatientId = columns[1].Trim(),
						DateString = columns[2].Trim(),
						InvoiceId = columns[3].Trim(),
						ServiceName = columns[4].Trim(),
						Cost = int.TryParse(columns[5].Trim(), out var cost) ? cost : 0,
						QuantityValue = columns[6].Trim(),
						QuantityType = columns[7].Trim()
					};

					if (TryParseDate(invoice.DateString, out var parsedDate))
					{
						invoice.ParsedDate = parsedDate;
						invoices.Add(invoice);
					}
					else
					{
						_logger.LogWarning("Invalid date format row {Row}: {Date}", i + 1, invoice.DateString);
					}
				}
			}

			_logger.LogInformation("Parsed {Count} invoice rows from CSV", invoices.Count);
			return invoices;
		}

		// ===== Helper: Process Excel File =====

		private async Task<List<ExcelInvoiceHistoricalRow>> ProcessInvoiceExcelHistorical(IFormFile file)
		{
			var invoices = new List<ExcelInvoiceHistoricalRow>();
			using var stream = new MemoryStream();
			await file.CopyToAsync(stream);

			ExcelPackage.License.SetNonCommercialPersonal("HFiles");
			using var package = new ExcelPackage(stream);
			var worksheet = package.Workbook.Worksheets[0];

			if (worksheet.Dimension == null) return invoices;

			var rowCount = worksheet.Dimension.End.Row;

			for (int row = 2; row <= rowCount; row++)
			{
				var costValue = int.TryParse(worksheet.Cells[row, 6].Text.Trim(), out var cost) ? cost : 0;

				var invoice = new ExcelInvoiceHistoricalRow
				{
					PatientName = worksheet.Cells[row, 1].Text.Trim(),
					PatientId = worksheet.Cells[row, 2].Text.Trim(),
					DateString = worksheet.Cells[row, 3].Text.Trim(),
					InvoiceId = worksheet.Cells[row, 4].Text.Trim(),
					ServiceName = worksheet.Cells[row, 5].Text.Trim(),
					Cost = costValue,  // This is the total amount
					QuantityValue = worksheet.Cells[row, 7].Text.Trim(),
					QuantityType = worksheet.Cells[row, 8].Text.Trim()
				};

				if (TryParseDate(invoice.DateString, out var parsedDate))
				{
					invoice.ParsedDate = parsedDate;
					invoices.Add(invoice);
				}
				else
				{
					_logger.LogWarning("Invalid date format row {Row}: {Date}", row, invoice.DateString);
				}
			}

			_logger.LogInformation("Parsed {Count} invoice rows from Excel", invoices.Count);
			return invoices;
		}

		// NOTE: TryParseDate method already exists in your controller from Treatments/Prescriptions API
		// If you don't have it, add this:
		// private bool TryParseDate(string dateString, out DateTime parsedDate)
		// {
		//     var formats = new[] { "dd-MM-yyyy", "MM/dd/yyyy", "yyyy-MM-dd", "dd/MM/yyyy", "dd.MM.yyyy" };
		//     return DateTime.TryParseExact(dateString, formats, null, DateTimeStyles.None, out parsedDate);
		// }

		// ===== Helper: Process Invoice Group =====

		private async Task<bool> ProcessInvoiceGroupHistorical(
			string patientId,
			DateTime appointmentDate,
			string invoiceId,
			List<ExcelInvoiceHistoricalRow> invoices,
			InvoiceHistoricalImportResponse response)
		{
			// 1. Find user
			var user = await _userRepository.GetUserByPatientIdAsync(patientId);
			if (user == null)
			{
				response.SkippedReasons.Add($"Patient {patientId}: User not found");
				return false;
			}

			// 2. Get or create clinic patient
			var fullName = $"{user.FirstName} {user.LastName}".Trim();
			var clinicPatient = await _clinicVisitRepository.GetOrCreatePatientAsync(user.HfId ?? "", fullName);

			// 3. Get or create visit
			var existingVisit = await _clinicVisitRepository.GetExistingVisitAsync(clinicPatient.Id, appointmentDate);
			ClinicVisit visit;

			if (existingVisit == null)
			{
				visit = new ClinicVisit
				{
					ClinicPatientId = clinicPatient.Id,
					ClinicId = CLINIC_ID,
					AppointmentDate = appointmentDate.Date,
					AppointmentTime = new TimeSpan(0, 0, 0),  // Midnight for historical
					PaymentMethod = null
				};
				await _clinicVisitRepository.SaveVisitAsync(visit);
				response.VisitsCreated++;
			}
			else
			{
				visit = existingVisit;
			}

			// 4. Create invoice JSON
			var invoiceJsonData = CreateInvoiceJsonHistorical(user, invoices, invoiceId, appointmentDate);

			// 5. Check if already exists
			var existingRecord = await _clinicPatientRecordRepository.GetByCompositeKeyAsync(
				CLINIC_ID, clinicPatient.Id, visit.Id, RecordType.Invoice);

			if (existingRecord != null)
			{
				response.SkippedReasons.Add($"Patient {patientId} on {appointmentDate:dd-MM-yyyy} Invoice {invoiceId}: Already exists");
				return false;
			}

			// 6. Create invoice record
			var invoiceRecord = new ClinicPatientRecord
			{
				ClinicId = CLINIC_ID,
				PatientId = clinicPatient.Id,
				ClinicVisitId = visit.Id,
				Type = RecordType.Invoice,
				JsonData = invoiceJsonData,
				SendToPatient = false,
				UniqueRecordId = null  // Historical marker
			};

			await _clinicPatientRecordRepository.SaveAsync(invoiceRecord);

			response.PatientsProcessed++;
			response.AddedInvoices.Add(new AddedInvoiceHistoricalSummary
			{
				PatientId = patientId,
				PatientName = fullName,
				HFID = user.HfId ?? "",
				AppointmentDate = appointmentDate.ToString("dd-MM-yyyy"),
				InvoiceId = invoiceId,
				ServiceCount = invoices.Count,
				TotalCost = invoices.Sum(i => i.Cost),
				Services = invoices.Select(i => i.ServiceName).ToList()
			});

			return true;
		}

		// ===== Helper: Create Invoice JSON =====

		private string CreateInvoiceJsonHistorical(
			Domain.Entities.Users.User user,
			List<ExcelInvoiceHistoricalRow> invoices,
			string invoiceId,
			DateTime appointmentDate)
		{
			// CRITICAL: Use Cost as the total (no separate FinalCost column in Excel)
			var serviceItems = invoices.Select(i => new
			{
				name = i.ServiceName,
				qtyPerDay = i.QuantityValue,
				cost = i.Cost,
				total = i.Cost  // IMPORTANT: Cost IS the total
			}).ToArray();

			var totalCost = invoices.Sum(i => i.Cost);

			var invoiceData = new
			{
				patient = new
				{
					name = $"{user.FirstName} {user.LastName}".Trim(),
					hfid = user.HfId ?? "",
					gender = user.Gender ?? "",
					invid = invoiceId,
					dob = user.DOB ?? "",
					date = appointmentDate.ToString("dd/MM/yyyy"),
					mobile = user.PhoneNumber ?? "",
					city = user.City ?? ""
				},
				services = serviceItems,
				totalCost = totalCost,
				grandTotal = totalCost,
				paid = totalCost,
				clinicInfo = new
				{
					name = "ARTHROSE",
					subtitle = "CRANIOFACIAL PAIN & TMJ CENTRE",
					website = "www.arthrosetmjindia.com"
				}
			};

			return JsonConvert.SerializeObject(invoiceData, Formatting.None);
		}

		// ===== API 2: Generate Invoice PDFs =====

		[HttpPost("generate-invoice-pdfs-historical")]
		//[Authorize]
		public async Task<IActionResult> GenerateInvoicePdfsHistorical([FromBody] InvoiceHistoricalPdfRequest request)
		{
			HttpContext.Items["Log-Category"] = "Historical Invoice PDF Generation (2020-2024)";

			var response = new InvoiceHistoricalPdfResponse();
			var transaction = await _clinicRepository.BeginTransactionAsync();
			var committed = false;

			try
			{
				// Get unsent historical invoices (UniqueRecordId = null)
				var allInvoiceRecords = await _clinicPatientRecordRepository.GetUnsentInvoiceRecordsAsync(CLINIC_ID);
				var historicalInvoices = allInvoiceRecords
					.Where(r => r.UniqueRecordId == null)
					.ToList();

				if (!historicalInvoices.Any())
				{
					return Ok(ApiResponseFactory.Success(new InvoiceHistoricalPdfResponse
					{
						Message = "No unsent historical invoices found."
					}, "No unsent historical invoices found."));
				}

				_logger.LogInformation("Found {Count} unsent historical invoice records", historicalInvoices.Count);

				// Download Chromium once
				await new BrowserFetcher().DownloadAsync();

				using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
				{
					Headless = true,
					Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
				});

				foreach (var record in historicalInvoices)
				{
					response.TotalProcessed++;

					try
					{
						var success = await ProcessSingleInvoicePdfHistorical(record, browser, response);
						if (success) response.Successful++;
						else
						{
							response.Failed++;
							response.FailedRecords.Add(new InvoiceHistoricalPdfFailed
							{
								RecordId = record.Id,
								Reason = "PDF generation or upload failed"
							});
						}
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Error generating PDF for invoice record {RecordId}", record.Id);
						response.Failed++;
						response.FailedRecords.Add(new InvoiceHistoricalPdfFailed
						{
							RecordId = record.Id,
							Reason = $"Processing error: {ex.Message}"
						});
					}
				}

				await transaction.CommitAsync();
				committed = true;

				response.Message = $"Historical invoice PDF generation: {response.Successful} successful, {response.Failed} failed.";
				_logger.LogInformation(response.Message);

				return Ok(ApiResponseFactory.Success(response, response.Message));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Historical invoice PDF generation failed");
				return StatusCode(500, ApiResponseFactory.Fail("PDF generation failed."));
			}
			finally
			{
				if (!committed && transaction.GetDbTransaction().Connection != null)
					await transaction.RollbackAsync();
			}
		}

		// ===== Helper: Process Single Invoice PDF =====

		private async Task<bool> ProcessSingleInvoicePdfHistorical(
			ClinicPatientRecord invoiceRecord,
			IBrowser browser,
			InvoiceHistoricalPdfResponse response)
		{
			try
			{
				// Get related data
				var visit = await _clinicVisitRepository.GetByIdAsync(invoiceRecord.ClinicVisitId);
				var clinicPatient = await _clinicPatientRecordRepository.GetByIdAsync(invoiceRecord.PatientId);
				var user = clinicPatient != null ? await _userRepository.GetUserByHFIDAsync(clinicPatient.HFID) : null;

				if (visit == null || clinicPatient == null || user == null)
				{
					_logger.LogWarning("Missing related data for invoice record {RecordId}", invoiceRecord.Id);
					return false;
				}

				// Parse JSON
				var invoiceData = JsonConvert.DeserializeObject<InvoiceHistoricalJsonPayload>(invoiceRecord.JsonData);
				if (invoiceData?.Patient == null || invoiceData.Services == null)
				{
					_logger.LogWarning("Invalid invoice JSON for record {RecordId}", invoiceRecord.Id);
					return false;
				}

				// Generate HTML (same as 2019)
				var htmlContent = GenerateInvoiceHtmlHistorical(invoiceData);

				// Generate PDF
				using var page = await browser.NewPageAsync();
				await page.SetContentAsync(htmlContent);

				var pdfOptions = new PdfOptions
				{
					Format = PaperFormat.A4,
					PrintBackground = true,
					MarginOptions = new MarginOptions
					{
						Top = "0.5in",
						Bottom = "0.5in",
						Left = "0.5in",
						Right = "0.5in"
					}
				};

				var pdfBytes = await page.PdfDataAsync(pdfOptions);
				if (pdfBytes == null || pdfBytes.Length == 0)
				{
					_logger.LogWarning("PDF generation returned empty bytes for record {RecordId}", invoiceRecord.Id);
					return false;
				}

				// Upload to S3
				var tempFileName = $"invoice_clinic{invoiceRecord.ClinicId}_patient{invoiceRecord.PatientId}_{Guid.NewGuid()}.pdf";
				var tempFilePath = Path.Combine(Path.GetTempPath(), tempFileName);
				await System.IO.File.WriteAllBytesAsync(tempFilePath, pdfBytes);

				var s3Key = $"clinic/{tempFileName}";
				var s3Url = await _s3StorageService.UploadFileToS3(tempFilePath, s3Key);

				if (string.IsNullOrEmpty(s3Url))
				{
					System.IO.File.Delete(tempFilePath);
					_logger.LogWarning("S3 upload failed for record {RecordId}", invoiceRecord.Id);
					return false;
				}

				// Update record
				invoiceRecord.SendToPatient = true;
				await _clinicPatientRecordRepository.UpdateAsync(invoiceRecord);

				// Create user report
				var reportName = $"Arthrose_Invoice_{invoiceData.Patient.Name.Replace(" ", "_")}_{visit.AppointmentDate:dd-MM-yy}";
				var epochTime = new DateTimeOffset(visit.AppointmentDate.Date + visit.AppointmentTime).ToUnixTimeSeconds();
				var fileSizeKb = Math.Round((decimal)pdfBytes.Length / 1024, 2);

				var userReport = new UserReport
				{
					UserId = user.Id,
					ReportName = reportName,
					ReportCategory = 8,  // Invoice category
					ReportUrl = s3Url,
					EpochTime = epochTime,
					FileSize = fileSizeKb,
					UploadedBy = "Clinic",
					ClinicId = CLINIC_ID,
					UserType = "Independent",
					DeletedBy = 0
				};

				await _userRepository.SaveAsync(userReport);

				// Create PDF record
				var pdfRecord = new ClinicPatientRecord
				{
					ClinicId = CLINIC_ID,
					PatientId = invoiceRecord.PatientId,
					ClinicVisitId = invoiceRecord.ClinicVisitId,
					Type = RecordType.Invoice,
					JsonData = JsonConvert.SerializeObject(new { url = s3Url }),
					SendToPatient = true
				};

				await _clinicPatientRecordRepository.SaveAsync(pdfRecord);

				// Clean up
				System.IO.File.Delete(tempFilePath);

				// Add to success
				response.SuccessfulRecords.Add(new InvoiceHistoricalPdfSuccess
				{
					RecordId = invoiceRecord.Id,
					PatientName = invoiceData.Patient.Name,
					HFID = clinicPatient.HFID,
					AppointmentDate = visit.AppointmentDate.ToString("dd-MM-yyyy"),
					InvoiceUrl = s3Url,
					FileSizeKB = fileSizeKb
				});

				_logger.LogInformation("Successfully generated PDF for invoice record {RecordId}", invoiceRecord.Id);
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error processing invoice PDF for record {RecordId}", invoiceRecord.Id);
				return false;
			}
		}

		// ===== Helper: Generate Invoice HTML (EXACT SAME AS 2019) =====

		private string GenerateInvoiceHtmlHistorical(InvoiceHistoricalJsonPayload data)
		{
			var serviceTableRows = new StringBuilder();

			for (int i = 0; i < data.Services.Count; i++)
			{
				var service = data.Services[i];
				serviceTableRows.AppendLine($@"
        <tr>
            <td>{i + 1}</td>
            <td>{service.Name}</td>
            <td>{service.QtyPerDay}</td>
            <td class=""right"">{service.Cost:0.00}</td>
            <td class=""right"">{service.Total:0.00}</td>
        </tr>");
			}

			var formattedDob = !string.IsNullOrEmpty(data.Patient.Dob) ? data.Patient.Dob : "";

			return $@"
<!DOCTYPE html>
<html lang=""en"">
  <head>
    <meta charset=""UTF-8"" />
    <style>
      body {{
        font-family: Arial, Helvetica, sans-serif;
        margin: 40px;
        color: #333;
        background-color: #fff;
      }}
      .header {{
        text-align: center;
        margin-bottom: 20px;
      }}
      .header img {{
        max-height: 130px;
        width: auto;
      }}
      .patient-info {{
        border: 1px solid #e5e7eb;
        border-radius: 8px;
        padding: 16px;
        margin-bottom: 24px;
        font-size: 13px;
        background-color: #fafafa;
      }}
      .patient-info table {{
        width: 100%;
        border-collapse: collapse;
      }}
      .patient-info td {{
        padding: 4px 8px;
        vertical-align: top;
      }}
      .section-title {{
        font-weight: bold;
        margin-bottom: 8px;
        font-size: 14px;
        color: #333;
      }}
      table.invoice {{
        width: 100%;
        border-collapse: collapse;
        margin-top: 8px;
        font-size: 13px;
        border: 1px solid #000;
      }}
      table.invoice th,
      table.invoice td {{
        border: 1px solid #000;
        padding: 10px;
        text-align: left;
      }}
      table.invoice th {{
        background: #f9f9f9;
        font-weight: 600;
        color: #333;
      }}
      table.invoice td.right {{
        text-align: right;
      }}
      .totals {{
        font-weight: bold;
        background-color: #f9f9f9;
      }}
      .totals td {{
        border-top: 2px solid #000;
      }}
      .signature {{
        margin-top: 40px;
        text-align: right;
        font-size: 14px;
        color: #333;
      }}
      .signature .line {{
        margin-bottom: 2px;
        border-top: 1px solid #333;
        width: 116px;
        margin-left: auto;
      }}
      .signature p {{
        font-family: ""Cedarville Cursive"", cursive;
        font-size: 15px;
        color: #1a3c6e;
        margin: 0;
        padding-right: 0px;
      }}
      footer {{
        margin-top: 40px;
        font-size: 15px;
        display: flex;
        justify-content: space-between;
        color: #000;
      }}
    </style>
  </head>
  <body>
    <!-- Header with Logo -->
    <div class=""header"">
      <img src=""https://d7cop3y0lcg80.cloudfront.net/reports/1/0604d10d087f97b877ea0ae85e9494b5df28b6e7_26-09-2025_10-12-19.png"" alt=""ARTHROSE CRANIOFACIAL PAIN & TMJ CENTRE Logo"" />
    </div>

    <!-- Patient Info -->
    <div class=""patient-info"">
      <table>
        <tr>
          <td><strong>Patient Name:</strong> {data.Patient.Name}</td>
          <td><strong>HFID:</strong> {data.Patient.Hfid}</td>
        </tr>
        <tr>
          <td><strong>Gender:</strong> {data.Patient.Gender}</td>
          <td><strong>INVID:</strong> {data.Patient.Invid}</td>
        </tr>
        <tr>
          <td><strong>DOB:</strong> {formattedDob}</td>
          <td><strong>Mobile:</strong> {data.Patient.Mobile}</td>
        </tr>
        <tr>
          <td><strong>Consultant Coach:</strong> {DOCTOR_NAME}</td>
          <td><strong>City:</strong> {data.Patient.City}</td>
        </tr>
      </table>
    </div>

    <!-- Invoice -->
    <div class=""section-title"">Invoice</div>
    <table class=""invoice"">
      <thead>
        <tr>
          <th>S.No.</th>
          <th>Service/Product</th>
          <th>Qty/Day</th>
          <th>Cost (₹)</th>
          <th>Total (₹)</th>
        </tr>
      </thead>
      <tbody>
        {serviceTableRows}
        <tr class=""totals"">
          <td colspan=""4"" class=""right"">Total Cost:</td>
          <td class=""right"">₹{data.TotalCost:0.00}</td>
        </tr>
        <tr class=""totals"">
          <td colspan=""4"" class=""right"">Grand Total:</td>
          <td class=""right"">₹{data.GrandTotal:0.00}</td>
        </tr>
        <tr class=""totals"">
          <td colspan=""4"" class=""right"">paid</td>
          <td class=""right"">₹{data.Paid:0.00}</td>
        </tr>
        <tr class=""totals"">
          <td colspan=""4"" class=""right"">Balance</td>
          <td class=""right"">₹{(data.GrandTotal - data.Paid):0.00}</td>
        </tr>
      </tbody>
    </table>

    <!-- Signature -->
    <div class=""signature"">
      <p>{DOCTOR_NAME}</p>
      <div class=""line"">{DOCTOR_NAME}</div>
    </div>

    <footer>
      <span>www.arthrosetmjindia.com</span>
      <span>www.hfiles.in</span>
    </footer>
  </body>
</html>";
		}







		// ===== 2019 RECEIPT IMPORT API =====

		// API 1: Import 2019 receipts from Excel/CSV
		[HttpPost("import-receipts-2019")]
		//[Authorize]
		public async Task<IActionResult> ImportReceipts2019FromExcel([FromForm] ReceiptImportRequest request)
		{
			HttpContext.Items["Log-Category"] = "2019 Receipt Import";

			if (request.ExcelFile == null || request.ExcelFile.Length == 0)
				return BadRequest(ApiResponseFactory.Fail("Excel file is required."));

			if (!Path.GetExtension(request.ExcelFile.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase) &&
				!Path.GetExtension(request.ExcelFile.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
				return BadRequest(ApiResponseFactory.Fail("Only .csv and .xlsx files are supported."));

			var response = new ReceiptImportResponse();
			var transaction = await _clinicRepository.BeginTransactionAsync();
			var committed = false;

			try
			{
				List<ExcelReceiptRow> receiptRows;

				if (Path.GetExtension(request.ExcelFile.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
				{
					receiptRows = await ProcessReceiptCsvFile2019(request.ExcelFile);
				}
				else
				{
					receiptRows = await ProcessReceiptExcelFile2019(request.ExcelFile);
				}

				if (!receiptRows.Any())
				{
					return BadRequest(ApiResponseFactory.Fail("No valid receipt data found in the file."));
				}

				// Group receipts by patient, date, and receipt ID
				var groupedReceipts = receiptRows
					.GroupBy(r => new { r.PatientId, r.ParsedDate.Date, r.ReceiptId })
					.ToList();

				foreach (var group in groupedReceipts)
				{
					response.TotalProcessed++;

					try
					{
						var success = await ProcessReceiptGroup2019(
							group.Key.PatientId,
							group.Key.Date,
							group.Key.ReceiptId,
							group.ToList(),
							response);

						if (success)
						{
							response.Successful++;
						}
						else
						{
							response.Failed++;
						}
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Error processing receipt group for Patient {PatientId} on {Date} Receipt {ReceiptId}",
							group.Key.PatientId, group.Key.Date, group.Key.ReceiptId);
						response.Failed++;
						response.SkippedReasons.Add($"Patient {group.Key.PatientId} on {group.Key.Date:dd-MM-yyyy} Receipt {group.Key.ReceiptId}: Processing error - {ex.Message}");
					}
				}

				await transaction.CommitAsync();
				committed = true;

				response.Message = $"2019 Receipt import completed: {response.Successful} successful, " +
								  $"{response.Failed} failed out of {response.TotalProcessed} total receipt groups. " +
								  $"Processed {response.PatientsProcessed} patients with {response.VisitsCreated} visits.";

				_logger.LogInformation("2019 Receipt import completed: {Added} added, {Failed} failed",
					response.Successful, response.Failed);

				// Invalidate cache
				_cacheService.InvalidateClinicStatistics(CLINIC_ID);

				return Ok(ApiResponseFactory.Success(response, response.Message));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to import 2019 receipts from Excel");
				return StatusCode(500, ApiResponseFactory.Fail("Failed to process Excel file: " + ex.Message));
			}
			finally
			{
				if (!committed && transaction.GetDbTransaction().Connection != null)
					await transaction.RollbackAsync();
			}
		}

		// Helper: Process CSV file for 2019 receipts
		private async Task<List<ExcelReceiptRow>> ProcessReceiptCsvFile2019(IFormFile file)
		{
			var receipts = new List<ExcelReceiptRow>();

			using var reader = new StreamReader(file.OpenReadStream());
			var csvContent = await reader.ReadToEndAsync();
			var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

			for (int i = 1; i < lines.Length; i++) // Skip header
			{
				var columns = lines[i].Split(',');
				if (columns.Length >= 7)
				{
					var receipt = new ExcelReceiptRow
					{
						PatientName = columns[0].Trim(),
						PatientId = columns[1].Trim(),
						DateString = columns[2].Trim(),
						ReceiptId = columns[3].Trim(),
						InvoiceId = columns[4].Trim(),
						ModeOfPayment = columns[5].Trim(),
						AmountPaid = int.TryParse(columns[6].Trim(), out var amountPaid) ? amountPaid : 0
					};

					// Try multiple date formats
					if (DateTime.TryParseExact(receipt.DateString, "dd-MM-yyyy", null, DateTimeStyles.None, out var parsedDate) ||
						DateTime.TryParseExact(receipt.DateString, "MM/dd/yyyy", null, DateTimeStyles.None, out parsedDate) ||
						DateTime.TryParseExact(receipt.DateString, "yyyy-MM-dd", null, DateTimeStyles.None, out parsedDate))
					{
						receipt.ParsedDate = parsedDate;
						receipts.Add(receipt);
					}
					else
					{
						_logger.LogWarning("Invalid date format for row {Row}: {DateString}", i + 1, receipt.DateString);
					}
				}
			}

			return receipts;
		}

		// Helper: Process Excel file for 2019 receipts
		private async Task<List<ExcelReceiptRow>> ProcessReceiptExcelFile2019(IFormFile file)
		{
			var receipts = new List<ExcelReceiptRow>();

			using var stream = new MemoryStream();
			await file.CopyToAsync(stream);

			ExcelPackage.License.SetNonCommercialPersonal("HFiles");
			using var package = new ExcelPackage(stream);
			var worksheet = package.Workbook.Worksheets[0];

			if (worksheet.Dimension == null) return receipts;

			var rowCount = worksheet.Dimension.End.Row;

			for (int row = 2; row <= rowCount; row++)
			{
				var receipt = new ExcelReceiptRow
				{
					PatientName = worksheet.Cells[row, 1].Text.Trim(),
					PatientId = worksheet.Cells[row, 2].Text.Trim(),
					DateString = worksheet.Cells[row, 3].Text.Trim(),
					ReceiptId = worksheet.Cells[row, 4].Text.Trim(),
					InvoiceId = worksheet.Cells[row, 5].Text.Trim(),
					ModeOfPayment = worksheet.Cells[row, 6].Text.Trim(),
					AmountPaid = int.TryParse(worksheet.Cells[row, 7].Text.Trim(), out var amountPaid) ? amountPaid : 0
				};

				// Try multiple date formats
				if (DateTime.TryParseExact(receipt.DateString, "dd-MM-yyyy", null, DateTimeStyles.None, out var parsedDate) ||
					DateTime.TryParseExact(receipt.DateString, "MM/dd/yyyy", null, DateTimeStyles.None, out parsedDate) ||
					DateTime.TryParseExact(receipt.DateString, "yyyy-MM-dd", null, DateTimeStyles.None, out parsedDate))
				{
					receipt.ParsedDate = parsedDate;
					receipts.Add(receipt);
				}
				else
				{
					_logger.LogWarning("Invalid date format for row {Row}: {DateString}", row, receipt.DateString);
				}
			}

			return receipts;
		}

		// Helper: Process receipt group for 2019
		private async Task<bool> ProcessReceiptGroup2019(
			string patientId,
			DateTime appointmentDate,
			string receiptId,
			List<ExcelReceiptRow> receipts,
			ReceiptImportResponse response)
		{
			// 1. Find user by patientId
			var user = await _userRepository.GetUserByPatientIdAsync(patientId);
			if (user == null)
			{
				response.SkippedReasons.Add($"Patient {patientId}: User not found in database");
				return false;
			}

			// 2. Get or create clinic patient
			var fullName = $"{user.FirstName} {user.LastName}".Trim();
			var clinicPatient = await _clinicVisitRepository.GetOrCreatePatientAsync(user.HfId ?? "", fullName);

			// 3. Check if visit exists for this date
			var existingVisit = await GetExistingVisitByDate(clinicPatient.Id, appointmentDate);
			ClinicVisit visit;

			if (existingVisit == null)
			{
				// Parse payment method from receipt data
				var firstReceipt = receipts.First();
				var paymentMethod = ParsePaymentMethod2019(firstReceipt.ModeOfPayment);

				// Create new visit
				visit = new ClinicVisit
				{
					ClinicPatientId = clinicPatient.Id,
					ClinicId = CLINIC_ID,
					AppointmentDate = appointmentDate.Date,
					AppointmentTime = APPOINTMENT_TIME,
					PaymentMethod = paymentMethod
				};

				await _clinicVisitRepository.SaveVisitAsync(visit);
				response.VisitsCreated++;
			}
			else
			{
				visit = existingVisit;

				// Always update payment method from receipt data
				var firstReceipt = receipts.First();
				var parsedPaymentMethod = ParsePaymentMethod2019(firstReceipt.ModeOfPayment);

				// Update if payment method is different or not set
				if (visit.PaymentMethod != parsedPaymentMethod)
				{
					visit.PaymentMethod = parsedPaymentMethod;
					await _clinicVisitRepository.UpdateAsync(visit);
				}
			}

			// 4. Create receipt JSON data (same format as 2025)
			var receiptJsonData = CreateReceiptJsonData2019(user, clinicPatient, receipts, receiptId, appointmentDate);

			// 5. Check if receipt record already exists
			var existingRecord = await _clinicPatientRecordRepository.GetByCompositeKeyAsync(
				CLINIC_ID, clinicPatient.Id, visit.Id, RecordType.Receipt);

			if (existingRecord != null)
			{
				response.SkippedReasons.Add($"Patient {patientId} on {appointmentDate:dd-MM-yyyy} Receipt {receiptId}: Receipt record already exists");
				return false;
			}

			// 6. Create receipt record
			var receiptRecord = new ClinicPatientRecord
			{
				ClinicId = CLINIC_ID,
				PatientId = clinicPatient.Id,
				ClinicVisitId = visit.Id,
				Type = RecordType.Receipt,
				JsonData = receiptJsonData,
				SendToPatient = false
			};

			await _clinicPatientRecordRepository.SaveAsync(receiptRecord);

			response.PatientsProcessed++;
			response.AddedReceipts.Add(new AddedReceiptSummary
			{
				PatientId = patientId,
				PatientName = fullName,
				HFID = user.HfId ?? "",
				AppointmentDate = appointmentDate.ToString("dd-MM-yyyy"),
				ReceiptId = receiptId,
				InvoiceId = receipts.First().InvoiceId,
				ModeOfPayment = receipts.First().ModeOfPayment,
				AmountPaid = receipts.Sum(r => r.AmountPaid)
			});

			return true;
		}

		// Helper: Parse payment method (SAME as 2025)
		private PaymentMethod? ParsePaymentMethod2019(string modeOfPayment)
		{
			if (string.IsNullOrWhiteSpace(modeOfPayment))
				return null;

			return modeOfPayment.ToUpper().Trim() switch
			{
				"CARD" => PaymentMethod.DebitCard,
				"DEBIT CARD" => PaymentMethod.DebitCard,
				"CREDIT CARD" => PaymentMethod.CreditCard,
				"CASH" => PaymentMethod.Cash,
				"WALLET" => PaymentMethod.Wallet,
				"BANK TRANSFER" => PaymentMethod.BankTransfer,
				"CHECK" => PaymentMethod.Check,
				"CHEQUE" => PaymentMethod.Check,
				_ => null // Unknown payment method, store as null
			};
		}

		// Helper: Create receipt JSON data (SAME as 2025)
		private string CreateReceiptJsonData2019(
			Domain.Entities.Users.User user,
			ClinicPatient clinicPatient,
			List<ExcelReceiptRow> receipts,
			string receiptId,
			DateTime appointmentDate)
		{
			var firstReceipt = receipts.First();
			var totalAmount = receipts.Sum(r => r.AmountPaid);

			// Convert payment mode
			var modeOfPayment = firstReceipt.ModeOfPayment.ToUpper() switch
			{
				"CARD" => "Debit Card",
				"CASH" => "Cash",
				"WALLET" => "Wallet",
				_ => firstReceipt.ModeOfPayment
			};

			var serviceItems = receipts.Select(r => new
			{
				name = "Service",
				qtyPerDay = "1 QTY",
				cost = r.AmountPaid,
				total = r.AmountPaid,
				ModeOfPayment = modeOfPayment,
				ChequeNo = "--"
			}).ToArray();

			var receiptData = new
			{
				patient = new
				{
					name = $"{user.FirstName} {user.LastName}".Trim(),
					uhid = user.HfId ?? "",
					gender = user.Gender ?? "",
					receiptId = receiptId,
					dob = user.DOB ?? "",
					doctor = DOCTOR_NAME,
					mobile = user.PhoneNumber ?? "",
					city = user.City ?? ""
				},
				receipt = new
				{
					date = appointmentDate.ToString("dd/MM/yyyy"),
					receiptNumber = receiptId,
					modeOfPayment = modeOfPayment,
					chequeNo = "--",
					amountPaid = totalAmount,
					amountInWords = ConvertAmountToWords2019(totalAmount)
				},
				services = serviceItems,
				clinicInfo = new
				{
					name = "Arthrose",
					subtitle = "CRANIOFACIAL PAIN & TMJ CENTRE",
					website = "www.arthrosetmjindia.com"
				}
			};

			return JsonConvert.SerializeObject(receiptData, Formatting.None);
		}

		// Helper: Convert amount to words (SAME as 2025)
		private string ConvertAmountToWords2019(int amount)
		{
			// Simple implementation - you can enhance this with a proper number-to-words converter
			return $"{amount} Only";
		}

		// API 2: Generate receipt PDFs for 2019
		[HttpPost("generate-receipt-pdfs-2019")]
		//[Authorize]
		public async Task<IActionResult> GenerateReceiptPdfs2019([FromBody] ReceiptPdfRequest request)
		{
			HttpContext.Items["Log-Category"] = "2019 Receipt PDF Generation";

			var response = new ReceiptPdfResponse();
			var transaction = await _clinicRepository.BeginTransactionAsync();
			var committed = false;

			try
			{
				// Get all unsent receipt records for CLINIC_ID = 8
				var receiptRecords = await _clinicPatientRecordRepository.GetUnsentReceiptRecordsAsync(CLINIC_ID);

				if (!receiptRecords.Any())
				{
					return Ok(ApiResponseFactory.Success(new ReceiptPdfResponse
					{
						TotalProcessed = 0,
						Successful = 0,
						Failed = 0,
						Message = "No unsent receipt records found."
					}, "No unsent receipt records found."));
				}

				_logger.LogInformation("Found {Count} unsent receipt records", receiptRecords.Count);

				// Download Chromium once for all PDFs
				await new BrowserFetcher().DownloadAsync();

				using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
				{
					Headless = true,
					Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
				});

				foreach (var receiptRecord in receiptRecords)
				{
					response.TotalProcessed++;

					try
					{
						var success = await ProcessSingleReceiptPdf2019(receiptRecord, browser, response);
						if (success)
						{
							response.Successful++;
						}
						else
						{
							response.Failed++;
							response.FailedRecords.Add(new ReceiptPdfFailed
							{
								RecordId = receiptRecord.Id,
								Reason = "PDF generation or upload failed"
							});
						}
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Error generating PDF for receipt record {RecordId}", receiptRecord.Id);
						response.Failed++;
						response.FailedRecords.Add(new ReceiptPdfFailed
						{
							RecordId = receiptRecord.Id,
							Reason = $"Processing error: {ex.Message}"
						});
					}
				}

				await transaction.CommitAsync();
				committed = true;

				response.Message = $"Receipt PDF generation completed: {response.Successful} successful, " +
								  $"{response.Failed} failed out of {response.TotalProcessed} total receipts.";

				_logger.LogInformation("Receipt PDF generation completed. Success: {Success}, Failed: {Failed}",
					response.Successful, response.Failed);

				return Ok(ApiResponseFactory.Success(response, response.Message));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during receipt PDF generation");
				return StatusCode(500, ApiResponseFactory.Fail("An error occurred during PDF generation."));
			}
			finally
			{
				if (!committed && transaction.GetDbTransaction().Connection != null)
					await transaction.RollbackAsync();
			}
		}

		// Helper: Process single receipt PDF (SAME naming as 2025)
		private async Task<bool> ProcessSingleReceiptPdf2019(
			ClinicPatientRecord receiptRecord,
			IBrowser browser,
			ReceiptPdfResponse response)
		{
			try
			{
				// Get related data
				var visit = await _clinicVisitRepository.GetByIdAsync(receiptRecord.ClinicVisitId);
				var clinicPatient = await _clinicPatientRecordRepository.GetByIdAsync(receiptRecord.PatientId);
				var user = clinicPatient != null ? await _userRepository.GetUserByHFIDAsync(clinicPatient.HFID) : null;

				if (visit == null || clinicPatient == null || user == null)
				{
					_logger.LogWarning("Missing related data for receipt record {RecordId}", receiptRecord.Id);
					return false;
				}

				// Parse receipt JSON
				var receiptData = JsonConvert.DeserializeObject<ReceiptJsonPayload>(receiptRecord.JsonData);
				if (receiptData?.Patient == null || receiptData.Receipt == null)
				{
					_logger.LogWarning("Invalid receipt JSON data for record {RecordId}", receiptRecord.Id);
					return false;
				}

				// Generate HTML and PDF
				var htmlContent = GenerateReceiptHtml2019(receiptData);

				using var page = await browser.NewPageAsync();
				await page.SetContentAsync(htmlContent);

				var pdfOptions = new PdfOptions
				{
					Format = PaperFormat.A4,
					PrintBackground = true,
					MarginOptions = new MarginOptions
					{
						Top = "0.5in",
						Bottom = "0.5in",
						Left = "0.5in",
						Right = "0.5in"
					}
				};

				var pdfBytes = await page.PdfDataAsync(pdfOptions);
				if (pdfBytes == null || pdfBytes.Length == 0)
				{
					_logger.LogWarning("PDF generation returned empty bytes for record {RecordId}", receiptRecord.Id);
					return false;
				}

				// Upload to S3 (same path structure as 2025)
				var tempFileName = $"receipt_clinic{receiptRecord.ClinicId}_patient{receiptRecord.PatientId}_{Guid.NewGuid()}.pdf";
				var tempFilePath = Path.Combine(Path.GetTempPath(), tempFileName);
				await System.IO.File.WriteAllBytesAsync(tempFilePath, pdfBytes);

				var s3Key = $"clinic/{tempFileName}";
				var s3Url = await _s3StorageService.UploadFileToS3(tempFilePath, s3Key);

				if (string.IsNullOrEmpty(s3Url))
				{
					System.IO.File.Delete(tempFilePath);
					_logger.LogWarning("S3 upload failed for record {RecordId}", receiptRecord.Id);
					return false;
				}

				// Update receipt record to mark as sent
				receiptRecord.SendToPatient = true;
				await _clinicPatientRecordRepository.UpdateAsync(receiptRecord);

				// Create user report (SAME naming as 2025)
				var reportName = $"Arthrose_Receipt_{receiptData.Patient.Name.Replace(" ", "_")}_{visit.AppointmentDate:dd-MM-yy}";
				var epochTime = new DateTimeOffset(visit.AppointmentDate.Date + visit.AppointmentTime).ToUnixTimeSeconds();
				var fileSizeKb = Math.Round((decimal)pdfBytes.Length / 1024, 2);

				var userReport = new UserReport
				{
					UserId = user.Id,
					ReportName = reportName,
					ReportCategory = 8, // Receipt category
					ReportUrl = s3Url,
					EpochTime = epochTime,
					FileSize = fileSizeKb,
					UploadedBy = "Clinic",
					ClinicId = 8,
					UserType = "Independent",
					DeletedBy = 0
				};

				await _userRepository.SaveAsync(userReport);

				// Create another clinic patient record with the PDF URL
				var pdfRecord = new ClinicPatientRecord
				{
					ClinicId = CLINIC_ID,
					PatientId = receiptRecord.PatientId,
					ClinicVisitId = receiptRecord.ClinicVisitId,
					Type = RecordType.Receipt,
					JsonData = JsonConvert.SerializeObject(new { url = s3Url }),
					SendToPatient = true
				};

				await _clinicPatientRecordRepository.SaveAsync(pdfRecord);

				// Clean up
				System.IO.File.Delete(tempFilePath);

				// Add to successful records
				response.SuccessfulRecords.Add(new ReceiptPdfSuccess
				{
					RecordId = receiptRecord.Id,
					PatientName = receiptData.Patient.Name,
					HFID = clinicPatient.HFID,
					AppointmentDate = visit.AppointmentDate.ToString("dd-MM-yyyy"),
					ReceiptUrl = s3Url,
					FileSizeKB = fileSizeKb
				});

				_logger.LogInformation("Successfully generated PDF for receipt record {RecordId}", receiptRecord.Id);
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error processing receipt PDF for record {RecordId}", receiptRecord.Id);
				return false;
			}
		}

		// Helper: Generate HTML (SAME as 2025)
		private string GenerateReceiptHtml2019(ReceiptJsonPayload data)
		{
			var formattedDob = !string.IsNullOrEmpty(data.Patient.Dob) ? data.Patient.Dob : "Invalid Date";

			return $@"
<!DOCTYPE html>
<html lang=""en"">
  <head>
    <meta charset=""UTF-8"" />
    <style>
      body {{
        font-family: Arial, Helvetica, sans-serif;
        margin: 40px;
        color: #333;
        background-color: #fff;
      }}
      .header {{
        text-align: center;
        margin-bottom: 20px;
      }}
      .header img {{
        max-height: 130px;
        width: auto;
      }}
      .patient-info {{
        border: 1px solid #e5e7eb;
        border-radius: 8px;
        padding: 16px;
        margin-bottom: 24px;
        font-size: 13px;
        background-color: #fafafa;
      }}
      .patient-info table {{
        width: 100%;
        border-collapse: collapse;
      }}
      .patient-info td {{
        padding: 4px 8px;
        vertical-align: top;
      }}
      .section-title {{
        font-weight: bold;
        margin-bottom: 8px;
        font-size: 14px;
        color: #333;
      }}
      table.receipt {{
        width: 100%;
        border-collapse: collapse;
        margin-top: 8px;
        font-size: 13px;
        border: 1px solid #000;
      }}
      table.receipt th,
      table.receipt td {{
        border: 1px solid #000;
        padding: 10px;
        text-align: left;
      }}
      table.receipt th {{
        background: #f9f9f9;
        font-weight: 600;
        color: #333;
      }}
      table.receipt td.right {{
        text-align: right;
      }}
      table.receipt td.center {{
        text-align: center;
      }}
      .amount-paid {{
        color: #28a745;
        font-weight: bold;
      }}
      .receipt-details {{
        margin-top: 20px;
        border: 1px solid #28a745;
        border-left: 4px solid #28a745;
        padding: 15px;
        background-color: #f8f9fa;
        font-size: 14px;
      }}
      .receipt-details .thanks {{
        margin-bottom: 10px;
      }}
      .receipt-details .amount {{
        font-weight: bold;
      }}
      .signature {{
        margin-top: 40px;
        text-align: right;
        font-size: 14px;
        color: #333;
      }}
      .signature .line {{
        margin-bottom: 2px;
        border-top: 1px solid #333;
        width: 116px;
        margin-left: auto;
      }}
      .signature p {{
        font-family: ""Cedarville Cursive"", cursive;
        font-size: 15px;
        color: #1a3c6e;
        margin: 0;
        padding-right: 0px;
      }}
      footer {{
        margin-top: 40px;
        font-size: 15px;
        display: flex;
        justify-content: space-between;
        color: #000;
      }}
    </style>
  </head>
  <body>
    <!-- Header with Logo -->
    <div class=""header"">
      <img src=""https://d7cop3y0lcg80.cloudfront.net/reports/1/0604d10d087f97b877ea0ae85e9494b5df28b6e7_26-09-2025_10-12-19.png"" alt=""ARTHROSE CRANIOFACIAL PAIN & TMJ CENTRE Logo"" />
    </div>

    <!-- Patient Info -->
    <div class=""patient-info"">
      <table>
        <tr>
          <td><strong>Patient Name:</strong> {data.Patient.Name}</td>
          <td><strong>HFID:</strong> {data.Patient.Uhid}</td>
        </tr>
        <tr>
          <td><strong>Gender:</strong> {data.Patient.Gender}</td>
          <td><strong>RCID:</strong> {data.Patient.ReceiptId}</td>
        </tr>
        <tr>
          <td><strong>DOB:</strong> {formattedDob}</td>
          <td><strong>Mobile:</strong> {data.Patient.Mobile}</td>
        </tr>
        <tr>
          <td><strong>Consultant Coach:</strong> {data.Patient.Doctor}</td>
          <td><strong>City:</strong> {data.Patient.City}</td>
        </tr>
      </table>
    </div>

    <!-- Receipt -->
    <div class=""section-title"">Receipt</div>
    <table class=""receipt"">
      <thead>
        <tr>
          <th>Date</th>
          <th>Receipt Number</th>
          <th>Mode Of Payment</th>
          <th>Cheque No.</th>
          <th>Amount Paid (₹)</th>
        </tr>
      </thead>
      <tbody>
        <tr>
          <td>{data.Receipt.Date}</td>
          <td>{data.Receipt.ReceiptNumber}</td>
          <td>{data.Receipt.ModeOfPayment}</td>
          <td class=""center"">{data.Receipt.ChequeNo}</td>
          <td class=""right amount-paid"">₹{data.Receipt.AmountPaid:0.00}</td>
        </tr>
      </tbody>
    </table>

    <!-- Receipt Details -->
    <div class=""receipt-details"">
      <div class=""thanks"">
        <strong>Received with thanks from:</strong> {data.Patient.Name}
      </div>
      <div class=""amount"">
        <strong>The sum of Rupees:</strong> {data.Receipt.AmountInWords}
        <span style=""color: #28a745"">(₹{data.Receipt.AmountPaid:0.00})</span> /-
      </div>
    </div>

    <!-- Signature -->
    <div class=""signature"">
      <p>{DOCTOR_NAME}</p>
      <div class=""line"">{DOCTOR_NAME}</div>
    </div>

    <footer>
      <span>www.arthrosetmjindia.com</span>
      <span>www.hfiles.in</span>
    </footer>
  </body>
</html>";
		}









		// API 1: Import 2020-2024 receipts from Excel/CSV
		[HttpPost("import-receipts-2020-2024")]
		//[Authorize]
		public async Task<IActionResult> ImportReceipts2020To2024FromExcel([FromForm] ReceiptImportRequest request)
		{
			HttpContext.Items["Log-Category"] = "2020-2024 Receipt Import";

			if (request.ExcelFile == null || request.ExcelFile.Length == 0)
				return BadRequest(ApiResponseFactory.Fail("Excel file is required."));

			if (!Path.GetExtension(request.ExcelFile.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase) &&
				!Path.GetExtension(request.ExcelFile.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
				return BadRequest(ApiResponseFactory.Fail("Only .csv and .xlsx files are supported."));

			var response = new ReceiptImportResponse();
			var transaction = await _clinicRepository.BeginTransactionAsync();
			var committed = false;

			try
			{
				List<ExcelReceiptRow> receiptRows;

				if (Path.GetExtension(request.ExcelFile.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
				{
					receiptRows = await ProcessReceiptCsvFile2020To2024(request.ExcelFile);
				}
				else
				{
					receiptRows = await ProcessReceiptExcelFile2020To2024(request.ExcelFile);
				}

				if (!receiptRows.Any())
				{
					return BadRequest(ApiResponseFactory.Fail("No valid receipt data found in the file."));
				}

				// Group receipts by patient, date, and receipt ID
				var groupedReceipts = receiptRows
					.GroupBy(r => new { r.PatientId, r.ParsedDate.Date, r.ReceiptId })
					.ToList();

				foreach (var group in groupedReceipts)
				{
					response.TotalProcessed++;

					try
					{
						var success = await ProcessReceiptGroup2020To2024(
							group.Key.PatientId,
							group.Key.Date,
							group.Key.ReceiptId,
							group.ToList(),
							response);

						if (success) response.Successful++;
						else response.Failed++;
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Error processing 2020-2024 receipt group for Patient {PatientId} on {Date} Receipt {ReceiptId}",
							group.Key.PatientId, group.Key.Date, group.Key.ReceiptId);
						response.Failed++;
						response.SkippedReasons.Add($"Patient {group.Key.PatientId} on {group.Key.Date:dd-MM-yyyy} Receipt {group.Key.ReceiptId}: {ex.Message}");
					}
				}

				await transaction.CommitAsync();
				committed = true;

				response.Message = $"2020-2024 Receipt import completed: {response.Successful} successful, " +
								  $"{response.Failed} failed out of {response.TotalProcessed} total receipt groups. " +
								  $"Processed {response.PatientsProcessed} patients with {response.VisitsCreated} visits.";

				_logger.LogInformation("2020-2024 Receipt import completed: {Added} added, {Failed} failed",
					response.Successful, response.Failed);

				// Invalidate cache
				_cacheService.InvalidateClinicStatistics(CLINIC_ID);

				return Ok(ApiResponseFactory.Success(response, response.Message));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to import 2020-2024 receipts from Excel");
				return StatusCode(500, ApiResponseFactory.Fail("Failed to process Excel file: " + ex.Message));
			}
			finally
			{
				if (!committed && transaction.GetDbTransaction().Connection != null)
					await transaction.RollbackAsync();
			}
		}

		// Helper: Process CSV file for 2020-2024 receipts
		private async Task<List<ExcelReceiptRow>> ProcessReceiptCsvFile2020To2024(IFormFile file)
		{
			var receipts = new List<ExcelReceiptRow>();

			using var reader = new StreamReader(file.OpenReadStream());
			var csvContent = await reader.ReadToEndAsync();
			var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

			for (int i = 1; i < lines.Length; i++) // Skip header
			{
				var columns = lines[i].Split(',');
				if (columns.Length >= 7)
				{
					var receipt = new ExcelReceiptRow
					{
						PatientName = columns[0].Trim(),
						PatientId = columns[1].Trim(),
						DateString = columns[2].Trim(),
						ReceiptId = columns[3].Trim(),
						InvoiceId = columns[4].Trim(),
						ModeOfPayment = columns[5].Trim(),
						AmountPaid = int.TryParse(columns[6].Trim(), out var amountPaid) ? amountPaid : 0
					};

					// Try multiple date formats
					if (TryParseDate(receipt.DateString, out var parsedDate))
					{
						receipt.ParsedDate = parsedDate;
						receipts.Add(receipt);
					}
					else
					{
						_logger.LogWarning("Invalid date format for 2020-2024 receipt row {Row}: {DateString}", i + 1, receipt.DateString);
					}
				}
			}

			return receipts;
		}

		// Helper: Process Excel file for 2020-2024 receipts
		private async Task<List<ExcelReceiptRow>> ProcessReceiptExcelFile2020To2024(IFormFile file)
		{
			var receipts = new List<ExcelReceiptRow>();

			using var stream = new MemoryStream();
			await file.CopyToAsync(stream);

			ExcelPackage.License.SetNonCommercialPersonal("HFiles");
			using var package = new ExcelPackage(stream);
			var worksheet = package.Workbook.Worksheets[0];

			if (worksheet.Dimension == null) return receipts;

			var rowCount = worksheet.Dimension.End.Row;

			for (int row = 2; row <= rowCount; row++)
			{
				var receipt = new ExcelReceiptRow
				{
					PatientName = worksheet.Cells[row, 1].Text.Trim(),
					PatientId = worksheet.Cells[row, 2].Text.Trim(),
					DateString = worksheet.Cells[row, 3].Text.Trim(),
					ReceiptId = worksheet.Cells[row, 4].Text.Trim(),
					InvoiceId = worksheet.Cells[row, 5].Text.Trim(),
					ModeOfPayment = worksheet.Cells[row, 6].Text.Trim(),
					AmountPaid = int.TryParse(worksheet.Cells[row, 7].Text.Trim(), out var amountPaid) ? amountPaid : 0
				};

				// Try multiple date formats
				if (TryParseDate(receipt.DateString, out var parsedDate))
				{
					receipt.ParsedDate = parsedDate;
					receipts.Add(receipt);
				}
				else
				{
					_logger.LogWarning("Invalid date format for 2020-2024 receipt row {Row}: {DateString}", row, receipt.DateString);
				}
			}

			return receipts;
		}

		// Helper: Process receipt group for 2020-2024 (IDENTICAL to 2019)
		private async Task<bool> ProcessReceiptGroup2020To2024(
			string patientId,
			DateTime appointmentDate,
			string receiptId,
			List<ExcelReceiptRow> receipts,
			ReceiptImportResponse response)
		{
			// 1. Find user by patientId
			var user = await _userRepository.GetUserByPatientIdAsync(patientId);
			if (user == null)
			{
				response.SkippedReasons.Add($"Patient {patientId}: User not found in database");
				return false;
			}

			// 2. Get or create clinic patient
			var fullName = $"{user.FirstName} {user.LastName}".Trim();
			var clinicPatient = await _clinicVisitRepository.GetOrCreatePatientAsync(user.HfId ?? "", fullName);

			// 3. Check if visit exists for this date
			var existingVisit = await GetExistingVisitByDate(clinicPatient.Id, appointmentDate);
			ClinicVisit visit;

			if (existingVisit == null)
			{
				// Parse payment method from receipt data
				var firstReceipt = receipts.First();
				var paymentMethod = ParsePaymentMethod2020To2024(firstReceipt.ModeOfPayment);

				// Create new visit
				visit = new ClinicVisit
				{
					ClinicPatientId = clinicPatient.Id,
					ClinicId = CLINIC_ID,
					AppointmentDate = appointmentDate.Date,
					AppointmentTime = APPOINTMENT_TIME,
					PaymentMethod = paymentMethod
				};

				await _clinicVisitRepository.SaveVisitAsync(visit);
				response.VisitsCreated++;
			}
			else
			{
				visit = existingVisit;

				// Always update payment method from receipt data
				var firstReceipt = receipts.First();
				var parsedPaymentMethod = ParsePaymentMethod2020To2024(firstReceipt.ModeOfPayment);

				// Update if payment method is different or not set
				if (visit.PaymentMethod != parsedPaymentMethod)
				{
					visit.PaymentMethod = parsedPaymentMethod;
					await _clinicVisitRepository.UpdateAsync(visit);
				}
			}

			// 4. Create receipt JSON data (same format as 2019)
			var receiptJsonData = CreateReceiptJsonData2020To2024(user, clinicPatient, receipts, receiptId, appointmentDate);

			// 5. Check if receipt record already exists
			var existingRecord = await _clinicPatientRecordRepository.GetByCompositeKeyAsync(
				CLINIC_ID, clinicPatient.Id, visit.Id, RecordType.Receipt);

			if (existingRecord != null)
			{
				response.SkippedReasons.Add($"Patient {patientId} on {appointmentDate:dd-MM-yyyy} Receipt {receiptId}: Receipt record already exists");
				return false;
			}

			// 6. Create receipt record
			var receiptRecord = new ClinicPatientRecord
			{
				ClinicId = CLINIC_ID,
				PatientId = clinicPatient.Id,
				ClinicVisitId = visit.Id,
				Type = RecordType.Receipt,
				JsonData = receiptJsonData,
				SendToPatient = false
			};

			await _clinicPatientRecordRepository.SaveAsync(receiptRecord);

			response.PatientsProcessed++;
			response.AddedReceipts.Add(new AddedReceiptSummary
			{
				PatientId = patientId,
				PatientName = fullName,
				HFID = user.HfId ?? "",
				AppointmentDate = appointmentDate.ToString("dd-MM-yyyy"),
				ReceiptId = receiptId,
				InvoiceId = receipts.First().InvoiceId,
				ModeOfPayment = receipts.First().ModeOfPayment,
				AmountPaid = receipts.Sum(r => r.AmountPaid)
			});

			return true;
		}

		// Helper: Parse payment method (IDENTICAL to 2019)
		private PaymentMethod? ParsePaymentMethod2020To2024(string modeOfPayment)
		{
			if (string.IsNullOrWhiteSpace(modeOfPayment))
				return null;

			return modeOfPayment.ToUpper().Trim() switch
			{
				"CARD" => PaymentMethod.DebitCard,
				"DEBIT CARD" => PaymentMethod.DebitCard,
				"CREDIT CARD" => PaymentMethod.CreditCard,
				"CASH" => PaymentMethod.Cash,
				"WALLET" => PaymentMethod.Wallet,
				"BANK TRANSFER" => PaymentMethod.BankTransfer,
				"CHECK" => PaymentMethod.Check,
				"CHEQUE" => PaymentMethod.Check,
				_ => null
			};
		}

		// Helper: Create receipt JSON data (IDENTICAL to 2019)
		private string CreateReceiptJsonData2020To2024(
			Domain.Entities.Users.User user,
			ClinicPatient clinicPatient,
			List<ExcelReceiptRow> receipts,
			string receiptId,
			DateTime appointmentDate)
		{
			var firstReceipt = receipts.First();
			var totalAmount = receipts.Sum(r => r.AmountPaid);

			// Convert payment mode
			var modeOfPayment = firstReceipt.ModeOfPayment.ToUpper() switch
			{
				"CARD" => "Debit Card",
				"CASH" => "Cash",
				"WALLET" => "Wallet",
				_ => firstReceipt.ModeOfPayment
			};

			var serviceItems = receipts.Select(r => new
			{
				name = "Service",
				qtyPerDay = "1 QTY",
				cost = r.AmountPaid,
				total = r.AmountPaid,
				ModeOfPayment = modeOfPayment,
				ChequeNo = "--"
			}).ToArray();

			var receiptData = new
			{
				patient = new
				{
					name = $"{user.FirstName} {user.LastName}".Trim(),
					uhid = user.HfId ?? "",
					gender = user.Gender ?? "",
					receiptId = receiptId,
					dob = user.DOB ?? "",
					doctor = DOCTOR_NAME,
					mobile = user.PhoneNumber ?? "",
					city = user.City ?? ""
				},
				receipt = new
				{
					date = appointmentDate.ToString("dd/MM/yyyy"),
					receiptNumber = receiptId,
					modeOfPayment = modeOfPayment,
					chequeNo = "--",
					amountPaid = totalAmount,
					amountInWords = ConvertAmountToWords2020To2024(totalAmount)
				},
				services = serviceItems,
				clinicInfo = new
				{
					name = "Arthrose",
					subtitle = "CRANIOFACIAL PAIN & TMJ CENTRE",
					website = "www.arthrosetmjindia.com"
				}
			};

			return JsonConvert.SerializeObject(receiptData, Formatting.None);
		}

		// Helper: Convert amount to words (IDENTICAL to 2019)
		private static string ConvertAmountToWords2020To2024(int amount)
		{
			return $"{amount} Only";
		}

		// API 2: Generate receipt PDFs for 2020-2024
		[HttpPost("generate-receipt-pdfs-2020-2024")]
		//[Authorize]
		public async Task<IActionResult> GenerateReceiptPdfs2020To2024([FromBody] ReceiptPdfRequest request)
		{
			HttpContext.Items["Log-Category"] = "2020-2024 Receipt PDF Generation";

			var response = new ReceiptPdfResponse();
			var transaction = await _clinicRepository.BeginTransactionAsync();
			var committed = false;

			try
			{
				// Get all unsent receipt records for CLINIC_ID = 8
				var receiptRecords = await _clinicPatientRecordRepository.GetUnsentReceiptRecordsAsync(CLINIC_ID);

				if (!receiptRecords.Any())
				{
					return Ok(ApiResponseFactory.Success(new ReceiptPdfResponse
					{
						TotalProcessed = 0,
						Successful = 0,
						Failed = 0,
						Message = "No unsent receipt records found."
					}, "No unsent receipt records found."));
				}

				_logger.LogInformation("Found {Count} unsent receipt records for 2020-2024", receiptRecords.Count);

				// Download Chromium once for all PDFs
				await new BrowserFetcher().DownloadAsync();

				using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
				{
					Headless = true,
					Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
				});

				foreach (var receiptRecord in receiptRecords)
				{
					response.TotalProcessed++;

					try
					{
						var success = await ProcessSingleReceiptPdf2020To2024(receiptRecord, browser, response);
						if (success)
						{
							response.Successful++;
						}
						else
						{
							response.Failed++;
							response.FailedRecords.Add(new ReceiptPdfFailed
							{
								RecordId = receiptRecord.Id,
								Reason = "PDF generation or upload failed"
							});
						}
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Error generating PDF for 2020-2024 receipt record {RecordId}", receiptRecord.Id);
						response.Failed++;
						response.FailedRecords.Add(new ReceiptPdfFailed
						{
							RecordId = receiptRecord.Id,
							Reason = $"Processing error: {ex.Message}"
						});
					}
				}

				await transaction.CommitAsync();
				committed = true;

				response.Message = $"2020-2024 Receipt PDF generation completed: {response.Successful} successful, " +
								  $"{response.Failed} failed out of {response.TotalProcessed} total receipts.";

				_logger.LogInformation("2020-2024 Receipt PDF generation completed. Success: {Success}, Failed: {Failed}",
					response.Successful, response.Failed);

				return Ok(ApiResponseFactory.Success(response, response.Message));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during 2020-2024 receipt PDF generation");
				return StatusCode(500, ApiResponseFactory.Fail("An error occurred during PDF generation."));
			}
			finally
			{
				if (!committed && transaction.GetDbTransaction().Connection != null)
					await transaction.RollbackAsync();
			}
		}

		// Helper: Process single receipt PDF (REUSES 2019 HTML generator)
		private async Task<bool> ProcessSingleReceiptPdf2020To2024(
			ClinicPatientRecord receiptRecord,
			IBrowser browser,
			ReceiptPdfResponse response)
		{
			try
			{
				// Get related data
				var visit = await _clinicVisitRepository.GetByIdAsync(receiptRecord.ClinicVisitId);
				var clinicPatient = await _clinicPatientRecordRepository.GetByIdAsync(receiptRecord.PatientId);
				var user = clinicPatient != null ? await _userRepository.GetUserByHFIDAsync(clinicPatient.HFID) : null;

				if (visit == null || clinicPatient == null || user == null)
				{
					_logger.LogWarning("Missing related data for 2020-2024 receipt record {RecordId}", receiptRecord.Id);
					return false;
				}

				// Parse receipt JSON
				var receiptData = JsonConvert.DeserializeObject<ReceiptJsonPayload>(receiptRecord.JsonData);
				if (receiptData?.Patient == null || receiptData.Receipt == null)
				{
					_logger.LogWarning("Invalid receipt JSON data for 2020-2024 record {RecordId}", receiptRecord.Id);
					return false;
				}

				// REUSE 2019 HTML generator (IDENTICAL format)
				var htmlContent = GenerateReceiptHtml2019(receiptData);

				using var page = await browser.NewPageAsync();
				await page.SetContentAsync(htmlContent);

				var pdfOptions = new PdfOptions
				{
					Format = PaperFormat.A4,
					PrintBackground = true,
					MarginOptions = new MarginOptions
					{
						Top = "0.5in",
						Bottom = "0.5in",
						Left = "0.5in",
						Right = "0.5in"
					}
				};

				var pdfBytes = await page.PdfDataAsync(pdfOptions);
				if (pdfBytes == null || pdfBytes.Length == 0)
				{
					_logger.LogWarning("PDF generation returned empty bytes for 2020-2024 record {RecordId}", receiptRecord.Id);
					return false;
				}

				// Upload to S3 (same path structure)
				var tempFileName = $"receipt_clinic{receiptRecord.ClinicId}_patient{receiptRecord.PatientId}_{Guid.NewGuid()}.pdf";
				var tempFilePath = Path.Combine(Path.GetTempPath(), tempFileName);
				await System.IO.File.WriteAllBytesAsync(tempFilePath, pdfBytes);

				var s3Key = $"clinic/{tempFileName}";
				var s3Url = await _s3StorageService.UploadFileToS3(tempFilePath, s3Key);

				if (string.IsNullOrEmpty(s3Url))
				{
					System.IO.File.Delete(tempFilePath);
					_logger.LogWarning("S3 upload failed for 2020-2024 record {RecordId}", receiptRecord.Id);
					return false;
				}

				// Update receipt record to mark as sent
				receiptRecord.SendToPatient = true;
				await _clinicPatientRecordRepository.UpdateAsync(receiptRecord);

				// Create user report (same naming)
				var reportName = $"Arthrose_Receipt_{receiptData.Patient.Name.Replace(" ", "_")}_{visit.AppointmentDate:dd-MM-yy}";
				var epochTime = new DateTimeOffset(visit.AppointmentDate.Date + visit.AppointmentTime).ToUnixTimeSeconds();
				var fileSizeKb = Math.Round((decimal)pdfBytes.Length / 1024, 2);

				var userReport = new UserReport
				{
					UserId = user.Id,
					ReportName = reportName,
					ReportCategory = 8, // Receipt category
					ReportUrl = s3Url,
					EpochTime = epochTime,
					FileSize = fileSizeKb,
					UploadedBy = "Clinic",
					ClinicId = 8,
					UserType = "Independent",
					DeletedBy = 0
				};

				await _userRepository.SaveAsync(userReport);

				// Create another clinic patient record with the PDF URL
				var pdfRecord = new ClinicPatientRecord
				{
					ClinicId = CLINIC_ID,
					PatientId = receiptRecord.PatientId,
					ClinicVisitId = receiptRecord.ClinicVisitId,
					Type = RecordType.Receipt,
					JsonData = JsonConvert.SerializeObject(new { url = s3Url }),
					SendToPatient = true
				};

				await _clinicPatientRecordRepository.SaveAsync(pdfRecord);

				// Clean up
				System.IO.File.Delete(tempFilePath);

				// Add to successful records
				response.SuccessfulRecords.Add(new ReceiptPdfSuccess
				{
					RecordId = receiptRecord.Id,
					PatientName = receiptData.Patient.Name,
					HFID = clinicPatient.HFID,
					AppointmentDate = visit.AppointmentDate.ToString("dd-MM-yyyy"),
					ReceiptUrl = s3Url,
					FileSizeKB = fileSizeKb
				});

				_logger.LogInformation("Successfully generated PDF for 2020-2024 receipt record {RecordId}", receiptRecord.Id);
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error processing 2020-2024 receipt PDF for record {RecordId}", receiptRecord.Id);
				return false;
			}
		}
		[HttpDelete("clinic/{clinicId}/patient/{patientId}/visit/{clinicVisitId}/records/{recordId}")]
		[Authorize]
		public async Task<IActionResult> DeletePatientRecord(
	[FromRoute] int clinicId,
	[FromRoute] int patientId,
	[FromRoute] int clinicVisitId,
	[FromRoute] int recordId)  // This is the UniqueRecordId of the specific record
		{
			HttpContext.Items["Log-Category"] = "Patient Record Delete";

			// Validate all IDs are positive
			if (clinicId <= 0 || patientId <= 0 || clinicVisitId <= 0 || recordId <= 0)
			{
				_logger.LogWarning("Invalid IDs in delete request. ClinicId: {ClinicId}, PatientId: {PatientId}, VisitId: {VisitId}, RecordId: {RecordId}",
					clinicId, patientId, clinicVisitId, recordId);
				return BadRequest(ApiResponseFactory.Fail("All IDs must be positive integers."));
			}

			// Check clinic authorization (user must belong to the clinic)
			bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
			if (!isAuthorized)
			{
				_logger.LogWarning("Unauthorized delete attempt for Clinic ID {ClinicId}.", clinicId);
				return Unauthorized(ApiResponseFactory.Fail("You are not authorized to delete records in this clinic."));
			}

			try
			{
				// Optional: Verify the record actually belongs to this clinic/patient/visit (security defense-in-depth)
				var existingRecord = await _clinicPatientRecordRepository.GetByUniqueRecordIdAsync(recordId);

				if (existingRecord == null)
				{
					return NotFound(ApiResponseFactory.Fail("Record not found."));
				}

				if (existingRecord.ClinicId != clinicId ||
					existingRecord.PatientId != patientId ||
					existingRecord.ClinicVisitId != clinicVisitId)
				{
					_logger.LogWarning("Security: Attempt to delete record {RecordId} with mismatched ownership. Expected Clinic/Patient/Visit: {ClinicId}/{PatientId}/{VisitId}, Actual: {ActualClinic}/{ActualPatient}/{ActualVisit}",
						recordId, clinicId, patientId, clinicVisitId, existingRecord.ClinicId, existingRecord.PatientId, existingRecord.ClinicVisitId);
					return NotFound(ApiResponseFactory.Fail("Record not found.")); // Don't leak existence
				}

				// Perform the deletion
				bool deleted = await _clinicPatientRecordRepository.DeleteAsync(recordId);

				if (!deleted)
				{
					return NotFound(ApiResponseFactory.Fail("Record not found or already deleted."));
				}

				_logger.LogInformation("Successfully deleted record {RecordId} for Clinic {ClinicId}, Patient {PatientId}, Visit {VisitId}",
					recordId, clinicId, patientId, clinicVisitId);

				return Ok(ApiResponseFactory.Success("Record deleted successfully."));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error deleting record {RecordId} for Clinic {ClinicId}, Patient {PatientId}, Visit {VisitId}",
					recordId, clinicId, patientId, clinicVisitId);
				return StatusCode(500, ApiResponseFactory.Fail("An error occurred while deleting the record."));
			}
		}



		/// Upload documents and attach to a receipt (Clinic ID 36 only)

		[HttpPost("clinic/receipt/documents/upload")]
		[Authorize]
		public async Task<IActionResult> UploadReceiptDocuments([FromForm] ReceiptDocumentUploadRequest request)
		{
			HttpContext.Items["Log-Category"] = "Receipt Document Upload";

			// Validate clinic ID is 36 (High5 Performance)
			if (request.ClinicId != 36)
			{
				_logger.LogWarning("Receipt document upload attempted for non-High5 clinic: {ClinicId}", request.ClinicId);
				return BadRequest(ApiResponseFactory.Fail("Receipt document attachment is only available for High5 Performance clinic."));
			}

			if (!ModelState.IsValid)
			{
				var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
				_logger.LogWarning("Validation failed for receipt document upload. Errors: {@Errors}", errors);
				return BadRequest(ApiResponseFactory.Fail(errors));
			}

			// Authorization check
			bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(request.ClinicId, User);
			if (!isAuthorized)
			{
				_logger.LogWarning("Unauthorized receipt document upload attempt for Clinic ID {ClinicId}", request.ClinicId);
				return Unauthorized(ApiResponseFactory.Fail("You are not authorized to upload documents for this clinic."));
			}

			// Validate file types and sizes
			var allowedExtensions = new[] { ".pdf", ".png", ".jpg", ".jpeg", ".doc", ".docx" };
			foreach (var file in request.Documents)
			{
				var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
				var fileSize = file.Length;

				if (fileSize > 50 * 1024 * 1024) // 50MB limit
				{
					return BadRequest(ApiResponseFactory.Fail($"File {file.FileName} exceeds the 50MB limit."));
				}

				if (!allowedExtensions.Contains(extension))
				{
					_logger.LogWarning("Invalid file type attempted: {FileName} with extension: {Extension}",
						file.FileName, extension);
					return BadRequest(ApiResponseFactory.Fail(
						$"Unsupported file type for {file.FileName}. Only PDF, JPG, JPEG, PNG, DOC, and DOCX files are allowed."));
				}
			}

			// Verify visit exists
			var visit = await _clinicVisitRepository.GetByIdAsync(request.VisitId);
			if (visit == null || visit.ClinicId != request.ClinicId)
			{
				return NotFound(ApiResponseFactory.Fail("Clinic visit not found."));
			}

			// Verify patient exists
			var clinicPatient = await _clinicPatientRecordRepository.GetByIdAsync(request.PatientId);
			if (clinicPatient == null)
			{
				return NotFound(ApiResponseFactory.Fail("Patient not found."));
			}

			await using var transaction = await _clinicRepository.BeginTransactionAsync();
			bool committed = false;

			try
			{
				var uploadedUrls = new List<string>();

				// Upload each document to S3
				foreach (var file in request.Documents)
				{
					var fileName = $"receipt_{request.ReceiptNumber}_{request.ClinicId}_{request.PatientId}_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
					var tempPath = Path.Combine(Path.GetTempPath(), fileName);

					// Save to temp file
					using (var stream = new FileStream(tempPath, FileMode.Create))
					{
						await file.CopyToAsync(stream);
					}

					// Upload to S3
					var s3Url = await _s3StorageService.UploadFileToS3(tempPath, $"clinic/receipts/{fileName}");

					if (s3Url != null)
					{
						uploadedUrls.Add(s3Url);
					}

					// Delete temp file
					System.IO.File.Delete(tempPath);
				}

				if (!uploadedUrls.Any())
				{
					return StatusCode(500, ApiResponseFactory.Fail("Failed to upload files to S3."));
				}

				// Check if receipt document record already exists
				var existingRecord = await _clinicPatientRecordRepository
					.GetReceiptDocumentByReceiptNumberAsync(request.ClinicId, request.ReceiptNumber);

				if (existingRecord != null)
				{
					// Append new URLs to existing record
					var existingUrls = System.Text.Json.JsonSerializer.Deserialize<List<string>>(existingRecord.JsonData) ?? new List<string>();
					existingUrls.AddRange(uploadedUrls);
					existingRecord.JsonData = System.Text.Json.JsonSerializer.Serialize(existingUrls);
					existingRecord.EpochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

					await _clinicPatientRecordRepository.UpdateAsync(existingRecord);

                    _logger.LogInformation("Updated existing receipt document record for Receipt {ReceiptNumber}", request.ReceiptNumber);
                }
                else
                {
                    // Create new record
                    var record = new ClinicPatientRecord
                    {
                        ClinicId = request.ClinicId,
                        PatientId = request.PatientId,
                        ClinicVisitId = request.VisitId,
                        Type = RecordType.ReceiptDocuments,
                        UniqueRecordId = request.ReceiptNumber,
                        JsonData = System.Text.Json.JsonSerializer.Serialize(uploadedUrls),
                        SendToPatient = false,
                        EpochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    };

					await _clinicPatientRecordRepository.SaveAsync(record);

					_logger.LogInformation("Created new receipt document record for Receipt {ReceiptNumber}", request.ReceiptNumber);
				}

				await transaction.CommitAsync();
				committed = true;

				// Invalidate cache
				_cacheService.InvalidateClinicStatistics(request.ClinicId);

				var response = new ReceiptDocumentUploadResponse
				{
					ReceiptNumber = request.ReceiptNumber,
					ClinicId = request.ClinicId,
					PatientId = request.PatientId,
					VisitId = request.VisitId,
					TotalDocumentsUploaded = uploadedUrls.Count,
					DocumentUrls = uploadedUrls,
					Message = $"Successfully uploaded {uploadedUrls.Count} document(s) for receipt {request.ReceiptNumber}."
				};

				_logger.LogInformation(
					"Uploaded {Count} documents for Receipt {ReceiptNumber}, Clinic {ClinicId}, Patient {PatientId}, Visit {VisitId}",
					uploadedUrls.Count, request.ReceiptNumber, request.ClinicId, request.PatientId, request.VisitId);

				return Ok(ApiResponseFactory.Success(response, response.Message));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error uploading receipt documents for Receipt {ReceiptNumber}, Clinic {ClinicId}",
					request.ReceiptNumber, request.ClinicId);
				return StatusCode(500, ApiResponseFactory.Fail("An error occurred while uploading documents."));
			}
			finally
			{
				if (!committed && transaction.GetDbTransaction().Connection != null)
					await transaction.RollbackAsync();
			}
		}


		/// Get all receipt documents for a specific visit (Clinic ID 36 only)

		[HttpGet("clinic/{clinicId}/patient/{patientId}/visit/{visitId}/receipt-documents")]
		[Authorize]
		public async Task<IActionResult> GetReceiptDocuments(
			[FromRoute] int clinicId,
			[FromRoute] int patientId,
			[FromRoute] int visitId)
		{
			HttpContext.Items["Log-Category"] = "Receipt Document Fetch";

			// Validate clinic ID is 36
			if (clinicId != 36)
			{
				_logger.LogWarning("Receipt document fetch attempted for non-High5 clinic: {ClinicId}", clinicId);
				return BadRequest(ApiResponseFactory.Fail("Receipt document attachment is only available for High5 Performance clinic."));
			}

			if (clinicId <= 0 || patientId <= 0 || visitId <= 0)
			{
				_logger.LogWarning("Invalid IDs. ClinicId: {ClinicId}, PatientId: {PatientId}, VisitId: {VisitId}",
					clinicId, patientId, visitId);
				return BadRequest(ApiResponseFactory.Fail("Clinic ID, Patient ID, and Visit ID must be positive integers."));
			}

			bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
			if (!isAuthorized)
			{
				_logger.LogWarning("Unauthorized receipt document fetch attempt for Clinic ID {ClinicId}", clinicId);
				return Unauthorized(ApiResponseFactory.Fail("You are not authorized to view documents for this clinic."));
			}

			try
			{
				var records = await _clinicPatientRecordRepository
					.GetReceiptDocumentsByVisitAsync(clinicId, patientId, visitId);

				var documentInfoList = records.Select(r =>
				{
					var urls = System.Text.Json.JsonSerializer.Deserialize<List<string>>(r.JsonData) ?? new List<string>();

					return new DocumentInfo
					{
						RecordId = r.Id,
						ReceiptNumber = r.UniqueRecordId ?? "",
						DocumentUrls = urls,
						UploadedAt = r.EpochTime
					};
				}).ToList();

				var response = new ReceiptDocumentGetResponse
				{
					ClinicId = clinicId,
					PatientId = patientId,
					VisitId = visitId,
					Documents = documentInfoList
				};

				_logger.LogInformation(
					"Fetched {Count} receipt document records for Clinic {ClinicId}, Patient {PatientId}, Visit {VisitId}",
					documentInfoList.Count, clinicId, patientId, visitId);

				return Ok(ApiResponseFactory.Success(response, "Receipt documents fetched successfully."));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error fetching receipt documents for Clinic {ClinicId}, Patient {PatientId}, Visit {VisitId}",
					clinicId, patientId, visitId);
				return StatusCode(500, ApiResponseFactory.Fail("An error occurred while fetching receipt documents."));
			}
		}

		/// Delete receipt documents by receipt number (Clinic ID 36 only)

		/// <summary>
		/// Delete receipt documents by record ID (Clinic ID 36 only)
		/// </summary>
		[HttpDelete("clinic/receipt/documents/{recordId}")]
		[Authorize]
		public async Task<IActionResult> DeleteReceiptDocuments(
			[FromRoute] int recordId,
			[FromQuery] int clinicId)
		{
			HttpContext.Items["Log-Category"] = "Receipt Document Delete";

			// Validate clinic ID is 36
			if (clinicId != 36)
			{
				_logger.LogWarning("Receipt document delete attempted for non-High5 clinic: {ClinicId}", clinicId);
				return BadRequest(ApiResponseFactory.Fail("Receipt document attachment is only available for High5 Performance clinic."));
			}

			if (recordId <= 0)
			{
				return BadRequest(ApiResponseFactory.Fail("Record ID must be a positive integer."));
			}

			bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
			if (!isAuthorized)
			{
				_logger.LogWarning("Unauthorized receipt document delete attempt for Clinic ID {ClinicId}", clinicId);
				return Unauthorized(ApiResponseFactory.Fail("You are not authorized to delete documents for this clinic."));
			}

			await using var transaction = await _clinicRepository.BeginTransactionAsync();
			bool committed = false;

			try
			{
				// Get the record first to verify it exists and belongs to the clinic
				var record = await _clinicPatientRecordRepository.GetRecordByIdAsync(recordId);

				if (record == null)
				{
					_logger.LogWarning("Receipt document record {RecordId} not found", recordId);
					return NotFound(ApiResponseFactory.Fail($"Receipt document with ID {recordId} not found."));
				}

				// Verify it belongs to the correct clinic and is an Images type
				if (record.ClinicId != clinicId)
				{
					_logger.LogWarning("Receipt document record {RecordId} does not belong to Clinic {ClinicId}",
						recordId, clinicId);
					return NotFound(ApiResponseFactory.Fail($"Receipt document with ID {recordId} not found."));
				}

                if (record.Type != RecordType.ReceiptDocuments)
                {
                    _logger.LogWarning("Record {RecordId} is not an Images type (Type: {Type})",
                        recordId, record.Type);
                    return BadRequest(ApiResponseFactory.Fail("This record is not a receipt document."));
                }

				// Delete the record
				bool deleted = await _clinicPatientRecordRepository.DeleteDocumentsAsync(recordId);

				if (!deleted)
				{
					return NotFound(ApiResponseFactory.Fail($"Receipt document with ID {recordId} not found or already deleted."));
				}

				await transaction.CommitAsync();
				committed = true;

				// Invalidate cache
				_cacheService.InvalidateClinicStatistics(clinicId);

				var response = new ReceiptDocumentDeleteResponse
				{
					ReceiptNumber = record.UniqueRecordId ?? "",
					DeletedCount = 1,
					Message = $"Successfully deleted receipt document (Record ID: {recordId}, Receipt: {record.UniqueRecordId})."
				};

				_logger.LogInformation("Deleted receipt document record {RecordId} for Receipt {ReceiptNumber}, Clinic {ClinicId}",
					recordId, record.UniqueRecordId, clinicId);

				return Ok(ApiResponseFactory.Success(response, response.Message));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error deleting receipt document record {RecordId}, Clinic {ClinicId}",
					recordId, clinicId);
				return StatusCode(500, ApiResponseFactory.Fail("An error occurred while deleting receipt documents."));
			}
			finally
			{
				if (!committed && transaction.GetDbTransaction().Connection != null)
					await transaction.RollbackAsync();
			}
		}


        [HttpPost("clinic/patient/documents/resend/{recordId}")]
        [Authorize]
        public async Task<IActionResult> ResendPdfDocument([FromRoute] int recordId)
        {
            HttpContext.Items["Log-Category"] = "PDF Document Resend";

            if (recordId <= 0)
            {
                return BadRequest(ApiResponseFactory.Fail("Record ID must be a positive integer."));
            }

            try
            {
                // 1. FETCH THE EXISTING RECORD FROM DATABASE
                var record = await _clinicPatientRecordRepository.GetRecordByIdAsync(recordId);

                if (record == null)
                {
                    _logger.LogWarning("Record {RecordId} not found for resend", recordId);
                    return NotFound(ApiResponseFactory.Fail($"Record with ID {recordId} not found."));
                }

                // 2. AUTHORIZATION CHECK
                bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(record.ClinicId, User);
                if (!isAuthorized)
                {
                    _logger.LogWarning("Unauthorized resend attempt for Record {RecordId}, Clinic {ClinicId}",
                        recordId, record.ClinicId);
                    return Unauthorized(ApiResponseFactory.Fail("You are not authorized to resend documents for this clinic."));
                }

                // 3. VALIDATE RECORD HAS SendToPatient = true (PDF was already generated)
                if (!record.SendToPatient)
                {
                    return BadRequest(ApiResponseFactory.Fail("This record does not have a generated PDF yet. Please generate the PDF first."));
                }

                // 4. PARSE THE EXISTING URL FROM JsonData
                string pdfUrl;
                try
                {
                    var jsonData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(record.JsonData);
                    pdfUrl = jsonData?.GetValueOrDefault("url") ?? "";

                    if (string.IsNullOrWhiteSpace(pdfUrl))
                    {
                        return BadRequest(ApiResponseFactory.Fail("Record does not contain a valid PDF URL."));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse JSON data for Record {RecordId}", recordId);
                    return BadRequest(ApiResponseFactory.Fail("Invalid JSON data format in the record."));
                }

                // 5. GET RELATED DATA FOR EMAIL
                var visit = await _clinicVisitRepository.GetByIdAsync(record.ClinicVisitId);
                if (visit == null)
                {
                    return NotFound(ApiResponseFactory.Fail("Visit not found."));
                }

                var clinicPatient = await _clinicPatientRecordRepository.GetByIdAsync(record.PatientId);
                if (clinicPatient == null)
                {
                    return NotFound(ApiResponseFactory.Fail("Patient not found."));
                }

                var user = await _userRepository.GetUserByHFIDAsync(clinicPatient.HFID);
                if (user == null)
                {
                    return NotFound(ApiResponseFactory.Fail("User not found for HFID."));
                }

                var clinic = await _clinicRepository.GetByIdAsync(record.ClinicId);
                if (clinic == null)
                {
                    return NotFound(ApiResponseFactory.Fail($"Clinic with ID {record.ClinicId} not found."));
                }

                // 6. PREPARE EMAIL DATA
                var clinicName = clinic.ClinicName ?? "Clinic";
                var patientName = clinicPatient?.PatientName ?? $"{user?.FirstName} {user?.LastName}" ?? "N/A";
                string appointmentDate = visit?.AppointmentDate != null
                    ? visit.AppointmentDate.ToString("dd-MM-yyyy")
                    : "N/A";
                string appointmentTime = visit != null
                    ? visit.AppointmentTime.ToString(@"hh\:mm")
                    : "N/A";

                // Determine document type and category
                string documentType = record.Type.ToString();
                string categoryName = record.Type switch
                {
                    RecordType.Prescription => "Medications/Prescription",
                    RecordType.Treatment => "Lab Reports",
                    RecordType.Invoice => "Invoices/Insurance",
                    RecordType.Receipt => "Invoices/Insurance",
                    RecordType.MembershipPlan => "Membership Plans",
                    RecordType.Images => "Lab Reports",
                    RecordType.HfPdf => "Special Report",
                    _ => "Unknown"
                };

                // 7. SEND EMAIL WITH EXISTING URL
                bool emailSent = false;
                string? emailSentTo = null;

                if (!string.IsNullOrWhiteSpace(user?.Email))
                {
                    try
                    {
                        var patientFirstName = user.FirstName ?? "Patient";

                        // Create document info for email template
                        var patientDocumentInfo = new PatientDocumentInfo
                        {
                            DocumentType = documentType,
                            Category = categoryName,
                            DocumentUrl = pdfUrl  // ← EXISTING URL FROM DATABASE
                        };

                        var emailTemplate = _emailTemplateService.GeneratePatientDocumentsUploadedEmailTemplate(
                            patientFirstName,
                            new List<PatientDocumentInfo> { patientDocumentInfo },
                            clinicName,
                            appointmentDate,
                            appointmentTime
                        );

                        await _emailService.SendEmailAsync(
                            user.Email,
                            $"Document Resent - {clinicName}",
                            emailTemplate
                        );

                        emailSent = true;
                        emailSentTo = user.Email;

                        _logger.LogInformation(
                            "Resend email sent successfully to {Email} for Record {RecordId}, URL: {Url}",
                            user.Email, recordId, pdfUrl);
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogError(emailEx,
                            "Failed to send resend email to {Email} for Record {RecordId}",
                            user.Email, recordId);
                        // Don't fail the entire operation if email fails
                    }
                }
                else
                {
                    _logger.LogInformation(
                        "Email not provided for Patient {PatientName} (HFID: {HFID}). Skipping email notification.",
                        patientName, clinicPatient?.HFID);
                }

                // 8. RETURN RESPONSE
                var response = new
                {
                    RecordId = recordId,
                    PatientName = patientName,
                    PatientHFID = clinicPatient?.HFID,
                    ClinicName = clinicName,
                    Email = string.IsNullOrWhiteSpace(user?.Email) ? "Not provided" : user.Email,
                    AppointmentDate = appointmentDate,
                    AppointmentTime = appointmentTime,
                    DocumentType = documentType,
                    Category = categoryName,
                    DocumentUrl = pdfUrl,  // ← EXISTING URL FROM DATABASE
                    EmailSent = emailSent,
                    SentToEmail = emailSentTo,
                    ResentAt = DateTime.UtcNow,
                    Message = emailSent
                        ? $"Document successfully resent to {emailSentTo}"
                        : "Document URL retrieved but email was not sent (no email provided or email failed)"
                };

                _logger.LogInformation(
                    "Document resent for Record {RecordId}, Patient {PatientName}, Email sent: {EmailSent}, Existing URL: {Url}",
                    recordId, patientName, emailSent, pdfUrl);

                return Ok(ApiResponseFactory.Success(response, response.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resending document for Record {RecordId}", recordId);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while resending the document."));
            }
        }




		[HttpPatch("clinic/patient/records/patient/{patientId}/visit/{clinicVisitId}/goal-setting")]
		[Authorize]
		public async Task<IActionResult> UpdateGoalSettingRequired(
	[FromRoute] int patientId,
	[FromRoute] int clinicVisitId,
	[FromBody] bool isGoalSettingRequired)
		{
			HttpContext.Items["Log-Category"] = "Clinic Patient Record Goal Setting Update";

			try
			{
				var records = await _clinicPatientRecordRepository
					.GetAllByPatientAndVisitAsync(patientId, clinicVisitId);

				if (!records.Any())
				{
					return NotFound(ApiResponseFactory.Fail(
						"No clinic patient records found for given patient and visit."
					));
				}

				// 🔐 Authorization (check once)
				bool isAuthorized = await _clinicAuthorizationService
					.IsClinicAuthorized(records.First().ClinicId, User);

				if (!isAuthorized)
				{
					return Unauthorized(ApiResponseFactory.Fail(
						"You are not authorized to update this clinic record."
					));
				}

				var epoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

				foreach (var record in records)
				{
					record.IsGoalSettingRequired = isGoalSettingRequired;
					record.EpochTime = epoch;
				}

				await _clinicPatientRecordRepository.UpdateRangeAsync(records);

				_logger.LogInformation(
					"IsGoalSettingRequired updated to {Value} for PatientId {PatientId}, VisitId {VisitId} (Records: {Count})",
					isGoalSettingRequired, patientId, clinicVisitId, records.Count
				);

				return Ok(ApiResponseFactory.Success(new
				{
					PatientId = patientId,
					ClinicVisitId = clinicVisitId,
					UpdatedRecords = records.Count,
					IsGoalSettingRequired = isGoalSettingRequired
				}, "Goal setting requirement updated for all visit records."));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex,
					"Error updating IsGoalSettingRequired for PatientId {PatientId}, VisitId {VisitId}",
					patientId, clinicVisitId);

				return StatusCode(500,
					ApiResponseFactory.Fail("An error occurred while updating goal setting requirement."));
			}
		}







	}
}
