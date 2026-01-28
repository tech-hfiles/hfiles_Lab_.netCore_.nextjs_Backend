using HFiles_Backend.API.DTOs.Clinics;
using HFiles_Backend.API.Interfaces;
using HFiles_Backend.API.Services;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Clinics.PatientRecord;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Entities.Users;
using HFiles_Backend.Domain.Enums;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Data;
using HFiles_Backend.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;


namespace HFiles_Backend.API.Controllers
{
    [Route("api/clinics/{clinicId}/patients/{patientId}/documents")]
    [ApiController]
    public class ClinicDocumentController : ControllerBase
    {
        private readonly IClinicDocumentRepository _documentRepository;
        private readonly S3StorageService _s3StorageService;
        private readonly ILogger<ClinicDocumentController> _logger;
        private readonly IEmailTemplateService _emailTemplateService;  
        private readonly IUserRepository _userRepository;             
        private readonly EmailService _emailService;


        public ClinicDocumentController(
      IClinicDocumentRepository documentRepository,
      S3StorageService s3StorageService,
      IEmailTemplateService emailTemplateService,
      IUserRepository userRepository,                            
      EmailService emailService,
      ILogger<ClinicDocumentController> logger)
        {
            _documentRepository = documentRepository;
            _s3StorageService = s3StorageService;
            _emailTemplateService = emailTemplateService;             
            _userRepository = userRepository;                           
            _emailService = emailService;
            _logger = logger;
        }

