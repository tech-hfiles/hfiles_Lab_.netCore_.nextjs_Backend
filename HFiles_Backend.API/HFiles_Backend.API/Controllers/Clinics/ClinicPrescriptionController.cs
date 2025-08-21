using HFiles_Backend.API.Interfaces;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Clinics.Prescription;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;

namespace HFiles_Backend.API.Controllers.Clinics
{
    [Route("api/")]
    [ApiController]
    public class ClinicPrescriptionController(
    IClinicPrescriptionRepository clinicPrescriptionRepository,
    IClinicRepository clinicRepository,
    IClinicAuthorizationService clinicAuthorizationService,
    ILogger<ClinicPrescriptionController> logger
    ) : ControllerBase
    {
        private readonly IClinicPrescriptionRepository _clinicalPrescriptionRepository = clinicPrescriptionRepository;
        private readonly IClinicRepository _clinicRepository = clinicRepository;
        private readonly IClinicAuthorizationService _clinicAuthorizationService = clinicAuthorizationService;
        private readonly ILogger<ClinicPrescriptionController> _logger = logger;





        // add prescriptions
        [HttpPost("clinic/prescription")]
        [Authorize]
        public async Task<IActionResult> SavePrescription([FromBody] PrescriptionCreateRequest request)
        {
            HttpContext.Items["Log-Category"] = "Prescription Save";

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed for status update. Errors: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(request.ClinicId, User);
            if (!isAuthorized)
            {
                _logger.LogWarning("Unauthorized prescription attempt for Clinic ID {ClinicId}", request.ClinicId);
                return Unauthorized(ApiResponseFactory.Fail("You are not authorized to save prescriptions for this clinic."));
            }

            await using var transaction = await _clinicRepository.BeginTransactionAsync();
            bool committed = false;

            try
            {
                var prescription = new ClinicPrescription
                {
                    ClinicId = request.ClinicId,
                    MedicationName = request.MedicationName,
                    MedicationDosage = request.MedicationDosage,
                    Frequency = request.Frequency,
                    Timing = request.Timing,
                    Instructions = request.Instructions
                };

                await _clinicalPrescriptionRepository.SavePrescriptionAsync(prescription);
                await transaction.CommitAsync();
                committed = true;

                _logger.LogInformation("Prescription saved for Clinic ID {ClinicId}", request.ClinicId);
                return Ok(ApiResponseFactory.Success("Prescription saved successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving prescription for Clinic ID {ClinicId}", request.ClinicId);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while saving the prescription."));
            }
            finally
            {
                if (!committed && transaction.GetDbTransaction().Connection != null)
                    await transaction.RollbackAsync();
            }
        }





        // fetch prescriptions
        [HttpGet("clinic/{clinicId}/prescriptions")]
        [Authorize]
        public async Task<IActionResult> GetPrescriptions([FromRoute] int clinicId)
        {
            HttpContext.Items["Log-Category"] = "Prescription Fetch";

            if (clinicId <= 0)
            {
                _logger.LogWarning("Invalid Clinic ID received: {ClinicId}", clinicId);
                return BadRequest(ApiResponseFactory.Fail("Clinic ID must be a positive integer."));
            }

            bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
            if (!isAuthorized)
            {
                _logger.LogWarning("Unauthorized prescription fetch attempt for Clinic ID {ClinicId}", clinicId);
                return Unauthorized(ApiResponseFactory.Fail("You are not authorized to view prescriptions for this clinic."));
            }

            try
            {
                var prescriptions = await _clinicalPrescriptionRepository.GetPrescriptionsByClinicIdAsync(clinicId);

                var response = prescriptions.Select(p => new PrescriptionResponse
                {
                    MedicationName = p.MedicationName,
                    MedicationDosage = p.MedicationDosage,
                    Frequency = p.Frequency.ToString(),
                    Timing = p.Timing.ToString(),
                    Instructions = p.Instructions
                }).ToList();

                _logger.LogInformation("Fetched {Count} prescriptions for Clinic ID {ClinicId}", response.Count, clinicId);
                return Ok(ApiResponseFactory.Success(response, "Prescriptions fetched successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching prescriptions for Clinic ID {ClinicId}", clinicId);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while fetching prescriptions."));
            }
        }





        // Update prescriptions
        [HttpPatch("clinic/{clinicId}/prescription/{prescriptionId}")]
        [Authorize]
        public async Task<IActionResult> UpdatePrescription(
        [FromRoute] int clinicId,
        [FromRoute] int prescriptionId,
        [FromBody] PrescriptionUpdateRequest request)
        {
            HttpContext.Items["Log-Category"] = "Prescription Update";

            if (clinicId <= 0 || prescriptionId <= 0)
            {
                _logger.LogWarning("Invalid Clinic ID or Prescription ID. ClinicId: {ClinicId}, PrescriptionId: {PrescriptionId}", clinicId, prescriptionId);
                return BadRequest(ApiResponseFactory.Fail("Clinic ID and Prescription ID must be positive integers."));
            }

            bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
            if (!isAuthorized)
            {
                _logger.LogWarning("Unauthorized update attempt for Clinic ID {ClinicId}", clinicId);
                return Unauthorized(ApiResponseFactory.Fail("You are not authorized to update prescriptions for this clinic."));
            }

            await using var transaction = await _clinicRepository.BeginTransactionAsync();
            bool committed = false;

            try
            {
                var existing = await _clinicalPrescriptionRepository.GetByIdAsync(prescriptionId);
                if (existing == null || existing.ClinicId != clinicId)
                {
                    _logger.LogWarning("Prescription not found or mismatched clinic. ClinicId: {ClinicId}, PrescriptionId: {PrescriptionId}", clinicId, prescriptionId);
                    return NotFound(ApiResponseFactory.Fail("Prescription not found for the specified clinic."));
                }

                if (request.MedicationName != null) existing.MedicationName = request.MedicationName;
                if (request.MedicationDosage != null) existing.MedicationDosage = request.MedicationDosage;
                if (request.Frequency.HasValue) existing.Frequency = request.Frequency.Value;
                if (request.Timing.HasValue) existing.Timing = request.Timing.Value;
                if (request.Instructions != null) existing.Instructions = request.Instructions;

                await _clinicalPrescriptionRepository.UpdatePrescriptionAsync(existing);
                await transaction.CommitAsync();
                committed = true;

                _logger.LogInformation("Prescription updated. ClinicId: {ClinicId}, PrescriptionId: {PrescriptionId}", clinicId, prescriptionId);
                return Ok(ApiResponseFactory.Success("Prescription updated successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating prescription. ClinicId: {ClinicId}, PrescriptionId: {PrescriptionId}", clinicId, prescriptionId);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while updating the prescription."));
            }
            finally
            {
                if (!committed && transaction.GetDbTransaction().Connection != null)
                    await transaction.RollbackAsync();
            }
        }
    }
}
