using HFiles_Backend.API.DTOs.Clinics;
using HFiles_Backend.API.Interfaces;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Clinics.PatientHistory;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Security.Claims;

namespace HFiles_Backend.API.Controllers.Clinics
{
    [Route("api/clinic")]
    [ApiController]
    public class ClinicPatientMedicalHistoryController(
        ILogger<ClinicPatientMedicalHistoryController> logger,
        IClinicAuthorizationService clinicAuthorizationService,
        IUserRepository userRepository,
        IClinicRepository clinicRepository,
        IClinicPatientMedicalHistoryRepository medicalHistoryRepository,
        IClinicVisitRepository clinicVisitRepository,
        AppDbContext context) : ControllerBase
    {
        private readonly ILogger<ClinicPatientMedicalHistoryController> _logger = logger;
        private readonly IClinicAuthorizationService _clinicAuthorizationService = clinicAuthorizationService;
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IClinicRepository _clinicRepository = clinicRepository;
        private readonly IClinicPatientMedicalHistoryRepository _medicalHistoryRepository = medicalHistoryRepository;
        private readonly IClinicVisitRepository _clinicVisitRepository = clinicVisitRepository;
        private readonly AppDbContext _context = context;

        // Method to resolve username from claims (same pattern as MemberController)
        private async Task<string?> ResolveUsernameFromClaims()
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var adminIdStr = User.FindFirst("ClinicAdminId")?.Value;

            if (!int.TryParse(adminIdStr, out var adminId)) return null;

            if (role == "Super Admin")
            {
                var superAdmin = await _context.ClinicSuperAdmins.FirstOrDefaultAsync(sa => sa.Id == adminId);
                if (superAdmin != null)
                {
                    var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == superAdmin.UserId && u.DeletedBy == 0);
                    return $"{user?.FirstName} {user?.LastName}".Trim();
                }
            }