        [HttpPost]
        [Authorize]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadDocuments(
            [FromRoute] int clinicId,
            [FromRoute] int patientId,
            [FromForm] ClinicDocumentUploadRequest request)
        {
            HttpContext.Items["Log-Category"] = "Clinic Document Upload";

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed for document upload. Errors: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            // Validate FileNames array matches Files array length (if provided)
            if (request.FileNames != null && request.FileNames.Count != request.Files.Count)
            {
                return BadRequest(ApiResponseFactory.Fail("Number of file names must match number of files."));
            }

            // Validate file types and sizes
            var allowedExtensions = new[] { ".pdf", ".png", ".jpg", ".jpeg", ".doc", ".docx" };
            foreach (var file in request.Files)
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

            // Verify clinic exists
            var clinic = await _documentRepository.GetClinicByIdAsync(clinicId);
            if (clinic == null)
            {
                return NotFound(ApiResponseFactory.Fail("Clinic not found."));
            }

            await using var transaction = await _documentRepository.BeginTransactionAsync();
            bool committed = false;

            try
            {
                var uploadedRecords = new List<ClinicDocumentResponseDto>();

                // Upload each file to S3
                for (int i = 0; i < request.Files.Count; i++)
                {
                    var file = request.Files[i];
                    var fileExtension = Path.GetExtension(file.FileName);
                    var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                    var s3FileName = $"clinic_{clinicId}_patient_{patientId}_{timestamp}_{Guid.NewGuid()}{fileExtension}";
                    var tempPath = Path.Combine(Path.GetTempPath(), s3FileName);

                    // Save to temp file
                    using (var stream = new FileStream(tempPath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    // Upload to S3
                    var s3Key = $"clinic/documents/{clinicId}/{patientId}/{s3FileName}";
                    var s3Url = await _s3StorageService.UploadFileToS3(tempPath, s3Key);

                    // Delete temp file
                    System.IO.File.Delete(tempPath);

                    if (string.IsNullOrEmpty(s3Url))
                    {
                        return StatusCode(500, ApiResponseFactory.Fail($"Failed to upload file {file.FileName} to S3."));
                    }

                    // Use manual filename if provided, otherwise use original filename
                    var displayFileName = (request.FileNames != null && i < request.FileNames.Count && !string.IsNullOrWhiteSpace(request.FileNames[i]))
                        ? request.FileNames[i]
                        : file.FileName;

                    // Create document record
                    var document = new ClinicDocument_Storage
                    {
                        ClinicId = clinicId,
                        PatientId = patientId,
                        FileUrl = s3Url,
                        FileName = displayFileName,  // Use manual or original filename
                        FileSizeInBytes = file.Length,
                        SendToPatient = request.SendToPatient,
                        IsDeleted = false,
                        EpochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    };

                    var savedDocument = await _documentRepository.SaveDocumentAsync(document);

                    uploadedRecords.Add(new ClinicDocumentResponseDto
                    {
                        Id = savedDocument.Id,
                        FileName = savedDocument.FileName!,
                        FileUrl = savedDocument.FileUrl!,
                        FileSizeInBytes = savedDocument.FileSizeInBytes ?? 0,
                        SendToPatient = savedDocument.SendToPatient ?? false,
                        EpochTime = savedDocument.EpochTime
                    });

                    _logger.LogInformation(
                        "Uploaded document: {FileName} (Display Name: {DisplayFileName}) for Clinic {ClinicId}, Patient {PatientId}",
                        file.FileName, displayFileName, clinicId, patientId);
                }

                await transaction.CommitAsync();
                committed = true;

                var response = new ClinicDocumentUploadResponseDto
                {
                    ClinicId = clinicId,
                    PatientId = patientId,
                    TotalFilesUploaded = uploadedRecords.Count,
                    UploadedFiles = uploadedRecords,
                    Message = $"Successfully uploaded {uploadedRecords.Count} document(s)."
                };

                _logger.LogInformation(
                    "Uploaded {Count} documents for Clinic {ClinicId}, Patient {PatientId}",
                    uploadedRecords.Count, clinicId, patientId);

                return Ok(ApiResponseFactory.Success(response, response.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error uploading documents for Clinic {ClinicId}, Patient {PatientId}",
                    clinicId, patientId);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while uploading documents."));
            }
            finally
            {
                if (!committed && transaction.GetDbTransaction().Connection != null)
                    await transaction.RollbackAsync();
            }
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetDocuments(
            [FromRoute] int clinicId,
            [FromRoute] int patientId)
        {
            try
            {
                var documents = await _documentRepository.GetPatientDocumentsAsync(patientId, clinicId);
                return Ok(ApiResponseFactory.Success(documents, "Documents retrieved successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving documents for Clinic {ClinicId}, Patient {PatientId}",
                    clinicId, patientId);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while retrieving documents."));
            }
        }

        [HttpDelete("{documentId}")]
        [Authorize]
        public async Task<IActionResult> DeleteDocument(
            [FromRoute] int clinicId,
            [FromRoute] int patientId,
            [FromRoute] int documentId)
        {
            try
            {
                var result = await _documentRepository.SoftDeleteDocumentAsync(documentId, clinicId, patientId);

                if (!result)
                {
                    return NotFound(ApiResponseFactory.Fail("Document not found."));
                }

                return Ok(ApiResponseFactory.Success("Document deleted successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting document {DocumentId}", documentId);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while deleting the document."));
            }
        }


        [HttpPatch("{documentId}/filename")]
        [Authorize]
        public async Task<IActionResult> UpdateDocumentFileName(
    [FromRoute] int clinicId,
    [FromRoute] int patientId,
    [FromRoute] int documentId,
    [FromBody] UpdateFileNameRequest request)
        {
            HttpContext.Items["Log-Category"] = "Update Document FileName";

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed for filename update. Errors: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            if (string.IsNullOrWhiteSpace(request.FileName))
            {
                return BadRequest(ApiResponseFactory.Fail("File name cannot be empty."));
            }

            try
            {
                // Get the document
                var document = await _documentRepository.GetDocumentByIdAsync(documentId, clinicId, patientId);

                if (document == null)
                {
                    return NotFound(ApiResponseFactory.Fail("Document not found."));
                }

                // Update only the filename
                var result = await _documentRepository.UpdateDocumentFileNameAsync(documentId, clinicId, patientId, request.FileName);

                if (!result)
                {
                    return NotFound(ApiResponseFactory.Fail("Failed to update document filename."));
                }

                _logger.LogInformation(
                    "Updated filename for document {DocumentId} from '{OldName}' to '{NewName}' for Clinic {ClinicId}, Patient {PatientId}",
                    documentId, document.FileName, request.FileName, clinicId, patientId);

                return Ok(ApiResponseFactory.Success("Document filename updated successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating filename for document {DocumentId}", documentId);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while updating the document filename."));
            }
        }

        /// <summary>
        /// Send selected documents to patient via email
        /// </summary>
        //[HttpPost("send-email")]
        //[Authorize]
        //public async Task<IActionResult> SendDocumentsEmail(
        //    [FromRoute] int clinicId,
        //    [FromRoute] int patientId,
        //    [FromBody] SendDocumentEmailRequest request)
        //{
        //    HttpContext.Items["Log-Category"] = "Clinic Document Email Send";

        //    if (!ModelState.IsValid)
        //    {
        //        var errors = ModelState.Values
        //            .SelectMany(v => v.Errors)
        //            .Select(e => e.ErrorMessage)
        //            .ToList();
        //        _logger.LogWarning("Validation failed for sending document email. Errors: {@Errors}", errors);
        //        return BadRequest(ApiResponseFactory.Fail(errors));
        //    }

        //    if (request.DocumentIds == null || !request.DocumentIds.Any())
        //    {
        //        _logger.LogWarning("No document IDs provided in request");
        //        return BadRequest(ApiResponseFactory.Fail("At least one document must be selected."));
        //    }

        //    await using var transaction = await _documentRepository.BeginTransactionAsync();
        //    bool committed = false;

        //    try
        //    {
        //        // Verify clinic exists
        //        var clinic = await _documentRepository.GetClinicByIdAsync(clinicId);
        //        if (clinic == null)
        //        {
        //            _logger.LogWarning("Clinic ID {ClinicId} not found", clinicId);
        //            return NotFound(ApiResponseFactory.Fail("Clinic not found."));
        //        }

        //        // Get patient by ID
        //        var patient = await _documentRepository.GetPatientByIdAsync(patientId);
        //        if (patient == null)
        //        {
        //            _logger.LogWarning("Patient ID {PatientId} not found", patientId);
        //            return NotFound(ApiResponseFactory.Fail("Patient not found."));
        //        }

        //        // Get user details by HFID
        //        var user = await _userRepository.GetUserByHFIDAsync(patient.HFID);
        //        if (user == null)
        //        {
        //            _logger.LogWarning("User not found for HFID {HFID}", patient.HFID);
        //            return NotFound(ApiResponseFactory.Fail("User not found for this patient."));
        //        }

        //        if (string.IsNullOrWhiteSpace(user.Email))
        //        {
        //            _logger.LogWarning("Email not found for HFID {HFID}", patient.HFID);
        //            return BadRequest(ApiResponseFactory.Fail("Patient does not have an email address on file."));
        //        }

        //        // Fetch all requested documents
        //        var documents = new List<ClinicDocument_Storage>();
        //        foreach (var docId in request.DocumentIds)
        //        {
        //            var document = await _documentRepository.GetDocumentByIdAsync(docId, clinicId, patientId);
        //            if (document == null)
        //            {
        //                _logger.LogWarning("Document ID {DocumentId} not found or doesn't belong to Patient {PatientId}",
        //                    docId, patientId);
        //                return NotFound(ApiResponseFactory.Fail($"Document with ID {docId} not found or access denied."));
        //            }

        //            if (document.IsDeleted == true)
        //            {
        //                _logger.LogWarning("Attempting to send deleted document ID {DocumentId}", docId);
        //                return BadRequest(ApiResponseFactory.Fail($"Document with ID {docId} has been deleted."));
        //            }

        //            documents.Add(document);
        //        }

        //        HttpContext.Items["Sent-To-UserId"] = user.Id;

        //        // Prepare document information for email
        //        var documentInfoList = documents.Select(doc => new
        //        {
        //            FileName = doc.FileName,
        //            FileUrl = doc.FileUrl,
        //            FileSizeInMB = (doc.FileSizeInBytes ?? 0) / (1024.0 * 1024.0)
        //        }).ToList();

        //        // Generate email template
        //        var emailTemplate = _emailTemplateService.GenerateDocumentEmailTemplate(
        //            user.FirstName ?? patient.PatientName,
        //            clinic.ClinicName,
        //        documentInfoList,
        //            request.CustomMessage
        //        );

        //        // Send email
        //        await _emailService.SendEmailAsync(
        //            user.Email,
        //            $"Medical Documents from {clinic.ClinicName}",
        //            emailTemplate
        //        );

        //        // Update SendToPatient flag for all documents
        //        foreach (var document in documents)
        //        {
        //            document.SendToPatient = true;
        //        }

        //        await _documentRepository.SaveChangesAsync();
        //        await transaction.CommitAsync();
        //        committed = true;

        //        var response = new
        //        {
        //            TotalDocumentsSent = documents.Count,
        //            Documents = documents.Select(doc => new
        //            {
        //                Id = doc.Id,
        //                FileName = doc.FileName,
        //                FileUrl = doc.FileUrl
        //            }).ToList(),
        //            PatientName = patient.PatientName,
        //            PatientHFID = patient.HFID,
        //            SentToEmail = user.Email,
        //            ClinicName = clinic.ClinicName,

        //            NotificationContext = new
        //            {
        //                PatientName = patient.PatientName,
        //                PatientHFID = patient.HFID,
        //                ClinicId = clinicId,
        //                ClinicName = clinic.ClinicName,
        //                DocumentsCount = documents.Count,
        //                Status = "Sent"
        //            },
        //            NotificationMessage = $"{documents.Count} document(s) have been sent to {patient.PatientName} (HFID: {patient.HFID}) at {user.Email}"
        //        };

        //        _logger.LogInformation(
        //            "{Count} document(s) sent to Patient {PatientName} (HFID: {HFID}) at {Email} for Clinic {ClinicId}",
        //            documents.Count, patient.PatientName, patient.HFID, user.Email, clinicId);

        //        return Ok(ApiResponseFactory.Success(response, "Documents sent successfully via email."));
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex,
        //            "Error sending documents email to Patient ID {PatientId} for Clinic ID {ClinicId}",
        //            patientId, clinicId);

        //        return StatusCode(500, ApiResponseFactory.Fail("An error occurred while sending the documents."));
        //    }
        //    finally
        //    {
        //        if (!committed && transaction.GetDbTransaction().Connection != null)
        //            await transaction.RollbackAsync();
        //    }
        //}



        /// <summary>
        /// Send selected documents to patient via email AND/OR save to HF account
        /// </summary>
        [HttpPost("send-email")]
        [Authorize]
        public async Task<IActionResult> SendDocumentsEmail(
            [FromRoute] int clinicId,
            [FromRoute] int patientId,
            [FromBody] SendDocumentEmailRequest request)
                        {
            HttpContext.Items["Log-Category"] = "Clinic Document Email Send";

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                _logger.LogWarning("Validation failed for sending document email. Errors: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            if (request.DocumentIds == null || !request.DocumentIds.Any())
            {
                _logger.LogWarning("No document IDs provided in request");
                return BadRequest(ApiResponseFactory.Fail("At least one document must be selected."));
            }

            await using var transaction = await _documentRepository.BeginTransactionAsync();
            bool committed = false;

            try
            {
                // Verify clinic exists
                var clinic = await _documentRepository.GetClinicByIdAsync(clinicId);
                if (clinic == null)
                {
                    _logger.LogWarning("Clinic ID {ClinicId} not found", clinicId);
                    return NotFound(ApiResponseFactory.Fail("Clinic not found."));
                }

                // Get patient by ID
                var patient = await _documentRepository.GetPatientByIdAsync(patientId);
                if (patient == null)
                {
                    _logger.LogWarning("Patient ID {PatientId} not found", patientId);
                    return NotFound(ApiResponseFactory.Fail("Patient not found."));
                }

                // Get user details by HFID
                var user = await _userRepository.GetUserByHFIDAsync(patient.HFID);
                if (user == null)
                {
                    _logger.LogWarning("User not found for HFID {HFID}", patient.HFID);
                    return NotFound(ApiResponseFactory.Fail("User not found for this patient."));
                }

                // Optional: Skip email if no email exists, but still allow saving to HF account
                bool hasEmail = !string.IsNullOrWhiteSpace(user.Email);
                if (!hasEmail && !request.SaveToHfAccount)
                {
                    return BadRequest(ApiResponseFactory.Fail("Patient has no email and 'SaveToHfAccount' is false."));
                }

                // Fetch all requested documents
                var documents = new List<ClinicDocument_Storage>();
                var documentInfoListForEmail = new List<object>();

                foreach (var docId in request.DocumentIds)
                {
                    var document = await _documentRepository.GetDocumentByIdAsync(docId, clinicId, patientId);
                    if (document == null)
                    {
                        _logger.LogWarning("Document ID {DocumentId} not found or doesn't belong to Patient {PatientId}",
                            docId, patientId);
                        return NotFound(ApiResponseFactory.Fail($"Document with ID {docId} not found or access denied."));
                    }

                    if (document.IsDeleted == true)
                    {
                        _logger.LogWarning("Attempting to send deleted document ID {DocumentId}", docId);
                        return BadRequest(ApiResponseFactory.Fail($"Document with ID {docId} has been deleted."));
                    }

                    documents.Add(document);

                    // Prepare data for email template
                    documentInfoListForEmail.Add(new
                    {
                        FileName = document.FileName,
                        FileUrl = document.FileUrl,
                        FileSizeInMB = (document.FileSizeInBytes ?? 0) / (1024.0 * 1024.0)
                    });
                }

                HttpContext.Items["Sent-To-UserId"] = user.Id;

                // ────────────────────────────── NEW: Save to HF account if requested ──────────────────────────────
                //int reportsCreatedCount = 0;
                //var createdReports = new List<UserReport>();

                //if (request.SaveToHfAccount)
                //{
                //    foreach (var doc in documents)
                //    {
                //        // Determine report category based on filename (you can improve this logic)
                //        int reportCategory = (int)ReportType.SpecialReport; // default

                //        var fileNameLower = doc.FileName?.ToLowerInvariant() ?? "";

                //        if (fileNameLower.Contains("prescription") || fileNameLower.Contains("treatment"))
                //            reportCategory = (int)ReportType.MedicationsPrescription;
                //        else if (fileNameLower.Contains("invoice") || fileNameLower.Contains("bill") || fileNameLower.Contains("receipt"))
                //            reportCategory = (int)ReportType.InvoicesInsurance;
                //        else if (fileNameLower.Contains("lab") || fileNameLower.Contains("test") || fileNameLower.Contains("report"))
                //            reportCategory = (int)ReportType.LabReport;
                //        else if (fileNameLower.Contains("image") || fileNameLower.Contains("photo") || fileNameLower.Contains("scan"))
                //            reportCategory = (int)ReportType.LabReport;
                //        // You can add more specific rules here

                //        var report = new UserReport
                //        {
                //            UserId = user.Id,
                //            ReportName = doc.FileName ?? $"Document_{doc.Id}_{DateTime.UtcNow:yyyyMMdd}",
                //            ReportCategory = reportCategory,
                //            ReportUrl = doc.FileUrl!,
                //            EpochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                //            FileSize = Math.Round((decimal)(doc.FileSizeInBytes ?? 0) / 1024, 2), // in KB
                //            UploadedBy = "Clinic",
                //            UserType = user.UserReference == 0 ? "Independent" : "Dependent",
                //            DeletedBy = 0
                //        };

                //        await _userRepository.SaveAsync(report);
                //        createdReports.Add(report);
                //        reportsCreatedCount++;
                //    }

                //    _logger.LogInformation(
                //        "Saved {Count} documents to HF account for UserId {UserId} (HFID: {HFID})",
                //        reportsCreatedCount, user.Id, patient.HFID);
                //}

                // ────────────────────────────── NEW: Save to HF account if requested ──────────────────────────────
                int reportsCreatedCount = 0;
                var createdReports = new List<UserReport>();

                if (request.SaveToHfAccount)
                {
                    // ✅ Use repository method instead of direct _context
                    var visit = await _documentRepository.GetOrCreateVisitForTodayAsync(clinicId, patientId);

                    if (visit == null)
                    {
                        return StatusCode(500, ApiResponseFactory.Fail("Failed to create or retrieve visit."));
                    }

                    foreach (var doc in documents)
                    {
                        // Determine report category based on filename
                        int reportCategory = (int)ReportType.SpecialReport;
                        var fileNameLower = doc.FileName?.ToLowerInvariant() ?? "";

                        if (fileNameLower.Contains("prescription") || fileNameLower.Contains("treatment"))
                            reportCategory = (int)ReportType.MedicationsPrescription;
                        else if (fileNameLower.Contains("invoice") || fileNameLower.Contains("bill") || fileNameLower.Contains("receipt"))
                            reportCategory = (int)ReportType.InvoicesInsurance;
                        else if (fileNameLower.Contains("lab") || fileNameLower.Contains("test") || fileNameLower.Contains("report"))
                            reportCategory = (int)ReportType.LabReport;
                        else if (fileNameLower.Contains("image") || fileNameLower.Contains("photo") || fileNameLower.Contains("scan"))
                            reportCategory = (int)ReportType.LabReport;

                        // ✅ Save to UserReports table (for HF account)
                        var report = new UserReport
                        {
                            UserId = user.Id,
                            ReportName = doc.FileName ?? $"Document_{doc.Id}_{DateTime.UtcNow:yyyyMMdd}",
                            ReportCategory = reportCategory,
                            ReportUrl = doc.FileUrl!,
                            EpochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            FileSize = Math.Round((decimal)(doc.FileSizeInBytes ?? 0) / 1024, 2),
                            UploadedBy = "Clinic",
                            UserType = user.UserReference == 0 ? "Independent" : "Dependent",
                            DeletedBy = 0
                        };

                        await _userRepository.SaveAsync(report);
                        createdReports.Add(report);
                        reportsCreatedCount++;

                        // ✅ Save to ClinicPatientRecords (using repository method)
                        var patientRecord = new ClinicPatientRecord
                        {
                            ClinicId = clinicId,
                            ClinicVisitId = visit.Id,
                            PatientId = patientId,
                            Type = RecordType.Images,
                            JsonData = System.Text.Json.JsonSerializer.Serialize(new { url = doc.FileUrl }),
                            SendToPatient = true,
                            EpochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                        };

                        await _documentRepository.SaveClinicPatientRecordAsync(patientRecord);

                        _logger.LogInformation(
                            "Created ClinicPatientRecord for document {FileName} in Visit {VisitId}",
                            doc.FileName, visit.Id);
                    }

                    _logger.LogInformation(
                        "Saved {Count} documents to HF account AND ClinicPatientRecords for UserId {UserId} (HFID: {HFID})",
                        reportsCreatedCount, user.Id, patient.HFID);
                }

                // ────────────────────────────── Send email (only if email exists) ──────────────────────────────
                bool emailSent = false;
                string? sentToEmail = null;

                if (hasEmail)
                {
                    try
                    {
                        var emailTemplate = _emailTemplateService.GenerateDocumentEmailTemplate(
                            user.FirstName ?? patient.PatientName,
                            clinic.ClinicName,
                            documentInfoListForEmail,
                            request.CustomMessage
                        );

                        await _emailService.SendEmailAsync(
                            user.Email,
                            $"Medical Documents from {clinic.ClinicName}",
                            emailTemplate
                        );

                        emailSent = true;
                        sentToEmail = user.Email;

                        _logger.LogInformation(
                            "Email sent successfully to {Email} with {Count} documents",
                            user.Email, documents.Count);
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogError(emailEx, "Failed to send email to {Email}", user.Email);
                        // Do NOT fail the whole request if email fails
                    }
                }
                else
                {
                    _logger.LogInformation("No email found for user {HFID}. Skipping email send.", patient.HFID);
                }

                // Update SendToPatient flag for all documents (you can make this conditional if needed)
                foreach (var document in documents)
                {
                    document.SendToPatient = true;
                }

                await _documentRepository.SaveChangesAsync();
                await transaction.CommitAsync();
                committed = true;

                // ────────────────────────────── Final Response ──────────────────────────────
                var response = new
                {
                    TotalDocumentsProcessed = documents.Count,
                    Documents = documents.Select(doc => new
                    {
                        Id = doc.Id,
                        FileName = doc.FileName,
                        FileUrl = doc.FileUrl
                    }).ToList(),

                    EmailSent = emailSent,
                    SentToEmail = sentToEmail,

                    SavedToHfAccount = request.SaveToHfAccount,
                    HfReportsCreated = reportsCreatedCount,
                    HfReports = createdReports.Select(r => new
                    {
                        ReportId = r.Id,
                        ReportName = r.ReportName,
                        ReportCategory = r.ReportCategory,
                        ReportUrl = r.ReportUrl,
                        EpochTime = r.EpochTime
                    }),

                    PatientName = patient.PatientName,
                    PatientHFID = patient.HFID,
                    ClinicName = clinic.ClinicName,

                    NotificationMessage = $"{documents.Count} document(s) processed successfully. " +
                                         (emailSent ? $"Email sent to {sentToEmail}. " : "No email sent (no email on file or failed). ") +
                                         (request.SaveToHfAccount ? $"{reportsCreatedCount} document(s) saved to patient's HF account." : ""),

                    NotificationContext = new
                    {
                        PatientName = patient.PatientName,
                        PatientHFID = patient.HFID,
                        ClinicId = clinicId,
                        ClinicName = clinic.ClinicName,
                        DocumentsCount = documents.Count,
                        EmailStatus = emailSent ? "Sent" : "Not Sent",
                        HfAccountStatus = request.SaveToHfAccount ? $"{reportsCreatedCount} reports created" : "Not saved",
                        Status = "Processed"
                    }
                };

                _logger.LogInformation(
                    "{Count} document(s) processed for Patient {PatientName} (HFID: {HFID}) | Email: {EmailStatus} | HF Account: {HfStatus}",
                    documents.Count, patient.PatientName, patient.HFID,
                    emailSent ? "Sent" : "Skipped/Failed",
                    request.SaveToHfAccount ? $"{reportsCreatedCount} saved" : "Not requested");

                return Ok(ApiResponseFactory.Success(response, "Documents processed successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error processing documents for Patient ID {PatientId} in Clinic ID {ClinicId}",
                    patientId, clinicId);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while processing the documents."));
            }
            finally
            {
                if (!committed && transaction.GetDbTransaction()?.Connection != null)
                {
                    await transaction.RollbackAsync();
                }
            }
        }
    }
}