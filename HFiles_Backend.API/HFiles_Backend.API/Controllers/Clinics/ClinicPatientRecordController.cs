using HFiles_Backend.API.DTOs.Clinics;
using HFiles_Backend.API.Interfaces;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Clinics.PatientRecord;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Entities.Users;
using HFiles_Backend.Domain.Enums;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;
using Newtonsoft.Json;
using OfficeOpenXml;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace HFiles_Backend.API.Controllers.Clinics
{
    [Route("api/")]
    [ApiController]
    public class ClinicPatientRecordController(
        ILogger<ClinicPatientRecordController> logger,
        IClinicAuthorizationService clinicAuthorizationService,
        IClinicRepository clinicRepository,
        IClinicPatientRecordRepository clinicPatientRecordRepository,
        S3StorageService s3StorageService,
        IClinicVisitRepository clinicVisitRepository,
        IUserRepository userRepository
    ) : ControllerBase
    {
        private readonly ILogger<ClinicPatientRecordController> _logger = logger;
        private readonly IClinicAuthorizationService _clinicAuthorizationService = clinicAuthorizationService;
        private readonly IClinicRepository _clinicRepository = clinicRepository;
        private readonly IClinicPatientRecordRepository _clinicPatientRecordRepository = clinicPatientRecordRepository;
        private readonly S3StorageService _s3StorageService = s3StorageService;
        private readonly IClinicVisitRepository _clinicVisitRepository = clinicVisitRepository;
        private readonly IUserRepository _userRepository = userRepository;


        private const int CLINIC_ID = 8;
        private const string DOCTOR_NAME = "Dr. Varun R Kunte";
        private readonly TimeSpan APPOINTMENT_TIME = new(10, 0, 0);





        // Stores JSON data
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

                var existingRecord = await _clinicPatientRecordRepository
                    .GetByCompositeKeyAsync(request.ClinicId, request.PatientId, request.ClinicVisitId, parsedType);

                if (existingRecord is not null)
                {
                    existingRecord.JsonData = request.JsonData;
                    await _clinicPatientRecordRepository.UpdateAsync(existingRecord);

                    _logger.LogInformation("Existing record updated for Clinic ID {ClinicId}, Patient ID {PatientId}, Visit ID {VisitId}, Type {Type}",
                        request.ClinicId, request.PatientId, request.ClinicVisitId, parsedType);
                }
                else
                {
                    var newRecord = new ClinicPatientRecord
                    {
                        ClinicId = request.ClinicId,
                        PatientId = request.PatientId,
                        ClinicVisitId = request.ClinicVisitId,
                        Type = parsedType,
                        JsonData = request.JsonData
                    };

                    await _clinicPatientRecordRepository.SaveAsync(newRecord);

                    _logger.LogInformation("New record created for Clinic ID {ClinicId}, Patient ID {PatientId}, Visit ID {VisitId}, Type {Type}",
                        request.ClinicId, request.PatientId, request.ClinicVisitId, parsedType);
                }


                await transaction.CommitAsync();
                committed = true;

                return Ok(ApiResponseFactory.Success("Patient record saved successfully."));
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
                _logger.LogWarning("Invalid IDs. ClinicId: {ClinicId}, PatientId: {PatientId}, VisitId: {VisitId}", clinicId, patientId, clinicVisitId);
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
                var records = await _clinicPatientRecordRepository.GetByClinicPatientVisitAsync(clinicId, patientId, clinicVisitId);

                var response = records.Select(r => new ClinicPatientRecordResponse
                {
                    Type = r.Type,
                    JsonData = r.JsonData
                }).ToList();

                _logger.LogInformation("Fetched {Count} records for Clinic ID {ClinicId}, Patient ID {PatientId}, Visit ID {VisitId}",
                    response.Count, clinicId, patientId, clinicVisitId);

                return Ok(ApiResponseFactory.Success(response, "Records fetched successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching records for Clinic ID {ClinicId}, Visit ID {VisitId}", clinicId, clinicVisitId);
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
            HttpContext.Items["Log-Category"] = "Patient Document Upload";

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


            await using var transaction = await _clinicRepository.BeginTransactionAsync();
            bool committed = false;

            try
            {
                foreach (var doc in request.Documents)
                {
                    var uploadedExtension = Path.GetExtension(doc.PdfFile?.FileName)?.ToLower();
                    var fileSize = doc.PdfFile?.Length;

                    if (fileSize > 100 * 1024 * 1024)
                        return BadRequest(ApiResponseFactory.Fail("File size exceeds the 100MB limit."));

                    if ((doc.Type == RecordType.Invoice || doc.Type == RecordType.Receipt) && doc.PdfFile == null)
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
                                            FileSize = 0, // Optional: You can’t get size from URL unless stored separately
                                            UploadedBy = "Clinic",
                                            UserType = user.UserReference == 0 ? "Independent" : "Dependent",
                                            DeletedBy = 0
                                        };

                                        await _userRepository.SaveAsync(report);
                                    }
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
                        SendToPatient = doc.SendToPatient
                    };

                    await _clinicPatientRecordRepository.SaveAsync(record);

                    if (s3Url != null)
                    {
                        var report = new UserReport
                        {
                            UserId = user.Id,
                            ReportName = $"{doc.Type}",
                            ReportCategory = doc.Type switch
                            {
                                RecordType.Prescription => (int)ReportType.MedicationsPrescription,
                                RecordType.Treatment => (int)ReportType.MedicationsPrescription,
                                RecordType.Invoice => (int)ReportType.InvoicesInsurance,
                                RecordType.Receipt => (int)ReportType.InvoicesInsurance,
                                RecordType.Images => (int)ReportType.LabReport,
                                _ => (int)ReportType.Unknown
                            },
                            ReportUrl = s3Url,
                            EpochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            FileSize = Math.Round((decimal)(doc.PdfFile?.Length ?? 0) / 1024, 2),
                            UploadedBy = "Clinic",
                            //LabId = request.ClinicId,
                            UserType = user.UserReference == 0 ? "Independent" : "Dependent",
                            DeletedBy = 0
                        };

                        await _userRepository.SaveAsync(report);
                    }
                }

                await _clinicVisitRepository.UpdateAsync(visit);
                await transaction.CommitAsync();
                committed = true;

                var patientName = clinicPatient?.PatientName ?? $"{user?.FirstName} {user?.LastName}" ?? "N/A";
                string appointmentDate = visit?.AppointmentDate != null
                    ? visit.AppointmentDate.ToString("dd-MM-yyyy")
                    : "N/A";

                string appointmentTime = visit != null
                   ? visit.AppointmentTime.ToString(@"hh\:mm")
                   : "N/A";

                var uploadedDocs = request.Documents.Select(d => d.Type.ToString()).ToList();

                // Response + Notification
                var response = new
                {
                    PatientName = patientName,
                    AppointmentDate = appointmentDate,
                    AppointmentTime = appointmentTime,
                    UploadedDocuments = uploadedDocs,

                    NotificationContext = new
                    {
                        ClinicId = request.ClinicId,
                        PatientId = request.PatientId,
                        ClinicVisitId = request.ClinicVisitId,
                        HFID = clinicPatient?.HFID,
                        PatientName = patientName,
                        AppointmentDate = appointmentDate,
                        AppointmentTime = appointmentTime,
                        UploadedDocuments = uploadedDocs
                    },
                    NotificationMessage =
                     $"Documents ({string.Join(", ", uploadedDocs)}) uploaded for patient {patientName} on HF account {clinicPatient?.HFID} for {appointmentDate} at {appointmentTime}."

                };

                _logger.LogInformation(
                    "Uploaded documents ({Documents}) for Clinic ID {ClinicId}, Patient ID {PatientId}, Appointment on {AppointmentDate} {AppointmentTime}",
                    string.Join(", ", uploadedDocs), request.ClinicId, request.PatientId, appointmentDate, appointmentTime
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

                var response = new
                {
                    history.PatientName,
                    history.HfId,
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





        // Generate PDFs for prescriptions
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
        //        [Authorize]
        //        public async Task<IActionResult> GenerateAllUnsentPrescriptionPdfs([FromBody] GenerateUnsentRequest request)
        //        {
        //            HttpContext.Items["Log-Category"] = "Generate All Unsent Prescription PDFs";

        //            bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(request.ClinicId, User);
        //            if (!isAuthorized)
        //            {
        //                _logger.LogWarning("Unauthorized unsent PDF generation attempt for Clinic ID {ClinicId}", request.ClinicId);
        //                return Unauthorized(ApiResponseFactory.Fail("You are not authorized to generate prescriptions for this clinic."));
        //            }

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

        //            var formattedDob = !string.IsNullOrEmpty(data.Patient.Dob) ? data.Patient.Dob : "Not Provided";

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
    }
}
