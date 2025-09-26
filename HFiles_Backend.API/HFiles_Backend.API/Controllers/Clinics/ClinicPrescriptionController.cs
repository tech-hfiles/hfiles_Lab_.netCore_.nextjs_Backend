using HFiles_Backend.API.DTOs.Clinics;
using HFiles_Backend.API.Interfaces;
using HFiles_Backend.Application.Common;
using HFiles_Backend.Application.DTOs.Clinics.Prescription;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Enums;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;
using Newtonsoft.Json;
using OfficeOpenXml;
using System.Globalization;

namespace HFiles_Backend.API.Controllers.Clinics
{
    [Route("api/")]
    [ApiController]
    public class ClinicPrescriptionController(
    IClinicPrescriptionRepository clinicPrescriptionRepository,
    IClinicRepository clinicRepository,
    IClinicAuthorizationService clinicAuthorizationService,
    ILogger<ClinicPrescriptionController> logger,
    IUserRepository userRepository,
    IClinicVisitRepository clinicVisitRepository,
    IClinicPatientRecordRepository clinicPatientRecordRepository
    ) : ControllerBase
    {
        private readonly IClinicPrescriptionRepository _clinicalPrescriptionRepository = clinicPrescriptionRepository;
        private readonly IClinicRepository _clinicRepository = clinicRepository;
        private readonly IClinicAuthorizationService _clinicAuthorizationService = clinicAuthorizationService;
        private readonly ILogger<ClinicPrescriptionController> _logger = logger;
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IClinicVisitRepository _clinicVisitRepository = clinicVisitRepository;
        private readonly IClinicPatientRecordRepository _clinicPatientRecordRepository = clinicPatientRecordRepository;

        private const int CLINIC_ID = 8; // Fixed clinic ID as per requirements
        private const string DOCTOR_NAME = "Dr. Varun R Kunte";
        private readonly TimeSpan APPOINTMENT_TIME = new(10, 0, 0); // 10:00:00





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
                    PrescriptionId = p.Id,
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





        // Arthrose Prescription Data
        [HttpPost("prescription/import-excel")]
        public async Task<IActionResult> ImportPrescriptionsFromExcel([FromForm] PrescriptionImportRequest request)
        {
            HttpContext.Items["Log-Category"] = "Prescription Import";

            // Basic validation
            if (request.ExcelFile == null || request.ExcelFile.Length == 0)
                return BadRequest(ApiResponseFactory.Fail("Excel file is required."));

            if (!Path.GetExtension(request.ExcelFile.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(ApiResponseFactory.Fail("Only .xlsx files are supported."));

            var response = new PrescriptionImportResponse();
            var transaction = await _clinicRepository.BeginTransactionAsync();
            var committed = false;

            try
            {
                using var stream = new MemoryStream();
                await request.ExcelFile.CopyToAsync(stream);

                ExcelPackage.License.SetNonCommercialPersonal("HFiles");
                using var package = new ExcelPackage(stream);
                var worksheet = package.Workbook.Worksheets[0];

                if (worksheet.Dimension == null)
                    return BadRequest(ApiResponseFactory.Fail("Excel file is empty."));

                var rowCount = worksheet.Dimension.End.Row;
                _logger.LogInformation("Processing {RowCount} rows from Excel", rowCount - 1);

                // Group data by patient and date to handle multiple medications
                var groupedData = ProcessExcelData(worksheet, rowCount, response);

                // Process each patient-date combination
                foreach (var patientGroup in groupedData)
                {
                    await ProcessPatientPrescriptions(patientGroup.Key, patientGroup.Value, response);
                }

                await _clinicRepository.SaveChangesAsync();
                await transaction.CommitAsync();
                committed = true;

                response.Message = $"Import completed successfully: {response.SuccessfullyAdded} prescriptions added, " +
                                  $"{response.Skipped} entries skipped out of {response.TotalProcessed} total entries. " +
                                  $"Processed {response.PatientsProcessed} patients with {response.VisitsCreated} visits.";

                _logger.LogInformation("Prescription import completed: {Added} added, {Skipped} skipped",
                    response.SuccessfullyAdded, response.Skipped);

                return Ok(ApiResponseFactory.Success(response, response.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import prescriptions from Excel");
                return StatusCode(500, ApiResponseFactory.Fail("Failed to process Excel file: " + ex.Message));
            }
            finally
            {
                if (!committed && transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.RollbackAsync();
                }
            }
        }

        private Dictionary<string, List<ExcelPrescriptionRow>> ProcessExcelData(
            ExcelWorksheet worksheet, int rowCount, PrescriptionImportResponse response)
        {
            var groupedData = new Dictionary<string, List<ExcelPrescriptionRow>>();

            for (int row = 2; row <= rowCount; row++)
            {
                try
                {
                    var prescriptionData = ExtractPrescriptionDataFromRow(worksheet, row);
                    response.TotalProcessed++;

                    // Validate required fields
                    var validationResult = ValidatePrescriptionData(prescriptionData, row);
                    if (!validationResult.IsValid)
                    {
                        response.SkippedReasons.Add($"Row {row}: {validationResult.ErrorMessage}");
                        response.Skipped++;
                        continue;
                    }

                    // Parse date
                    var dateResult = ParseDateFromExcel(prescriptionData.DateString);
                    if (!dateResult.IsValid)
                    {
                        response.SkippedReasons.Add($"Row {row}: {dateResult.ErrorMessage}");
                        response.Skipped++;
                        continue;
                    }

                    prescriptionData.ParsedDate = dateResult.Date;

                    // Group by PatientId + Date
                    var groupKey = $"{prescriptionData.PatientId}_{dateResult.Date:yyyy-MM-dd}";
                    if (!groupedData.ContainsKey(groupKey))
                        groupedData[groupKey] = new List<ExcelPrescriptionRow>();

                    groupedData[groupKey].Add(prescriptionData);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing row {Row}", row);
                    response.SkippedReasons.Add($"Row {row}: Processing error - {ex.Message}");
                    response.Skipped++;
                }
            }

            return groupedData;
        }

        private async Task ProcessPatientPrescriptions(string groupKey,
            List<ExcelPrescriptionRow> prescriptions, PrescriptionImportResponse response)
        {
            var firstPrescription = prescriptions.First();

            // Step 1: Get or create patient
            var user = await _userRepository.GetUserByPatientIdAsync(firstPrescription.PatientId);
            if (user == null)
            {
                response.SkippedReasons.Add($"Patient {firstPrescription.PatientId}: User not found in database");
                response.Skipped++;
                return;
            }

            var clinicPatient = await GetOrCreateClinicPatient(user);

            // Step 2: Check if visit already exists for this patient-date combination
            var existingVisit = await GetExistingVisit(clinicPatient.Id, firstPrescription.ParsedDate);
            ClinicVisit visit;

            if (existingVisit == null)
            {
                // Create new visit
                visit = new ClinicVisit
                {
                    ClinicPatientId = clinicPatient.Id,
                    ClinicId = CLINIC_ID,
                    AppointmentDate = firstPrescription.ParsedDate.Date,
                    AppointmentTime = APPOINTMENT_TIME,
                    PaymentMethod = null
                };

                await _clinicVisitRepository.SaveVisitAsync(visit);
                response.VisitsCreated++;
            }
            else
            {
                visit = existingVisit;
            }

            // Step 3: Check if prescription record already exists for this visit
            var existingRecord = await _clinicPatientRecordRepository.GetByCompositeKeyAsync(
                CLINIC_ID, clinicPatient.Id, visit.Id, RecordType.Prescription);

            if (existingRecord != null)
            {
                response.SkippedReasons.Add($"Patient {firstPrescription.PatientId} on {firstPrescription.ParsedDate:yyyy-MM-dd}: Prescription already exists");
                response.Skipped++;
                return;
            }

            // Step 4: Create prescription record with JSON data
            var jsonData = CreatePrescriptionJsonData(user, clinicPatient, prescriptions, firstPrescription.PatientId);

            var prescriptionRecord = new ClinicPatientRecord
            {
                ClinicId = CLINIC_ID,
                PatientId = clinicPatient.Id,
                ClinicVisitId = visit.Id,
                Type = RecordType.Prescription,
                JsonData = jsonData,
                SendToPatient = false
            };

            await _clinicPatientRecordRepository.SaveAsync(prescriptionRecord);

            response.SuccessfullyAdded++;
            response.PatientsProcessed++;

            var addedPrescription = new AddedPrescriptionSummary
            {
                PatientId = firstPrescription.PatientId,
                PatientName = $"{user.FirstName} {user.LastName}",
                HFID = user.HfId ?? "",
                Date = firstPrescription.ParsedDate.ToString("yyyy-MM-dd"),
                MedicationCount = prescriptions.Count,
                Medications = prescriptions.Select(p => p.DrugName).ToList()
            };

            response.AddedPrescriptions.Add(addedPrescription);
        }

        private async Task<ClinicPatient> GetOrCreateClinicPatient(Domain.Entities.Users.User user)
        {
            var fullName = $"{user.FirstName} {user.LastName}".Trim();
            return await _clinicVisitRepository.GetOrCreatePatientAsync(user.HfId ?? "", fullName);
        }

        private async Task<ClinicVisit?> GetExistingVisit(int clinicPatientId, DateTime appointmentDate)
        {
            return await _clinicVisitRepository.GetExistingVisitAsync(clinicPatientId, appointmentDate);
        }

        private string CreatePrescriptionJsonData(Domain.Entities.Users.User user, ClinicPatient clinicPatient,
            List<ExcelPrescriptionRow> prescriptions, string patientId)
        {
            var medications = prescriptions.Select(p => new
            {
                name = p.DrugName,
                dosage = p.Dosage,
                frequency = p.Duration, // Mapping duration to frequency as per your requirement
                timing = p.Direction,
                instruction = p.Advice
            }).ToArray();

            var prescriptionData = new
            {
                patient = new
                {
                    name = $"{user.FirstName} {user.LastName}".Trim(),
                    hfid = user.HfId ?? "",
                    gender = user.Gender ?? "",
                    prfid = patientId,
                    dob = user.DOB ?? "",
                    mobile = user.PhoneNumber ?? "",
                    doctor = DOCTOR_NAME,
                    city = "" // Keep blank as per requirement
                },
                medications = medications,
                additionalNotes = "",
                clinicInfo = new
                {
                    name = "Arthrose",
                    subtitle = "CRANIOFACIAL PAIN & TMJ CENTRE",
                    website = "www.arthrosetmjindia.com"
                },
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };

            return JsonConvert.SerializeObject(prescriptionData, Formatting.None);
        }

        private ExcelPrescriptionRow ExtractPrescriptionDataFromRow(ExcelWorksheet worksheet, int row)
        {
            return new ExcelPrescriptionRow
            {
                PatientId = worksheet.Cells[row, 1].Text.Trim(), // Column A
                PatientName = worksheet.Cells[row, 2].Text.Trim(), // Column B
                DrugName = worksheet.Cells[row, 3].Text.Trim(), // Column C
                Duration = worksheet.Cells[row, 4].Text.Trim(), // Column D
                Dosage = worksheet.Cells[row, 5].Text.Trim(), // Column E
                Direction = worksheet.Cells[row, 6].Text.Trim(), // Column F
                Advice = worksheet.Cells[row, 7].Text.Trim(), // Column G
                DateString = worksheet.Cells[row, 8].Text.Trim() // Column H
            };
        }

        private (bool IsValid, string ErrorMessage) ValidatePrescriptionData(ExcelPrescriptionRow data, int row)
        {
            if (string.IsNullOrWhiteSpace(data.PatientId))
                return (false, "Patient ID is required");

            if (string.IsNullOrWhiteSpace(data.PatientName))
                return (false, "Patient name is required");

            if (string.IsNullOrWhiteSpace(data.DrugName))
                return (false, "Drug name is required");

            if (string.IsNullOrWhiteSpace(data.DateString))
                return (false, "Date is required");

            return (true, "");
        }

        private (bool IsValid, DateTime Date, string ErrorMessage) ParseDateFromExcel(string dateString)
        {
            try
            {
                // Try to parse as Excel date or ISO format
                if (DateTime.TryParse(dateString, out var parsedDate))
                {
                    return (true, parsedDate.Date, "");
                }

                // Try different date formats
                string[] formats = { "yyyy-MM-dd", "dd-MM-yyyy", "MM/dd/yyyy", "dd/MM/yyyy" };
                foreach (var format in formats)
                {
                    if (DateTime.TryParseExact(dateString, format, null, DateTimeStyles.None, out parsedDate))
                    {
                        return (true, parsedDate.Date, "");
                    }
                }

                return (false, default, $"Unable to parse date: {dateString}");
            }
            catch (Exception ex)
            {
                return (false, default, $"Error parsing date '{dateString}': {ex.Message}");
            }
        }
    }
}
