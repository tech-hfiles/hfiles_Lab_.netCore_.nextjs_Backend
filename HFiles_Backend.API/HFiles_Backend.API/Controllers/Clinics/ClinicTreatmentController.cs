using HFiles_Backend.API.Interfaces;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Clinics.Treatment;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;

namespace HFiles_Backend.API.Controllers.Clinics
{
    [Route("api/")]
    [ApiController]
    public class ClinicTreatmentController(
    ILogger<ClinicTreatmentController> logger,
    IClinicRepository clinicRepository,
    IClinicTreatmentRepository clinicTreatmentRepository,
    IClinicAuthorizationService clinicAuthorizationService
    ) : ControllerBase
    {
        private readonly ILogger<ClinicTreatmentController> _logger = logger;
        private readonly IClinicRepository _clinicRepository = clinicRepository;
        private readonly IClinicTreatmentRepository _clinicTreatmentRepository = clinicTreatmentRepository;
        private readonly IClinicAuthorizationService _clinicAuthorizationService = clinicAuthorizationService;





        // Add Treatment
        [HttpPost("clinic/treatment")]
        [Authorize]
        public async Task<IActionResult> CreateTreatment([FromBody] ClinicTreatmentCreateRequest request)
        {
            HttpContext.Items["Log-Category"] = "Treatment Create";

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed for treatment creation. Errors: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(request.ClinicId, User);
            if (!isAuthorized)
            {
                _logger.LogWarning("Unauthorized treatment creation attempt for Clinic ID {ClinicId}", request.ClinicId);
                return Unauthorized(ApiResponseFactory.Fail("You are not authorized to create treatments for this clinic."));
            }

            await using var transaction = await _clinicRepository.BeginTransactionAsync();
            bool committed = false;

            try
            {
                var treatment = new ClinicTreatment
                {
                    ClinicId = request.ClinicId,
                    TreatmentName = request.TreatmentName,
                    Cost = request.Cost
                };

                await _clinicTreatmentRepository.SaveAsync(treatment);
                await transaction.CommitAsync();
                committed = true;

                _logger.LogInformation("Treatment created for Clinic ID {ClinicId}", request.ClinicId);
                return Ok(ApiResponseFactory.Success("Treatment created successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating treatment for Clinic ID {ClinicId}", request.ClinicId);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while creating the treatment."));
            }
            finally
            {
                if (!committed && transaction.GetDbTransaction().Connection != null)
                    await transaction.RollbackAsync();
            }
        }





        // Update Treatment
        [HttpPatch("clinic/{clinicId}/treatment/{treatmentId}")]
        [Authorize]
        public async Task<IActionResult> UpdateTreatment(
        [FromRoute] int clinicId,
        [FromRoute] int treatmentId,
        [FromBody] ClinicTreatmentUpdateRequest request)
        {
            HttpContext.Items["Log-Category"] = "Treatment Update";

            bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
            if (!isAuthorized)
            {
                _logger.LogWarning("Unauthorized treatment update attempt for Clinic ID {ClinicId}", clinicId);
                return Unauthorized(ApiResponseFactory.Fail("You are not authorized to update treatments for this clinic."));
            }

            await using var transaction = await _clinicRepository.BeginTransactionAsync();
            bool committed = false;

            try
            {
                var existing = await _clinicTreatmentRepository.GetByIdAsync(treatmentId);
                if (existing == null || existing.ClinicId != clinicId)
                    return NotFound(ApiResponseFactory.Fail("Treatment not found for the specified clinic."));

                if (request.TreatmentName != null) existing.TreatmentName = request.TreatmentName;
                if (request.QuantityPerDay.HasValue) existing.QuantityPerDay = request.QuantityPerDay.Value;
                if (request.Cost.HasValue) existing.Cost = request.Cost.Value;
                if (request.Total.HasValue) existing.Total = request.Total.Value;
                if (request.Status.HasValue)
                    existing.Status = request.Status.Value;

                await _clinicTreatmentRepository.UpdateAsync(existing);
                await transaction.CommitAsync();
                committed = true;

                _logger.LogInformation("Treatment updated. ClinicId: {ClinicId}, TreatmentId: {TreatmentId}", clinicId, treatmentId);
                return Ok(ApiResponseFactory.Success("Treatment updated successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating treatment. ClinicId: {ClinicId}, TreatmentId: {TreatmentId}", clinicId, treatmentId);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while updating the treatment."));
            }
            finally
            {
                if (!committed && transaction.GetDbTransaction().Connection != null)
                    await transaction.RollbackAsync();
            }
        }





        // Fetch Treatments
        [HttpGet("clinic/{clinicId}/treatments")]
        [Authorize]
        public async Task<IActionResult> GetTreatments([FromRoute] int clinicId)
        {
            HttpContext.Items["Log-Category"] = "Treatment Fetch";

            if (clinicId <= 0)
            {
                _logger.LogWarning("Invalid Clinic ID received: {ClinicId}", clinicId);
                return BadRequest(ApiResponseFactory.Fail("Clinic ID must be a positive integer."));
            }

            bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
            if (!isAuthorized)
            {
                _logger.LogWarning("Unauthorized treatment fetch attempt for Clinic ID {ClinicId}", clinicId);
                return Unauthorized(ApiResponseFactory.Fail("You are not authorized to view treatments for this clinic."));
            }

            try
            {
                var treatments = await _clinicTreatmentRepository.GetByClinicIdAsync(clinicId);

                if (treatments == null || !treatments.Any())
                {
                    _logger.LogInformation("No treatments found for Clinic ID {ClinicId}", clinicId);
                    return Ok(ApiResponseFactory.Success(new List<ClinicTreatmentResponse>(), "No treatments found."));
                }

                var response = treatments.Select(t => new ClinicTreatmentResponse
                {
                    TreatmentName = t.TreatmentName,
                    QuantityPerDay = t.QuantityPerDay,
                    Cost = t.Cost,
                    Total = t.Total,
                    Status = t.Status
                }).ToList();

                _logger.LogInformation("Fetched {Count} treatments for Clinic ID {ClinicId}", response.Count, clinicId);
                return Ok(ApiResponseFactory.Success(response, "Treatments fetched successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching treatments for Clinic ID {ClinicId}", clinicId);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while fetching treatments."));
            }
        }
    }
}
