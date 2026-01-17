using HFiles_Backend.Application.DTOs.Clinics.PatientRecord;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;
using HFiles_Backend.Application.Common;

namespace HFiles_Backend.API.Controllers
{
    [Route("api/clinics/{clinicId}/patients/{patientId}/documents")]
    [ApiController]
    public class ClinicDocumentController : ControllerBase
    {
        private readonly IClinicDocumentRepository _documentRepository;
        private readonly S3StorageService _s3StorageService;
        private readonly ILogger<ClinicDocumentController> _logger;

        public ClinicDocumentController(
            IClinicDocumentRepository documentRepository,
            S3StorageService s3StorageService,
            ILogger<ClinicDocumentController> logger)
        {
            _documentRepository = documentRepository;
            _s3StorageService = s3StorageService;
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
    }
}