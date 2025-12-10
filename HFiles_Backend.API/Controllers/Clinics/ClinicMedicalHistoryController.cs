using HFiles_Backend.API.Interfaces;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Clinics.MedicalHistory;
using HFiles_Backend.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HFiles_Backend.API.Controllers.Clinics
{
    [Route("api/")]
    [ApiController]
    public class ClinicMedicalHistoryController(
    ILogger<ClinicMedicalHistoryController> logger,
    IClinicAuthorizationService clinicAuthorizationService
    ) : ControllerBase
    {
        private readonly ILogger<ClinicMedicalHistoryController> _logger = logger;
        private readonly IClinicAuthorizationService _clinicAuthorizationService = clinicAuthorizationService;






        [HttpGet("clinics/{clinicId}/clients/{patientId}/medical/history")]
        [Authorize]
        public async Task<IActionResult> GetMedicalHistory(
           [FromRoute] int clinicId,
           [FromRoute] int patientId,
           [FromServices] ClinicMedicalHistoryRepository clinicMedicalHistoryRepository)
        {
            // Validate clinic authorization
            bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
            if (!isAuthorized)
            {
                _logger.LogWarning("Unauthorized history fetch attempt for Clinic ID {ClinicId}", clinicId);
                return Unauthorized(ApiResponseFactory.Fail("You are not authorized to view this patient's history."));
            }

            // Fetch all relevant medical segments
            var surgeries = await clinicMedicalHistoryRepository.GetSurgerySummaryAsync(new List<int> { patientId });
            var staticAllergies = await clinicMedicalHistoryRepository.GetStaticAllergySummaryAsync(patientId);
            var dynamicAllergies = await clinicMedicalHistoryRepository.GetDynamicAllergySummariesAsync(new List<int> { patientId });
            var medications = await clinicMedicalHistoryRepository.GetMedicationAllergySummaryAsync(new List<int> { patientId });
            var staticDiseases = await clinicMedicalHistoryRepository.GetStaticDiseaseWithDefaultsAsync(patientId);
            var dynamicDiseases = await clinicMedicalHistoryRepository.GetDynamicDiseaseRecordSummaryAsync(new List<int> { patientId });
            var socialHistory = await clinicMedicalHistoryRepository.GetUserSocialHistoryAsync(patientId);
            var userProfile = await clinicMedicalHistoryRepository.GetUserProfileSummaryAsync(patientId);

            // Compose final DTO
            var history = new FullMedicalHistoryResponse
            {
                PatientId = patientId,
                Surgeries = surgeries,
                StaticAllergies = staticAllergies,
                DynamicAllergies = dynamicAllergies,
                MedicationAllergies = medications,
                StaticDiseases = staticDiseases,
                DynamicDiseases = dynamicDiseases,
                SocialHistory = socialHistory,
                UserProfileSummary = userProfile
            };

            // Logging
            _logger.LogInformation("Fetched complete medical history for user {UserId}", patientId);

            // Return structured response
            return Ok(ApiResponseFactory.Success(history, "Medical history fetched successfully."));
        }
    }
}