            if (role == "Admin")
            {
                var member = await _context.ClinicMembers.FirstOrDefaultAsync(m => m.Id == adminId);
                if (member != null)
                {
                    var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == member.UserId && u.DeletedBy == 0);
                    return $"{user?.FirstName} {user?.LastName}".Trim();
                }
            }

            return null;
        }





        // CREATE or UPDATE Medical History
        [HttpPost("patient/medical-history")]
        [Authorize]
        public async Task<IActionResult> CreateOrUpdateMedicalHistory([FromBody] PatientMedicalHistoryRequest request)
        {
            HttpContext.Items["Log-Category"] = "Patient Medical History";

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed for medical history. Errors: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            // Authorization check
            bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(request.ClinicId, User);
            if (!isAuthorized)
            {
                _logger.LogWarning("Unauthorized medical history update attempt for Clinic ID {ClinicId}", request.ClinicId);
                return Unauthorized(ApiResponseFactory.Fail("You are not authorized to manage medical history for this clinic."));
            }

            var transaction = await _clinicRepository.BeginTransactionAsync();

            try
            {
                // Resolve creator/updater name from claims
                string? actionByName = await ResolveUsernameFromClaims();
                if (string.IsNullOrWhiteSpace(actionByName))
                {
                    _logger.LogWarning("Unable to resolve user identity for medical history.");
                    await transaction.RollbackAsync();
                    return Unauthorized(ApiResponseFactory.Fail("Unable to resolve user identity."));
                }

                var adminIdStr = User.FindFirst("ClinicAdminId")?.Value;
                if (!int.TryParse(adminIdStr, out int adminId))
                {
                    await transaction.RollbackAsync();
                    return Unauthorized(ApiResponseFactory.Fail("Invalid admin ID in token."));
                }

                // Get user by HFID
                var user = await _userRepository.GetUserByHFIDAsync(request.HFID);
                if (user == null)
                {
                    _logger.LogWarning("User not found for HFID: {HFID}", request.HFID);
                    await transaction.RollbackAsync();
                    return NotFound(ApiResponseFactory.Fail($"User with HFID {request.HFID} not found."));
                }

                // Get or create clinic patient
                var fullName = $"{user.FirstName} {user.LastName}".Trim();
                var clinicPatient = await _clinicVisitRepository.GetOrCreatePatientAsync(request.HFID, fullName);

                // Check if medical history already exists using ClinicPatientId
                var existingHistory = await _medicalHistoryRepository.GetByClinicPatientIdAsync(
                    user.Id,  // This is the ClinicPatient ID
                    request.ClinicId
                );

                ClinicPatientMedicalHistory history;
                bool isUpdate = false;

                if (existingHistory != null)
                {
                    // UPDATE existing history ONLY
                    // The entity is already tracked by EF from GetByClinicPatientIdAsync
                    isUpdate = true;

                    existingHistory.Medical = request.Medical;
                    existingHistory.Surgical = request.Surgical;
                    existingHistory.Drugs = request.Drugs;
                    existingHistory.Allergies = request.Allergies;
                    existingHistory.GeneralExamination = request.GeneralExamination;
                    existingHistory.Investigations = request.Investigations;
                    existingHistory.Diagnoses = request.Diagnoses;
                    existingHistory.ProvisionalDiagnosis = request.ProvisionalDiagnosis;
                    existingHistory.Notes = request.Notes;
                    existingHistory.PresentComplaints = request.PresentComplaints;
                    existingHistory.PastHistory = request.PastHistory;
                    existingHistory.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    existingHistory.UpdatedBy = adminId;

                    // Entity is already tracked, so we just assign it
                    // No need to call UpdateAsync since EF is tracking changes
                    history = existingHistory;

                    _logger.LogInformation("Updating existing medical history ID: {HistoryId} for ClinicPatient ID: {ClinicPatientId}",
                        existingHistory.Id, clinicPatient.Id);
                }
                else
                {
                    // CREATE new history ONLY
                    history = new ClinicPatientMedicalHistory
                    {
                        ClinicPatientId = clinicPatient.Id,
                        ClinicId = request.ClinicId,
                        Medical = request.Medical,
                        Surgical = request.Surgical,
                        Drugs = request.Drugs,
                        Allergies = request.Allergies,
                        GeneralExamination = request.GeneralExamination,
                        Investigations = request.Investigations,
                        Diagnoses = request.Diagnoses,
                        ProvisionalDiagnosis = request.ProvisionalDiagnosis,
                        Notes = request.Notes,
                        PresentComplaints = request.PresentComplaints,
                        PastHistory = request.PastHistory,
                        CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        CreatedBy = adminId
                    };

                    await _medicalHistoryRepository.CreateAsync(history);

                    _logger.LogInformation("Creating new medical history for ClinicPatient ID: {ClinicPatientId}", clinicPatient.Id);
                }

                // Save all changes - this will either INSERT (create) or UPDATE (modify tracked entity)
                await _medicalHistoryRepository.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "{Action} medical history for ClinicPatient ID: {ClinicPatientId}, Clinic ID: {ClinicId} by {ActionBy}",
                    isUpdate ? "Updated" : "Created", clinicPatient.Id, request.ClinicId, actionByName
                );

                var clinic = await _clinicRepository.GetByIdAsync(request.ClinicId);
                var action = isUpdate ? "updated" : "created";

                var response = new
                {
                    MedicalHistoryId = history.Id,
                    ClinicPatientId = clinicPatient.Id,
                    HFID = request.HFID,
                    PatientName = fullName,
                    ClinicId = request.ClinicId,
                    ClinicName = clinic?.ClinicName ?? "Unknown",
                    Action = isUpdate ? "Updated" : "Created",
                    ActionBy = actionByName,
                    ActionAt = isUpdate ? history.UpdatedAt : history.CreatedAt,
                    NotificationContext = new
                    {
                        MedicalHistoryId = history.Id,
                        PatientName = fullName,
                        PatientHFID = request.HFID,
                        ClinicId = request.ClinicId,
                        ClinicName = clinic?.ClinicName ?? "Unknown",
                        Action = isUpdate ? "updated" : "created",
                        ActionBy = actionByName
                    },
                    NotificationMessage = $"Medical history for {fullName} (HFID: {request.HFID}) was {action} by {actionByName}."
                };

                return Ok(ApiResponseFactory.Success(response, $"Medical history {action} successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating/updating medical history for HFID: {HFID}", request.HFID);

                try
                {
                    await transaction.RollbackAsync();
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Error during transaction rollback");
                }

                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while saving medical history."));
            }
            finally
            {
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





        // GET Medical History by Patient ID
        [HttpGet("{clinicId}/patient/{patientId}/medical-history")]
        [Authorize]
        public async Task<IActionResult> GetMedicalHistory(
            [FromRoute] int patientId,
            [FromRoute] int clinicId)
        {
            HttpContext.Items["Log-Category"] = "Patient Medical History";

            if (patientId <= 0)
            {
                _logger.LogWarning("Invalid Patient ID: {PatientId}", patientId);
                return BadRequest(ApiResponseFactory.Fail("Patient ID must be a positive integer."));
            }

            if (clinicId <= 0)
            {
                _logger.LogWarning("Invalid Clinic ID: {ClinicId}", clinicId);
                return BadRequest(ApiResponseFactory.Fail("Clinic ID must be a positive integer."));
            }

            // Authorization check
            bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
            if (!isAuthorized)
            {
                _logger.LogWarning("Unauthorized medical history access attempt for Clinic ID {ClinicId}", clinicId);
                return Unauthorized(ApiResponseFactory.Fail("You are not authorized to view medical history for this clinic."));
            }

            try
            {
                var history = await _medicalHistoryRepository.GetByClinicPatientIdAsync(patientId, clinicId);

                if (history == null)
                {
                    _logger.LogInformation("No medical history found for Patient ID: {PatientId}, Clinic ID: {ClinicId}", patientId, clinicId);
                    return Ok(ApiResponseFactory.Success("No medical history found for this patient."));
                }

                // Get creator and updater names
                var createdByUser = await GetUserNameByAdminId(history.CreatedBy);
                var updatedByUser = history.UpdatedBy.HasValue
                    ? await GetUserNameByAdminId(history.UpdatedBy.Value)
                    : null;

                var response = new PatientMedicalHistoryResponse
                {
                    Id = history.Id,
                    ClinicPatientId = history.ClinicPatientId,
                    ClinicId = history.ClinicId,
                    HFID = history.ClinicPatient.HFID,
                    PatientName = history.ClinicPatient.PatientName,
                    Medical = history.Medical,
                    Surgical = history.Surgical,
                    Drugs = history.Drugs,
                    Allergies = history.Allergies,
                    GeneralExamination = history.GeneralExamination,
                    Investigations = history.Investigations,
                    Diagnoses = history.Diagnoses,
                    ProvisionalDiagnosis = history.ProvisionalDiagnosis,
                    Notes = history.Notes,
                    PresentComplaints = history.PresentComplaints,
                    PastHistory = history.PastHistory,
                    CreatedAt = history.CreatedAt,
                    UpdatedAt = history.UpdatedAt,
                    CreatedBy = createdByUser ?? "Unknown",
                    UpdatedBy = updatedByUser
                };

                _logger.LogInformation("Medical history retrieved for Patient ID: {PatientId}, Clinic ID: {ClinicId}", patientId, clinicId);
                return Ok(ApiResponseFactory.Success(response, "Medical history retrieved successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving medical history for Patient ID: {PatientId}", patientId);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while retrieving medical history."));
            }
        }

        // Helper method to get user name by admin ID
        private async Task<string?> GetUserNameByAdminId(int adminId)
        {
            var superAdmin = await _context.ClinicSuperAdmins.FirstOrDefaultAsync(sa => sa.Id == adminId);
            if (superAdmin != null)
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == superAdmin.UserId && u.DeletedBy == 0);
                if (user != null)
                    return $"{user.FirstName} {user.LastName}".Trim();
            }

            var member = await _context.ClinicMembers.FirstOrDefaultAsync(m => m.Id == adminId);
            if (member != null)
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == member.UserId && u.DeletedBy == 0);
                if (user != null)
                    return $"{user.FirstName} {user.LastName}".Trim();
            }

            return null;
        }
    }
}