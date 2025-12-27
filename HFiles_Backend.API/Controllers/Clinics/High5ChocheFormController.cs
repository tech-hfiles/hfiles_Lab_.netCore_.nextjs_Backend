using System.Text.Json;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Clinics.ConsentForm;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HFiles_Backend.API.Controllers.Clinics
{
    [ApiController]
    [Route("api")]
    [Authorize]
    public class High5ChocheFormController : ControllerBase
    {
        private readonly IHigh5ChocheFormRepository _formRepository;
        private readonly ILogger<High5ChocheFormController> _logger;

        public High5ChocheFormController(
            IHigh5ChocheFormRepository formRepository,
            ILogger<High5ChocheFormController> logger)
        {
            _formRepository = formRepository;
            _logger = logger;
        }

        /// <summary>
        /// Create a new High5 Choche Form
        /// </summary>
        /// <param name="clinicId">Clinic ID from URL</param>
        /// <param name="userId">User ID from URL</param>
        /// <param name="request">Form data payload</param>
        [HttpPost("clinics/{clinicId}/users/{userId}/high5-forms")]
        public async Task<IActionResult> CreateOrUpdateForm(
      [FromRoute] int clinicId,
      [FromRoute] int userId,
      [FromBody] High5ChocheFormCreateRequest request)
        {
            HttpContext.Items["Log-Category"] = "High5 Form Create/Update";

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                _logger.LogWarning("Validation failed for High5 form. Errors: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            try
            {
                // Check if a form already exists for this clinic, user, and form name
                var existingForm = await _formRepository.GetByClinicUserAndFormNameAsync(
                    clinicId,
                    userId,
                    request.FormName
                );

                High5ChocheForm savedForm;
                string message;

                if (existingForm != null)
                {
                    // Update existing form
                    existingForm.JsonData = request.JsonData;
                    existingForm.IsSend = request.IsSend ?? existingForm.IsSend;
                    existingForm.EpochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                    savedForm = await _formRepository.UpdateAsync(existingForm);
                    message = "High5 form updated successfully.";

                    _logger.LogInformation(
                        "High5 form updated successfully. Form ID: {FormId}, Clinic ID: {ClinicId}, User ID: {UserId}",
                        savedForm.Id, clinicId, userId
                    );
                }
                else
                {
                    // Create new form
                    var form = new High5ChocheForm
                    {
                        ClinicId = clinicId,
                        UserId = userId,
                        FormName = request.FormName,
                        JsonData = request.JsonData,
                        IsSend = request.IsSend ?? false,
                        EpochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    };

                    savedForm = await _formRepository.SaveAsync(form);
                    message = "High5 form created successfully.";

                    _logger.LogInformation(
                        "High5 form created successfully. Form ID: {FormId}, Clinic ID: {ClinicId}, User ID: {UserId}",
                        savedForm.Id, clinicId, userId
                    );
                }

                var response = new High5ChocheFormResponse
                {
                    Id = savedForm.Id,
                    ClinicId = savedForm.ClinicId,
                    UserId = savedForm.UserId,
                    FormName = savedForm.FormName,
                    JsonData = savedForm.JsonData,
                    IsSend = savedForm.IsSend,
                    EpochTime = savedForm.EpochTime,
                };

                return Ok(ApiResponseFactory.Success(response, message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating/updating High5 form for Clinic ID {ClinicId}, User ID {UserId}",
                    clinicId, userId);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while processing the form."));
            }
        }


        /// <summary>
        /// Get a specific form by Form Name (NEW ENDPOINT)
        /// </summary>
        [HttpGet("clinics/{clinicId}/users/{userId}/high5-forms/{formName}")]
        public async Task<IActionResult> GetFormByName(
            [FromRoute] int clinicId,
            [FromRoute] int userId,
            [FromRoute] string formName)
        {
            HttpContext.Items["Log-Category"] = "High5 Form Get By Name";

            try
            {
                // ✅ Decode %20 → space
                var decodedFormName = Uri.UnescapeDataString(formName).Trim();

                var form = await _formRepository.GetByClinicUserAndFormNameAsync(
                    clinicId,
                    userId,
                    decodedFormName
                );

                if (form == null)
                {
                    _logger.LogWarning(
                        "Form not found. Clinic ID: {ClinicId}, User ID: {UserId}, Form Name: {FormName}",
                        clinicId, userId, decodedFormName
                    );
                    return NotFound(ApiResponseFactory.Fail("Form not found."));
                }

                var response = new High5ChocheFormResponse
                {
                    Id = form.Id,
                    ClinicId = form.ClinicId,
                    UserId = form.UserId,
                    FormName = form.FormName,
                    JsonData = form.JsonData,
                    IsSend = form.IsSend,
                    EpochTime = form.EpochTime,
                };

                _logger.LogInformation(
                    "Form retrieved successfully. Form ID: {FormId}, Form Name: {FormName}",
                    form.Id, decodedFormName
                );

                return Ok(ApiResponseFactory.Success(response, "Form retrieved successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error retrieving form. Clinic ID: {ClinicId}, User ID: {UserId}, Form Name: {FormName}",
                    clinicId, userId, formName
                );
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while retrieving the form."));
            }
        }


        /// <summary>
        /// Get a specific form by ID
        /// </summary>
        [HttpGet("High5Form/{formId}/ClinicId/{clinicId}/userId/{userId}")]
        public async Task<IActionResult> GetFormById(
            [FromRoute] int clinicId,
            [FromRoute] int userId,
            [FromRoute] int formId)
        {
            try
            {
                var form = await _formRepository.GetByIdAsync(formId);

                if (form == null || form.ClinicId != clinicId || form.UserId != userId)
                {
                    return NotFound(ApiResponseFactory.Fail("Form not found."));
                }

                var response = new High5ChocheFormResponse
                {
                    Id = form.Id,
                    ClinicId = form.ClinicId,
                    UserId = form.UserId,
                    FormName = form.FormName,
                    JsonData = form.JsonData,
                    IsSend = form.IsSend,
                    EpochTime = form.EpochTime,
                };

                return Ok(ApiResponseFactory.Success(response, "Form retrieved successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving form {FormId}", formId);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while retrieving the form."));
            }
        }
    }
}