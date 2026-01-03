using HFiles_Backend.API.DTOs.Clinics;
using HFiles_Backend.API.Interfaces;
using HFiles_Backend.API.Services;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Clinics.ConsentForm;
using HFiles_Backend.Application.DTOs.Clinics.ConsentForm.HFiles_Backend.API.DTOs.Clinics;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;
using System.Web;

namespace HFiles_Backend.API.Controllers.Clinics
{
    [Route("api/")]
    [ApiController]
    public class ClinicConsentFormController(
    IClinicVisitRepository clinicVisitRepository,
    IClinicRepository clinicRepository,
    S3StorageService s3StorageService,
    ILogger<ClinicConsentFormController> logger,
    IConfiguration configuration,
    IEmailTemplateService emailTemplateService,
    IUserRepository userRepository,
    EmailService emailService,
    IWebHostEnvironment env
    ) : ControllerBase
    {
        private readonly IClinicVisitRepository _clinicVisitRepository = clinicVisitRepository;
        private readonly IClinicRepository _clinicRepository = clinicRepository;
        private readonly S3StorageService _s3StorageService = s3StorageService;
        private readonly ILogger<ClinicConsentFormController> _logger = logger;
        private readonly IConfiguration _configuration = configuration;
        private readonly IEmailTemplateService _emailTemplateService = emailTemplateService;
        private readonly IUserRepository _userRepository = userRepository;
        private readonly EmailService _emailService = emailService;
        private readonly IWebHostEnvironment _env = env;



        private string GetBaseUrl()
        {
            var environmentName = _env.EnvironmentName;
            var isProduction = _env.IsProduction();

            _logger.LogInformation("Environment Name: {EnvironmentName}, IsProduction: {IsProduction}",
                environmentName, isProduction);

            var baseUrl = _env.IsProduction()
                ? "https://hfiles.co.in"
                : "http://localhost:3000";

            _logger.LogInformation("Selected Base URL: {BaseUrl}", baseUrl);

            return baseUrl;
        }



        private static string UrlEncodeForConsentForm(string consentFormName)
        {
            // Custom URL encoding to match the required format:
            // Spaces should be %20 (not +)
            // Forward slash should be %2F (uppercase F)
            return consentFormName
                .Replace(" ", "%20")
                .Replace("/", "%2F");
        }





        // Add consent form to cloud and generate s3URL
        [HttpPost("consent/{visitConsentFormId}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadConsentFormPdf(
        [FromRoute] int visitConsentFormId,
        [FromForm] ConsentFormUploadRequest request)
        {
            HttpContext.Items["Log-Category"] = "Clinic Consent Form Upload";

            if (request.PdfFile == null || request.PdfFile.Length == 0)
                return BadRequest(ApiResponseFactory.Fail("No PDF file uploaded."));

            var extension = Path.GetExtension(request.PdfFile.FileName).ToLower();
            if (extension != ".pdf")
                return BadRequest(ApiResponseFactory.Fail("Only PDF files are allowed."));

            const long maxSizeInBytes = 100 * 1024 * 1024;
            if (request.PdfFile.Length > maxSizeInBytes)
                return BadRequest(ApiResponseFactory.Fail("File size exceeds 100MB limit."));

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
            }, "Consent form uploaded successfully."));
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

            // Extract related details
            var patientName = visitConsent?.Visit?.Patient?.PatientName ?? "Unknown Patient";
            var appointmentDate = visitConsent?.Visit?.AppointmentDate.ToString("dd-MM-yyyy") ?? "N/A";
            var appointmentTime = visitConsent?.Visit?.AppointmentTime.ToString(@"hh\:mm") ?? "N/A";


            // Response + Notification
            var response = new
            {
                ConsentFormTitle = consentFormTitle,
                IsVerified = true,

                // Notification
                NotificationContext = new
                {
                    VisitConsentFormId = visitConsent?.Id,
                    ConsentFormTitle = consentFormTitle,
                    PatientName = patientName,
                    AppointmentDate = appointmentDate,
                    AppointmentTime = appointmentTime,
                    Status = "Verified"
                },
                NotificationMessage = $"Consent form '{consentFormTitle}' for {patientName} on {appointmentDate} at {appointmentTime} has been verified."
            };

