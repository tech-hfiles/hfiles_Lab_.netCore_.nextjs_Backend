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
using OfficeOpenXml;
using System.Security.Claims;
using System.Text;

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
                    existingHistory.Intensity = request.Intensity;
                    existingHistory.Frequency = request.Frequency;
                    existingHistory.Duration = request.Duration;
                    existingHistory.NatureofPain = request.NatureofPain;
                    existingHistory.AggravatingFactors = request.AggravatingFactors;
                    existingHistory.RelievingFacors = request.RelievingFacors;
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
                        Intensity = request.Intensity,
                        Frequency = request.Frequency,
                        Duration = request.Duration,
                        NatureofPain = request.NatureofPain,
                        AggravatingFactors = request.AggravatingFactors,
                        RelievingFacors = request.RelievingFacors,
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

                var userProfile = await _userRepository.GetUserByHFIDAsync(history.ClinicPatient.HFID);

                var response = new PatientMedicalHistoryResponse
                {
                    Id = history.Id,
                    ClinicPatientId = history.ClinicPatientId,
                    ClinicId = history.ClinicId,
                    HFID = history.ClinicPatient.HFID,
                    ProfilePhoto = userProfile?.ProfilePhoto,
                    PatientName = history.ClinicPatient.PatientName,
                    Medical = history.Medical,
                    Surgical = history.Surgical,
                    Drugs = history.Drugs,
                    Allergies = history.Allergies,
                    GeneralExamination = history.GeneralExamination,
                    Investigations = history.Investigations,
                    Diagnoses = history.Diagnoses,
                    ProvisionalDiagnosis = history.ProvisionalDiagnosis,
                    intensity = history.Intensity,
                    frequency = history.Frequency,
                    duration = history.Duration,
                    natureofPain = history.NatureofPain,
                    aggravatingFactors = history.AggravatingFactors,
                    relievingFacors = history.RelievingFacors,
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





        //[HttpPost("import-clinical-notes")]
        //[Authorize]
        //public async Task<IActionResult> ImportClinicalNotesFromExcel([FromForm] ClinicalNotesImportRequest request)
        //{
        //    HttpContext.Items["Log-Category"] = "Clinical Notes Import";

        //    if (request.ExcelFile == null || request.ExcelFile.Length == 0)
        //        return BadRequest(ApiResponseFactory.Fail("Excel file is required."));

        //    var extension = Path.GetExtension(request.ExcelFile.FileName).ToLowerInvariant();
        //    if (extension != ".csv" && extension != ".xlsx")
        //        return BadRequest(ApiResponseFactory.Fail("Only .csv and .xlsx files are supported."));

        //    const int CLINIC_ID = 8;

        //    bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(CLINIC_ID, User);
        //    if (!isAuthorized)
        //    {
        //        _logger.LogWarning("Unauthorized clinical notes import attempt for Clinic ID {ClinicId}", CLINIC_ID);
        //        return Unauthorized(ApiResponseFactory.Fail("You are not authorized to import clinical notes for this clinic."));
        //    }

        //    var response = new ClinicalNotesImportResponse();
        //    var transaction = await _clinicRepository.BeginTransactionAsync();
        //    bool committed = false;

        //    try
        //    {
        //        var adminIdStr = User.FindFirst("ClinicAdminId")?.Value;
        //        if (!int.TryParse(adminIdStr, out int adminId))
        //        {
        //            return Unauthorized(ApiResponseFactory.Fail("Invalid admin ID in token."));
        //        }

        //        // Parse Excel/CSV file
        //        List<ExcelClinicalNoteRow> noteRows = extension == ".csv"
        //            ? await ProcessClinicalNotesCsvFile(request.ExcelFile)
        //            : await ProcessClinicalNotesExcelFile(request.ExcelFile);

        //        if (!noteRows.Any())
        //        {
        //            return BadRequest(ApiResponseFactory.Fail("No valid clinical notes data found in the file."));
        //        }

        //        // Group by patient
        //        var groupedByPatient = noteRows.GroupBy(n => n.PatientId).ToList();

        //        foreach (var patientGroup in groupedByPatient)
        //        {
        //            response.TotalProcessed++;

        //            try
        //            {
        //                var success = await ProcessPatientClinicalNotes(
        //                    patientGroup.Key,
        //                    patientGroup.ToList(),
        //                    CLINIC_ID,
        //                    adminId,
        //                    response);

        //                if (success)
        //                {
        //                    response.Successful++;
        //                    response.PatientsProcessed++;
        //                }
        //                else
        //                {
        //                    response.Failed++;
        //                }
        //            }
        //            catch (Exception ex)
        //            {
        //                _logger.LogError(ex, "Error processing clinical notes for Patient {PatientId}", patientGroup.Key);
        //                response.Failed++;
        //                response.SkippedReasons.Add($"Patient {patientGroup.Key}: Processing error - {ex.Message}");
        //            }
        //        }

        //        // Commit transaction
        //        await transaction.CommitAsync();
        //        committed = true;

        //        response.Message = $"Clinical notes import completed: {response.Successful} successful, " +
        //                          $"{response.Failed} failed out of {response.TotalProcessed} total patients processed.";

        //        _logger.LogInformation("Clinical notes import completed: {Added} added, {Failed} failed",
        //            response.Successful, response.Failed);

        //        return Ok(ApiResponseFactory.Success(response, response.Message));
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Failed to import clinical notes from Excel");
        //        return StatusCode(500, ApiResponseFactory.Fail("Failed to process Excel file: " + ex.Message));
        //    }
        //    finally
        //    {
        //        if (!committed && transaction.GetDbTransaction().Connection != null)
        //            await transaction.RollbackAsync();
        //    }
        //}

        //private async Task<List<ExcelClinicalNoteRow>> ProcessClinicalNotesCsvFile(IFormFile file)
        //{
        //    var notes = new List<ExcelClinicalNoteRow>();

        //    using var reader = new StreamReader(file.OpenReadStream());

        //    // Skip header line
        //    await reader.ReadLineAsync();

        //    string? line;
        //    while ((line = await reader.ReadLineAsync()) != null)
        //    {
        //        if (string.IsNullOrWhiteSpace(line)) continue;

        //        var columns = ParseCsvLine(line);
        //        if (columns.Count >= 7)
        //        {
        //            var note = new ExcelClinicalNoteRow
        //            {
        //                DoctorName = CleanField(columns[0]),
        //                PatientName = CleanField(columns[1]),
        //                PatientId = CleanField(columns[2]),
        //                DateString = CleanField(columns[3]),
        //                Investigation = CleanField(columns[4]),
        //                Diagnosis = CleanField(columns[5]),
        //                Note = CleanField(columns[6])
        //            };

        //            if (DateTime.TryParseExact(note.DateString, "dd-MM-yyyy", null,
        //                System.Globalization.DateTimeStyles.None, out var parsedDate))
        //            {
        //                note.ParsedDate = parsedDate;
        //                notes.Add(note);
        //            }
        //        }
        //    }

        //    return notes;
        //}

        //// Helper method to parse CSV line handling quoted fields
        //private List<string> ParseCsvLine(string line)
        //{
        //    var fields = new List<string>();
        //    var currentField = new StringBuilder();
        //    bool inQuotes = false;

        //    for (int i = 0; i < line.Length; i++)
        //    {
        //        char c = line[i];

        //        if (c == '"')
        //        {
        //            if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
        //            {
        //                // Double quote "" means literal quote
        //                currentField.Append('"');
        //                i++; // Skip next quote
        //            }
        //            else
        //            {
        //                // Toggle quote mode
        //                inQuotes = !inQuotes;
        //            }
        //        }
        //        else if (c == ',' && !inQuotes)
        //        {
        //            // End of field
        //            fields.Add(currentField.ToString());
        //            currentField.Clear();
        //        }
        //        else
        //        {
        //            currentField.Append(c);
        //        }
        //    }

        //    // Add last field
        //    fields.Add(currentField.ToString());

        //    return fields;
        //}

        //// Helper method to clean field - remove quotes and trim
        //private string CleanField(string field)
        //{
        //    if (string.IsNullOrEmpty(field))
        //        return string.Empty;

        //    // Remove leading/trailing quotes and whitespace
        //    field = field.Trim();

        //    // Remove surrounding quotes if present
        //    if (field.StartsWith("\"") && field.EndsWith("\""))
        //    {
        //        field = field.Substring(1, field.Length - 2);
        //    }

        //    // Replace double quotes with single quotes
        //    field = field.Replace("\"\"", "\"");

        //    // Final trim
        //    return field.Trim();
        //}

        //private async Task<List<ExcelClinicalNoteRow>> ProcessClinicalNotesExcelFile(IFormFile file)
        //{
        //    var notes = new List<ExcelClinicalNoteRow>();

        //    using var stream = new MemoryStream();
        //    await file.CopyToAsync(stream);

        //    ExcelPackage.License.SetNonCommercialPersonal("HFiles");
        //    using var package = new ExcelPackage(stream);
        //    var worksheet = package.Workbook.Worksheets[0];

        //    if (worksheet.Dimension == null) return notes;

        //    var rowCount = worksheet.Dimension.End.Row;

        //    for (int row = 2; row <= rowCount; row++)
        //    {
        //        var note = new ExcelClinicalNoteRow
        //        {
        //            DoctorName = worksheet.Cells[row, 1].Text.Trim(),
        //            PatientName = worksheet.Cells[row, 2].Text.Trim(),
        //            PatientId = worksheet.Cells[row, 3].Text.Trim(),
        //            DateString = worksheet.Cells[row, 4].Text.Trim(),
        //            Investigation = worksheet.Cells[row, 5].Text.Trim(),
        //            Diagnosis = worksheet.Cells[row, 6].Text.Trim(),
        //            Note = worksheet.Cells[row, 7].Text.Trim()
        //        };

        //        if (DateTime.TryParseExact(note.DateString, "dd-MM-yyyy", null,
        //            System.Globalization.DateTimeStyles.None, out var parsedDate))
        //        {
        //            note.ParsedDate = parsedDate;
        //            notes.Add(note);
        //        }
        //    }

        //    return notes;
        //}

        //private async Task<bool> ProcessPatientClinicalNotes(
        //    string patientId,
        //    List<ExcelClinicalNoteRow> notes,
        //    int clinicId,
        //    int adminId,
        //    ClinicalNotesImportResponse response)
        //{
        //    // 1. Find user by patientId
        //    var user = await _userRepository.GetUserByPatientIdAsync(patientId);
        //    if (user == null)
        //    {
        //        response.SkippedReasons.Add($"Patient {patientId}: User not found in database");
        //        return false;
        //    }

        //    // 2. Get HFID
        //    if (string.IsNullOrWhiteSpace(user.HfId))
        //    {
        //        response.SkippedReasons.Add($"Patient {patientId}: HFID not found for user");
        //        return false;
        //    }

        //    // 3. Get or create clinic patient
        //    var fullName = $"{user.FirstName} {user.LastName}".Trim();
        //    var clinicPatient = await _clinicVisitRepository.GetOrCreatePatientAsync(user.HfId, fullName);

        //    // 4. Sort notes by date
        //    var sortedNotes = notes.OrderBy(n => n.ParsedDate).ToList();

        //    // 5. Build date-wise formatted strings
        //    var investigationsText = new StringBuilder();
        //    var diagnosesText = new StringBuilder();
        //    var notesText = new StringBuilder();

        //    foreach (var note in sortedNotes)
        //    {
        //        var dateHeader = note.ParsedDate.ToString("dd-MM-yyyy");

        //        if (!string.IsNullOrWhiteSpace(note.Investigation))
        //        {
        //            investigationsText.AppendLine(dateHeader);
        //            investigationsText.AppendLine(note.Investigation);
        //            investigationsText.AppendLine();
        //        }

        //        if (!string.IsNullOrWhiteSpace(note.Diagnosis))
        //        {
        //            diagnosesText.AppendLine(dateHeader);
        //            diagnosesText.AppendLine(note.Diagnosis);
        //            diagnosesText.AppendLine();
        //        }

        //        if (!string.IsNullOrWhiteSpace(note.Note))
        //        {
        //            notesText.AppendLine(dateHeader);
        //            notesText.AppendLine(note.Note);
        //            notesText.AppendLine();
        //        }
        //    }

        //    // 6. ALWAYS CREATE NEW RECORD - NO UPDATE CHECK
        //    var newHistory = new ClinicPatientMedicalHistory
        //    {
        //        ClinicPatientId = clinicPatient.Id,
        //        ClinicId = clinicId,
        //        Investigations = investigationsText.ToString().Trim(),
        //        Diagnoses = diagnosesText.ToString().Trim(),
        //        Notes = notesText.ToString().Trim(),
        //        CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        //        CreatedBy = adminId,
        //        DeletedBy = 0,
        //        Medical = null,
        //        Surgical = null,
        //        Drugs = null,
        //        Allergies = null,
        //        GeneralExamination = null,
        //        ProvisionalDiagnosis = null,
        //        PresentComplaints = null,
        //        PastHistory = null
        //    };

        //    // Use repository CreateAsync method
        //    await _medicalHistoryRepository.CreateAsync(newHistory);

        //    // CRITICAL: Save changes immediately
        //    await _medicalHistoryRepository.SaveChangesAsync();

        //    _logger.LogInformation("Created new medical history for ClinicPatient ID: {ClinicPatientId}, Clinic ID: {ClinicId}",
        //        clinicPatient.Id, clinicId);

        //    // 7. Add to success summary
        //    response.AddedNotes.Add(new AddedClinicalNotesSummary
        //    {
        //        PatientId = patientId,
        //        PatientName = fullName,
        //        HFID = user.HfId,
        //        TotalVisitNotes = sortedNotes.Count,
        //        VisitDates = sortedNotes.Select(n => n.ParsedDate.ToString("dd-MM-yyyy")).Distinct().ToList()
        //    });

        //    return true;
        //}
    }
}