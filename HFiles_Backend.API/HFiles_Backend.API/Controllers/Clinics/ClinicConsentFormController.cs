using HFiles_Backend.API.DTOs.Clinics;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Clinics.ConsentForm;
using HFiles_Backend.Application.DTOs.Clinics.ConsentForm.HFiles_Backend.API.DTOs.Clinics;
using HFiles_Backend.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;

namespace HFiles_Backend.API.Controllers.Clinics
{
    [Route("api/")]
    [ApiController]
    public class ClinicConsentFormController(
    IClinicVisitRepository clinicVisitRepository,
    IClinicRepository clinicRepository,
    S3StorageService s3StorageService
    ) : ControllerBase
    {
        private readonly IClinicVisitRepository _clinicVisitRepository = clinicVisitRepository;
        private readonly IClinicRepository _clinicRepository = clinicRepository;
        private readonly S3StorageService _s3StorageService = s3StorageService;





        // Add consent form to cloud and generate s3URL
        [HttpPost("consent/{visitConsentFormId}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadConsentFormPdf(
        [FromRoute] int visitConsentFormId,
        [FromForm] ConsentFormUploadRequest request)
        {
            HttpContext.Items["Log-Category"] = "Consent Form Upload";

            if (request.PdfFile == null || request.PdfFile.Length == 0)
                return BadRequest(ApiResponseFactory.Fail("No PDF file uploaded."));

            var extension = Path.GetExtension(request.PdfFile.FileName).ToLower();
            if (extension != ".pdf")
                return BadRequest(ApiResponseFactory.Fail("Only PDF files are allowed."));

            const long maxSizeInBytes = 10 * 1024 * 1024;
            if (request.PdfFile.Length > maxSizeInBytes)
                return BadRequest(ApiResponseFactory.Fail("File size exceeds 10MB limit."));

            var visitConsent = await _clinicVisitRepository.GetVisitConsentFormAsync(visitConsentFormId);

            if (visitConsent == null)
                return NotFound(ApiResponseFactory.Fail("Consent form link not found."));

            if (!string.Equals(visitConsent.ConsentForm.Title, request.ConsentFormTitle, StringComparison.OrdinalIgnoreCase))
                return BadRequest(ApiResponseFactory.Fail("Consent form title mismatch."));

            var tempFilePath = Path.GetTempFileName();
            string? s3Url = null;
            bool committed = false;

            await using var transaction = await _clinicRepository.BeginTransactionAsync();

            try
            {
                await using (var stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await request.PdfFile.CopyToAsync(stream);
                }

                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var sanitizedTitle = request.ConsentFormTitle.Replace(" ", "_");
                var s3Key = $"consents/{visitConsentFormId}_{sanitizedTitle}_{timestamp}.pdf";

                s3Url = await _s3StorageService.UploadFileToS3(tempFilePath, s3Key);

                if (string.IsNullOrEmpty(s3Url))
                    return StatusCode(500, ApiResponseFactory.Fail("Failed to upload file to S3."));

                visitConsent.ConsentFormUrl = s3Url;
                await _clinicRepository.SaveChangesAsync();
                await transaction.CommitAsync();
                committed = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during consent form upload: {ex.Message}");
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while processing the consent form."));
            }
            finally
            {
                if (!committed && transaction.GetDbTransaction().Connection != null)
                    await transaction.RollbackAsync();

                if (System.IO.File.Exists(tempFilePath))
                    System.IO.File.Delete(tempFilePath);
            }

            return Ok(ApiResponseFactory.Success(new
            {
                ConsentFormTitle = request.ConsentFormTitle,
                S3Url = s3Url
            }, "Consent form uploaded and URL saved successfully."));
        }





        // Set Verified for the consent form API
        [HttpPut("consent/{visitConsentFormId}/verify")]
        [Authorize]
        public async Task<IActionResult> VerifyConsentForm(
        [FromRoute] int visitConsentFormId,
        [FromQuery] string consentFormTitle)
        {
            HttpContext.Items["Log-Category"] = "Consent Form Verification";

            var visitConsent = await _clinicVisitRepository.GetVisitConsentFormAsync(visitConsentFormId);
            if (visitConsent == null)
                return NotFound(ApiResponseFactory.Fail("Consent form link not found."));

            if (!string.Equals(visitConsent.ConsentForm.Title, consentFormTitle, StringComparison.OrdinalIgnoreCase))
                return BadRequest(ApiResponseFactory.Fail("Consent form title mismatch."));

            if (string.IsNullOrWhiteSpace(visitConsent.ConsentFormUrl))
                return BadRequest(ApiResponseFactory.Fail("Consent form not submitted yet. Upload the form before verifying."));

            visitConsent.IsVerified = true;
            await _clinicRepository.SaveChangesAsync();

            return Ok(ApiResponseFactory.Success(new
            {
                ConsentFormTitle = consentFormTitle,
                IsVerified = true
            }, "Consent form marked as verified."));
        }





        // Fetch all consent forms
        [HttpPost("consent/forms/{clinicId}")]
        [Authorize]
        public async Task<IActionResult> GetPatientConsentForms(
        [FromRoute] int clinicId,
        [FromBody] ClinicConsentFormsRequest request)
        {
            HttpContext.Items["Log-Category"] = "Consent Form Retrieval";

            await using var transaction = await _clinicRepository.BeginTransactionAsync();
            bool committed = false;

            try
            {
                var patient = await _clinicRepository.GetPatientByHFIDAsync(request.HFID);
                if (patient == null)
                    return NotFound(ApiResponseFactory.Fail("Patient not found for given HFID."));

                var visit = await _clinicRepository.GetVisitAsync(clinicId, patient.Id, request.AppointmentDate);
                if (visit == null)
                    return NotFound(ApiResponseFactory.Fail("Clinic visit not found for given date."));

                var consentForms = await _clinicRepository.GetConsentFormsForVisitAsync(visit.Id);

                var response = consentForms.Select(cf => new ClinicConsentFormResponse
                {
                    ClinicConsentFormId = cf.Id,
                    Title = cf.ConsentForm.Title,
                    ConsentFormUrl = cf.ConsentFormUrl,
                    IsVerified = cf.IsVerified
                }).ToList();

                await transaction.CommitAsync();
                committed = true;

                return Ok(ApiResponseFactory.Success(response, "Consent forms retrieved successfully."));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during consent form retrieval: {ex.Message}");
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while retrieving consent forms."));
            }
            finally
            {
                if (!committed && transaction.GetDbTransaction().Connection != null)
                    await transaction.RollbackAsync();
            }
        }
    }
}