            _logger.LogInformation("Consent form '{ConsentFormTitle}' verified for Patient: {PatientName}", consentFormTitle, patientName);

            return Ok(ApiResponseFactory.Success(response, "Consent form marked verified successfully."));
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
                    IsVerified = cf.IsVerified,
                    EpochTime = cf.EpochTime
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





        // Send multiple consent forms to patient
        [HttpPost("clinics/{clinicId}/patients/{patientId}/consent-forms/send")]
        [Authorize]
        public async Task<IActionResult> SendConsentFormsToPatient(
            [FromRoute] int clinicId,
            [FromRoute] int patientId,
            [FromBody] SendConsentFormsRequest request)
        {
            HttpContext.Items["Log-Category"] = "Clinic Consent Form";

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                _logger.LogWarning("Validation failed for sending consent forms. Errors: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            if (request.ConsentForms == null || !request.ConsentForms.Any())
            {
                _logger.LogWarning("No consent forms provided in request");
                return BadRequest(ApiResponseFactory.Fail("At least one consent form must be provided."));
            }

            // Check clinic authorization
            bool isAuthorized = await _clinicRepository.IsClinicAuthorizedAsync(clinicId, User);
            if (!isAuthorized)
            {
                _logger.LogWarning("Unauthorized consent form send attempt for Clinic ID {ClinicId}", clinicId);
                return Unauthorized(ApiResponseFactory.Fail("Only authorized clinics can send consent forms."));
            }

            var transaction = await _clinicRepository.BeginTransactionAsync();

            try
            {
                // Validate clinic exists
                var clinic = await _clinicRepository.GetClinicByIdAsync(clinicId);
                if (clinic == null)
                {
                    _logger.LogWarning("Clinic ID {ClinicId} not found", clinicId);
                    return NotFound(ApiResponseFactory.Fail("Clinic not found."));
                }

                // Get patient by ID and extract HFID
                var patient = await _clinicRepository.GetPatientByHFIDAsync("");
                var allPatients = await _clinicVisitRepository.GetVisitsByClinicIdAsync(clinicId);
                var targetPatient = allPatients.Select(v => v.Patient).FirstOrDefault(p => p.Id == patientId);

                if (targetPatient == null)
                {
                    _logger.LogWarning("Patient ID {PatientId} not found in Clinic ID {ClinicId}", patientId, clinicId);
                    return NotFound(ApiResponseFactory.Fail("Patient not found in this clinic."));
                }

                // Get user details by HFID
                var user = await _userRepository.GetUserByHFIDAsync(targetPatient.HFID);
                if (user == null)
                {
                    _logger.LogWarning("User not found for HFID {HFID}", targetPatient.HFID);
                    return NotFound(ApiResponseFactory.Fail("User not found for this patient."));
                }

                if (string.IsNullOrWhiteSpace(user.FirstName))
                {
                    _logger.LogWarning("FirstName not found for HFID {HFID}", targetPatient.HFID);
                    return NotFound(ApiResponseFactory.Fail("FirstName not found for this patient."));
                }

                HttpContext.Items["Sent-To-UserId"] = user.Id;

                // Validate visit exists for this patient and clinic
                var visit = await _clinicRepository.GetVisitAsync(clinicId, patientId, DateTime.Today);
                if (visit == null)
                {
                    // If no visit today, get the most recent visit
                    var visits = await _clinicVisitRepository.GetVisitsByClinicIdAsync(clinicId);
                    visit = visits.Where(v => v.Patient.Id == patientId)
                                  .OrderByDescending(v => v.AppointmentDate)
                                  .FirstOrDefault();

                    if (visit == null)
                    {
                        _logger.LogWarning("No visit found for Patient ID {PatientId} in Clinic ID {ClinicId}", patientId, clinicId);
                        return NotFound(ApiResponseFactory.Fail("No visit found for this patient."));
                    }
                }

                // Create consent form entries for all forms
                var consentFormEntries = new List<ClinicVisitConsentForm>();
                var consentFormLinks = new List<ConsentFormLinkInfo>();

                foreach (var consentForm in request.ConsentForms)
                {
                    var visitConsentForm = new ClinicVisitConsentForm
                    {
                        ClinicVisitId = visit.Id,
                        ConsentFormId = consentForm.ConsentFormId,
                        IsVerified = false
                    };

                    visit.ConsentFormsSent.Add(visitConsentForm);
                    consentFormEntries.Add(visitConsentForm);
                }

                // Save all consent form entries
                await _clinicRepository.SaveChangesAsync();

                // Generate consent form links for all forms
                var baseUrl = GetBaseUrl();
                for (int i = 0; i < consentFormEntries.Count; i++)
                {
                    var entry = consentFormEntries[i];
                    var formRequest = request.ConsentForms[i];
                    var encodedConsentName = UrlEncodeForConsentForm(formRequest.ConsentFormName);

                    // Determine the correct form URL based on consent form name
                    string formUrl;
                    var consentFormName = formRequest.ConsentFormName.ToLower();

                    if (consentFormName.Contains("dtr"))
                    {
                        formUrl = "PublicDTRConsentForm";
                    }
                    else if (consentFormName.Contains("tmd") || consentFormName.Contains("tmjp"))
                    {
                        formUrl = "PublicTMDConsentForm";
                    }
                    else if (consentFormName.Contains("photo"))
                    {
                        formUrl = "publicPhotographyReleaseForm";
                    }
                    else if (consentFormName.Contains("arthrose") && consentFormName.Contains("functional") && consentFormName.Contains("screening"))
                    {
                        formUrl = "publicFunctionalScreeningForm";
                    }
                    else if (consentFormName.Contains("registration"))
                    {
                        formUrl = "high5RegistrationForm";
                    }
                    else if (consentFormName.Contains("waiver"))
                    {
                        formUrl = "high5WavierForm";
                    }
                    else if (consentFormName.Contains("terms") || consentFormName.Contains("conditions"))
                    {
                        formUrl = "high5TermsConditionsForm";
                    }
                    else if (consentFormName.Contains("postnatal"))
                    {
                        formUrl = "high5PostNatalAssessmentForm";
                    }
                    else if (consentFormName.Contains("fitness assessment"))
                    {
                        formUrl = "PublicHigh5FitnessAssessment";
                    }
                    else if (consentFormName.Contains("goal setting"))
                    {
                        formUrl = "PublicHigh5GoalSettingForm";
                    }
                    else
                    {
                        // Default fallback
                        formUrl = "PublicTMDConsentForm";
                    }

                    var consentFormLink = $"{baseUrl}/{formUrl}?ConsentId={entry.Id}&ConsentName={encodedConsentName}&hfid={targetPatient.HFID}";

                    consentFormLinks.Add(new ConsentFormLinkInfo
                    {
                        ConsentFormId = entry.Id,
                        ConsentFormName = formRequest.ConsentFormName,
                        ConsentFormLink = consentFormLink
                    });
                }

                // Commit transaction BEFORE sending email (so data is saved regardless of email success)
                await transaction.CommitAsync();

                // Prepare consent form names string (used in both email and response)
                var consentFormNames = string.Join(", ", request.ConsentForms.Select(f => f.ConsentFormName));

                // ==================== SEND EMAIL NOTIFICATION ====================
                // Only send email if email is provided
                bool emailSent = false;
                string? emailSentTo = null;

                if (!string.IsNullOrWhiteSpace(user.Email))
                {
                    try
                    {
                        var emailTemplate = _emailTemplateService.GenerateMultipleConsentFormsEmailTemplate(
                            user.FirstName,
                            consentFormLinks,
                            clinic.ClinicName, clinicId
                        );

                        await _emailService.SendEmailAsync(
                            user.Email,
                            $"Consent Forms Required - {clinic.ClinicName}",
                            emailTemplate
                        );

                        emailSent = true;
                        emailSentTo = user.Email;

                        _logger.LogInformation(
                            "Consent forms email sent successfully to {Email} with {Count} forms",
                            user.Email, consentFormEntries.Count);
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogError(emailEx,
                            "Failed to send consent forms email to {Email} for Patient {PatientId}",
                            user.Email, patientId);
                        // Don't fail the entire operation if email fails
                    }
                }
                else
                {
                    _logger.LogInformation(
                        "Email not provided for Patient {PatientName} (HFID: {HFID}). Skipping email notification.",
                        targetPatient.PatientName, targetPatient.HFID);
                }

                // Build notification messages
                var consentFormLinksFormatted = string.Join("\n", consentFormLinks.Select((link, index) =>
                    $"{index + 1}. {link.ConsentFormName}: {link.ConsentFormLink}"));

                var userNotificationMessage = $"{clinic.ClinicName} has sent you {consentFormEntries.Count} consent form(s) to complete. Please check the links below:\n\n{consentFormLinksFormatted}";

                // Response with notification
                var response = new
                {
                    TotalConsentFormsSent = consentFormEntries.Count,
                    ConsentForms = consentFormLinks.Select(link => new
                    {
                        ConsentFormId = link.ConsentFormId,
                        ConsentFormName = link.ConsentFormName,
                        ConsentFormLink = link.ConsentFormLink
                    }).ToList(),
                    PatientName = targetPatient.PatientName,
                    PatientHFID = targetPatient.HFID,
                    ClinicName = clinic.ClinicName,

                    // Email status
                    EmailSent = emailSent,
                    SentToEmail = emailSentTo,

                    // Notification context
                    NotificationContext = new
                    {
                        PatientName = targetPatient.PatientName,
                        PatientHFID = targetPatient.HFID,
                        ClinicId = clinicId,
                        ClinicName = clinic.ClinicName,
                        ConsentFormsCount = consentFormEntries.Count,
                        ConsentFormNames = consentFormNames,
                        Status = "Sent",
                        EmailStatus = emailSent
                            ? "Email sent"
                            : string.IsNullOrWhiteSpace(user.Email)
                                ? "No email provided"
                                : "Email sending failed"
                    },
                    NotificationMessage = $"{consentFormEntries.Count} consent form(s) have been sent to {targetPatient.PatientName} on their HF Account (HFID: {targetPatient.HFID}). Forms: {consentFormNames}",
                    UserNotificationMessage = userNotificationMessage
                };

                _logger.LogInformation(
                    "{Count} consent form(s) sent to Patient {PatientName} (HFID: {HFID}) for Clinic {ClinicId}. Forms: {Forms}. Email sent: {EmailSent}",
                    consentFormEntries.Count, targetPatient.PatientName, targetPatient.HFID, clinicId, consentFormNames, emailSent);

                return Ok(ApiResponseFactory.Success(response, "Consent forms sent successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending consent forms to Patient ID {PatientId} in Clinic ID {ClinicId}", patientId, clinicId);

                // Safely rollback transaction
                try
                {
                    await transaction.RollbackAsync();
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Error during transaction rollback");
                }

                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while sending the consent forms."));
            }
            finally
            {
                // Safely dispose transaction
                try
                {
                    await transaction.DisposeAsync();
                }
                catch (Exception disposeEx)
                {
                    _logger.LogError(disposeEx, "Error disposing transaction");
                }
            }
        }


        // GET: api/clinic/{clinicId}/consent-forms
        [HttpGet("clinic/{clinicId}/consent-forms")]
        [Authorize]
        public async Task<IActionResult> GetConsentFormsByClinicId([FromRoute] int clinicId)
        {
            HttpContext.Items["Log-Category"] = "Consent Forms Retrieval";

            try
            {
                // Check clinic authorization
                bool isAuthorized = await _clinicRepository.IsClinicAuthorizedAsync(clinicId, User);
                if (!isAuthorized)
                {
                    _logger.LogWarning("Unauthorized access attempt for Clinic ID {ClinicId}", clinicId);
                    return Unauthorized(ApiResponseFactory.Fail("You are not authorized to access this clinic's consent forms."));
                }

                var forms = await _clinicRepository.GetConsentFormsByClinicIdAsync(clinicId);

                if (!forms.Any())
                {
                    _logger.LogInformation("No consent forms found for Clinic ID {ClinicId}", clinicId);
                    return NotFound(ApiResponseFactory.Fail($"No consent forms found for clinic ID {clinicId}"));
                }

                var response = forms.Select(f => new
                {
                    Id = f.Id,
                    Title = f.Title,
                    ClinicId = f.ClinicId
                }).ToList();

                _logger.LogInformation("Retrieved {Count} consent forms for Clinic ID {ClinicId}", forms.Count(), clinicId);

                return Ok(ApiResponseFactory.Success(response, "Consent forms retrieved successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving consent forms for Clinic ID {ClinicId}", clinicId);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while retrieving consent forms."));
            }
        }

        // PUT: api/clinic/{clinicId}/consent-forms/{consentFormId}
        [HttpPut("clinic/{clinicId}/consent-forms/{consentFormId}")]
        [Authorize]
        public async Task<IActionResult> UpdateConsentForm(
            [FromRoute] int clinicId,
            [FromRoute] int consentFormId,
            [FromBody] UpdateConsentFormRequest request)
        {
            HttpContext.Items["Log-Category"] = "Consent Form Update";

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                _logger.LogWarning("Validation failed for updating consent form. Errors: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            await using var transaction = await _clinicRepository.BeginTransactionAsync();
            bool committed = false;

            try
            {
                // Check clinic authorization
                bool isAuthorized = await _clinicRepository.IsClinicAuthorizedAsync(clinicId, User);
                if (!isAuthorized)
                {
                    _logger.LogWarning("Unauthorized update attempt for Clinic ID {ClinicId}", clinicId);
                    return Unauthorized(ApiResponseFactory.Fail("You are not authorized to update this clinic's consent forms."));
                }

                // Get the consent form
                var consentForm = await _clinicRepository.GetConsentFormByIdAsync(consentFormId);

                if (consentForm == null)
                {
                    _logger.LogWarning("Consent form ID {ConsentFormId} not found", consentFormId);
                    return NotFound(ApiResponseFactory.Fail($"Consent form with ID {consentFormId} not found."));
                }

                // Verify the consent form belongs to the specified clinic
                if (consentForm.ClinicId != clinicId)
                {
                    _logger.LogWarning(
                        "Consent form ID {ConsentFormId} does not belong to Clinic ID {ClinicId}",
                        consentFormId, clinicId);
                    return BadRequest(ApiResponseFactory.Fail("This consent form does not belong to the specified clinic."));
                }

                // Store old title for logging
                var oldTitle = consentForm.Title;

                // Update the title
                consentForm.Title = request.Title.Trim();

                // Save changes
                await _clinicRepository.UpdateConsentFormAsync(consentForm);
                await transaction.CommitAsync();
                committed = true;

                var response = new
                {
                    Id = consentForm.Id,
                    Title = consentForm.Title,
                    ClinicId = consentForm.ClinicId
                };

                _logger.LogInformation(
                    "Consent form ID {ConsentFormId} updated. Title changed from '{OldTitle}' to '{NewTitle}' for Clinic ID {ClinicId}",
                    consentFormId, oldTitle, consentForm.Title, clinicId);

                return Ok(ApiResponseFactory.Success(response, "Consent form updated successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error updating consent form ID {ConsentFormId} for Clinic ID {ClinicId}",
                    consentFormId, clinicId);

                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while updating the consent form."));
            }
            finally
            {
                if (!committed && transaction.GetDbTransaction().Connection != null)
                    await transaction.RollbackAsync();
            }
        }


        /// <summary>
        /// Delete a Clinic Visit Consent Form by its primary Id
        /// </summary>
        /// <param name="id">The Clinic Visit Consent Form Id (Primary Key)</param>
        [HttpDelete("clinic-visit-consent-forms/{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteConsentForm([FromRoute] int id)
        {
            HttpContext.Items["Log-Category"] = "Clinic Visit Consent Form Delete";

            try
            {
                _logger.LogInformation("Attempting to delete consent form with Id: {Id}", id);

                var consentForm = await _clinicVisitRepository.GetVisitConsentFormAsync(id);

                if (consentForm == null)
                {
                    _logger.LogWarning("Consent form not found with Id: {Id}", id);
                    return NotFound(ApiResponseFactory.Fail("Consent form not found."));
                }

                await _clinicVisitRepository.DeleteAsync(id);

                _logger.LogInformation("Consent form deleted successfully - Id: {Id}", id);

                return Ok(ApiResponseFactory.Success("Consent form deleted successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting consent form with Id: {Id}", id);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while deleting the consent form."));
            }
        }
    }
}
