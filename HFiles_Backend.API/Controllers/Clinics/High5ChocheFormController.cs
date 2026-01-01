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
                return BadRequest(ApiResponseFactory.Fail(ModelState));
            }

            try
            {
                var existingForm = await _formRepository.GetByClinicUserAndFormNameAsync(
                    clinicId,
                    userId,
                    request.FormName
                );

                High5ChocheForm savedForm;

                if (existingForm != null)
                {
                    // UPDATE
                    existingForm.JsonData = request.JsonData;
                    existingForm.IsSend = request.IsSend ?? existingForm.IsSend;
                    existingForm.ConsentId = request.ConsentId; // ✅ ADD
                    existingForm.EpochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                    savedForm = await _formRepository.UpdateAsync(existingForm);
                }
                else
                {
                    // CREATE
                    var form = new High5ChocheForm
                    {
                        ClinicId = clinicId,
                        UserId = userId,
                        FormName = request.FormName,
                        JsonData = request.JsonData,
                        IsSend = request.IsSend ?? false,
                        ConsentId = request.ConsentId, // ✅ ADD
                        EpochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    };

                    savedForm = await _formRepository.SaveAsync(form);
                }

                return Ok(ApiResponseFactory.Success(new High5ChocheFormResponse
                {
                    Id = savedForm.Id,
                    ClinicId = savedForm.ClinicId,
                    UserId = savedForm.UserId,
                    FormName = savedForm.FormName,
                    JsonData = savedForm.JsonData,
                    IsSend = savedForm.IsSend,
                    ConsentId = savedForm.ConsentId, // ✅ ADD
                    EpochTime = savedForm.EpochTime
                }, "Form saved successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving High5 form");
                return StatusCode(500, ApiResponseFactory.Fail("Internal server error"));
            }
        }



        /// <summary>
        /// Get a specific form by Form Name (NEW ENDPOINT)
        /// </summary>
        [HttpGet("clinics/{clinicId}/users/{userId}/high5-forms/{formName}/consent/{consentId}")]
        public async Task<IActionResult> GetFormByName(
    [FromRoute] int clinicId,
    [FromRoute] int userId,
    [FromRoute] string formName,
    [FromRoute] int consentId)
        {
            HttpContext.Items["Log-Category"] = "High5 Form Get";

            try
            {
                var decodedFormName = Uri.UnescapeDataString(formName).Trim();

                var form = await _formRepository.GetByClinicUserFormAndConsentAsync(
                    clinicId,
                    userId,
                    decodedFormName,
                    consentId
                );

                if (form == null)
                {
                    return NotFound(ApiResponseFactory.Fail("Form not found."));
                }

                return Ok(ApiResponseFactory.Success(new High5ChocheFormResponse
                {
                    Id = form.Id,
                    ClinicId = form.ClinicId,
                    UserId = form.UserId,
                    FormName = form.FormName,
                    JsonData = form.JsonData,
                    IsSend = form.IsSend,
                    ConsentId = form.ConsentId,
                    EpochTime = form.EpochTime
                }, "Form retrieved successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving form");
                return StatusCode(500, ApiResponseFactory.Fail("Internal server error"));
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