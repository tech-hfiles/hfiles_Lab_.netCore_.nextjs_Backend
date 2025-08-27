using HFiles_Backend.API.DTOs.Clinics;
using HFiles_Backend.API.Interfaces;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Clinics.PatientRecord;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Enums;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;
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
        IClinicVisitRepository clinicVisitRepository
    ) : ControllerBase
    {
        private readonly ILogger<ClinicPatientRecordController> _logger = logger;
        private readonly IClinicAuthorizationService _clinicAuthorizationService = clinicAuthorizationService;
        private readonly IClinicRepository _clinicRepository = clinicRepository;
        private readonly IClinicPatientRecordRepository _clinicPatientRecordRepository = clinicPatientRecordRepository;
        private readonly S3StorageService _s3StorageService = s3StorageService;
        private readonly IClinicVisitRepository _clinicVisitRepository = clinicVisitRepository;





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
                    JsonData = JsonSerializer.Serialize(s3Urls)
                };

                await _clinicPatientRecordRepository.SaveAsync(record);
                await transaction.CommitAsync();
                committed = true;

                _logger.LogInformation("Uploaded {Count} files for Clinic ID {ClinicId}, Patient ID {PatientId}", s3Urls.Count, request.ClinicId, request.PatientId);
                return Ok(ApiResponseFactory.Success("Files uploaded and record saved successfully."));
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

            await using var transaction = await _clinicRepository.BeginTransactionAsync();
            bool committed = false;

            try
            {
                foreach (var doc in request.Documents)
                {
                    //var allowedExtension = ".pdf";
                    var uploadedExtension = Path.GetExtension(doc.PdfFile?.FileName)?.ToLower();
                    var fileSize = doc.PdfFile?.Length;

                    //if (uploadedExtension != allowedExtension)
                    //{
                    //    return BadRequest(ApiResponseFactory.Fail("Only PDF files are allowed."));
                    //}

                    if (fileSize > 20 * 1024 * 1024)
                    {
                        return BadRequest(ApiResponseFactory.Fail("File size exceeds the 100MB limit."));
                    }

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
                        continue;
                    }

                    string jsonData = string.Empty;

                    if (doc.PdfFile != null)
                    {
                        var fileName = $"{doc.Type.ToString().ToLower()}_clinic{request.ClinicId}_patient{request.PatientId}_{Guid.NewGuid()}{Path.GetExtension(doc.PdfFile.FileName)}";

                        var tempPath = Path.Combine(Path.GetTempPath(), fileName);

                        using (var stream = new FileStream(tempPath, FileMode.Create))
                            await doc.PdfFile.CopyToAsync(stream);

                        var s3Url = await _s3StorageService.UploadFileToS3(tempPath, $"clinic-records/{fileName}");
                        System.IO.File.Delete(tempPath);

                        if (s3Url == null)
                            return StatusCode(500, ApiResponseFactory.Fail("Failed to upload file to S3."));

                        jsonData = JsonSerializer.Serialize(new { url = s3Url });
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
                }

                await _clinicVisitRepository.UpdateAsync(visit);
                await transaction.CommitAsync();
                committed = true;

                _logger.LogInformation("Uploaded documents for Clinic ID {ClinicId}, Patient ID {PatientId}", request.ClinicId, request.PatientId);
                return Ok(ApiResponseFactory.Success("Documents uploaded successfully."));
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

            await using var transaction = await _clinicRepository.BeginTransactionAsync();
            bool committed = false;

            try
            {
                var history = await clinicPatientRecordRepository.GetPatientHistoryAsync(clinicId, patientId);
                if (history == null)
                {
                    _logger.LogInformation("No history found for Clinic ID {ClinicId}, Patient ID {PatientId}", clinicId, patientId);
                    return NotFound(ApiResponseFactory.Fail("No history found for this patient."));
                }

                await transaction.CommitAsync();
                committed = true;

                _logger.LogInformation("Fetched history for Clinic ID {ClinicId}, Patient ID {PatientId}", clinicId, patientId);
                return Ok(ApiResponseFactory.Success(history, "Patient history fetched successfully."));
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
    }
}
