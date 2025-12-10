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
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OfficeOpenXml;
using System.Globalization;
using System.Text;

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
                    Duration = request.Duration,
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
                    Instructions = p.Instructions,
                    Duration = p.Duration
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
                if (request.Duration != null) existing.Duration = request.Duration;
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

        [HttpDelete("clinic/{clinicId}/prescription/{prescriptionId}/delete")]
        [Authorize]
        public async Task<IActionResult> DeletePrescription(
        [FromRoute] int clinicId,
        [FromRoute] int prescriptionId)
        {
            HttpContext.Items["Log-Category"] = "Prescription Delete";

            if (clinicId <= 0 || prescriptionId <= 0)
            {
                _logger.LogWarning("Invalid Clinic ID or Prescription ID. ClinicId: {ClinicId}, PrescriptionId: {PrescriptionId}", clinicId, prescriptionId);
                return BadRequest(ApiResponseFactory.Fail("Clinic ID and Prescription ID must be positive integers."));
            }

            bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
            if (!isAuthorized)
            {
                _logger.LogWarning("Unauthorized delete attempt for Clinic ID {ClinicId}", clinicId);
                return Unauthorized(ApiResponseFactory.Fail("You are not authorized to delete prescriptions for this clinic."));
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

                await _clinicalPrescriptionRepository.DeletePrescriptionAsync(prescriptionId);

                await transaction.CommitAsync();
                committed = true;

                _logger.LogInformation("Prescription deleted. ClinicId: {ClinicId}, PrescriptionId: {PrescriptionId}", clinicId, prescriptionId);
                return Ok(ApiResponseFactory.Success("Prescription deleted successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting prescription. ClinicId: {ClinicId}, PrescriptionId: {PrescriptionId}", clinicId, prescriptionId);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while deleting the prescription."));
            }
            finally
            {
                if (!committed && transaction.GetDbTransaction().Connection != null)
                    await transaction.RollbackAsync();
            }
        }


        // ********************************************************************************************************************


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





        // Historical Prescription Import API (2020-2024)
        // Add this to your existing controller
        // Uses existing ClinicPatientRecord table - NO NEW TABLES NEEDED

        [HttpPost("prescription/import-historical-excel")]
        public async Task<IActionResult> ImportHistoricalPrescriptionsFromExcel([FromForm] HistoricalPrescriptionImportRequest request)
        {
            HttpContext.Items["Log-Category"] = "Historical Prescription Import";

            // Basic validation
            if (request.ExcelFile == null || request.ExcelFile.Length == 0)
                return BadRequest(ApiResponseFactory.Fail("Excel file is required."));

            var fileExtension = Path.GetExtension(request.ExcelFile.FileName).ToLower();
            if (!fileExtension.Equals(".xlsx") && !fileExtension.Equals(".csv"))
                return BadRequest(ApiResponseFactory.Fail("Only .xlsx and .csv files are supported."));

            var response = new HistoricalPrescriptionImportResponse();
            var transaction = await _clinicRepository.BeginTransactionAsync();
            var committed = false;

            try
            {
                using var stream = new MemoryStream();
                await request.ExcelFile.CopyToAsync(stream);

                List<ExcelHistoricalPrescriptionRow> allRows;

                if (fileExtension.Equals(".csv"))
                {
                    allRows = ProcessHistoricalCsvFile(stream, response);
                }
                else
                {
                    allRows = ProcessHistoricalExcelFile(stream, response);
                }

                // Group data by patient and date (same as current 2025 API)
                var groupedData = allRows
                    .GroupBy(r => $"{r.PatientId}_{r.ParsedDate:yyyy-MM-dd}")
                    .ToDictionary(g => g.Key, g => g.ToList());

                _logger.LogInformation("Processing {GroupCount} patient-date combinations", groupedData.Count);

                // Process each patient-date group
                foreach (var group in groupedData)
                {
                    await ProcessHistoricalPatientGroup(group.Key, group.Value, response);
                }

                await _clinicRepository.SaveChangesAsync();
                await transaction.CommitAsync();
                committed = true;

                response.Message = $"Import completed successfully: {response.SuccessfullyAdded} prescriptions added, " +
                                  $"{response.Skipped} entries skipped (including {response.SkippedEmptyDrugName} with empty drug names) " +
                                  $"out of {response.TotalProcessed} total entries. " +
                                  $"Processed {response.PatientsProcessed} patients with {response.VisitsCreated} visits.";

                _logger.LogInformation("Historical prescription import completed: {Added} added, {Skipped} skipped",
                    response.SuccessfullyAdded, response.Skipped);

                return Ok(ApiResponseFactory.Success(response, response.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import historical prescriptions");
                return StatusCode(500, ApiResponseFactory.Fail("Failed to process file: " + ex.Message));
            }
            finally
            {
                if (!committed && transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.RollbackAsync();
                }
            }
        }

        private List<ExcelHistoricalPrescriptionRow> ProcessHistoricalCsvFile(MemoryStream stream, HistoricalPrescriptionImportResponse response)
        {
            var rows = new List<ExcelHistoricalPrescriptionRow>();
            stream.Position = 0;

            using var reader = new StreamReader(stream);
            string? line;
            int rowNumber = 0;
            bool isHeader = true;

            while ((line = reader.ReadLine()) != null)
            {
                rowNumber++;

                if (isHeader)
                {
                    isHeader = false;
                    continue;
                }

                try
                {
                    var values = ParseCsvLine(line);

                    if (values.Length < 8)
                    {
                        response.SkippedReasons.Add($"Row {rowNumber}: Insufficient columns");
                        response.Skipped++;
                        response.TotalProcessed++;
                        continue;
                    }

                    var row = new ExcelHistoricalPrescriptionRow
                    {
                        PatientId = values[0].Trim(),
                        PatientName = values[1].Trim(),
                        DrugName = values[2].Trim(),
                        Duration = values[3].Trim(),
                        Dosage = values[4].Trim(),
                        Direction = values[5].Trim(),
                        Advice = values[6].Trim(),
                        DateString = values[7].Trim()
                    };

                    response.TotalProcessed++;

                    // Skip if drug name is empty - CRITICAL FOR YOUR USE CASE
                    if (string.IsNullOrWhiteSpace(row.DrugName))
                    {
                        response.SkippedReasons.Add($"Row {rowNumber}: Empty drug name");
                        response.Skipped++;
                        response.SkippedEmptyDrugName++;
                        continue;
                    }

                    // Validate required fields
                    if (string.IsNullOrWhiteSpace(row.PatientId))
                    {
                        response.SkippedReasons.Add($"Row {rowNumber}: Patient ID is required");
                        response.Skipped++;
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(row.PatientName))
                    {
                        response.SkippedReasons.Add($"Row {rowNumber}: Patient name is required");
                        response.Skipped++;
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(row.DateString))
                    {
                        response.SkippedReasons.Add($"Row {rowNumber}: Date is required");
                        response.Skipped++;
                        continue;
                    }

                    // Parse date
                    var dateResult = ParseDateFromExcel(row.DateString);
                    if (!dateResult.IsValid)
                    {
                        response.SkippedReasons.Add($"Row {rowNumber}: {dateResult.ErrorMessage}");
                        response.Skipped++;
                        continue;
                    }

                    row.ParsedDate = dateResult.Date;
                    rows.Add(row);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing CSV row {Row}", rowNumber);
                    response.SkippedReasons.Add($"Row {rowNumber}: Processing error - {ex.Message}");
                    response.Skipped++;
                }
            }

            return rows;
        }

        private List<ExcelHistoricalPrescriptionRow> ProcessHistoricalExcelFile(MemoryStream stream, HistoricalPrescriptionImportResponse response)
        {
            var rows = new List<ExcelHistoricalPrescriptionRow>();

            ExcelPackage.License.SetNonCommercialPersonal("HFiles");
            using var package = new ExcelPackage(stream);
            var worksheet = package.Workbook.Worksheets[0];

            if (worksheet.Dimension == null)
                return rows;

            var rowCount = worksheet.Dimension.End.Row;
            _logger.LogInformation("Processing {RowCount} rows from Excel", rowCount - 1);

            for (int rowNumber = 2; rowNumber <= rowCount; rowNumber++)
            {
                try
                {
                    var row = new ExcelHistoricalPrescriptionRow
                    {
                        PatientId = worksheet.Cells[rowNumber, 1].Text.Trim(),
                        PatientName = worksheet.Cells[rowNumber, 2].Text.Trim(),
                        DrugName = worksheet.Cells[rowNumber, 3].Text.Trim(),
                        Duration = worksheet.Cells[rowNumber, 4].Text.Trim(),
                        Dosage = worksheet.Cells[rowNumber, 5].Text.Trim(),
                        Direction = worksheet.Cells[rowNumber, 6].Text.Trim(),
                        Advice = worksheet.Cells[rowNumber, 7].Text.Trim(),
                        DateString = worksheet.Cells[rowNumber, 8].Text.Trim()
                    };

                    response.TotalProcessed++;

                    // Skip if drug name is empty - CRITICAL FOR YOUR USE CASE
                    if (string.IsNullOrWhiteSpace(row.DrugName))
                    {
                        response.SkippedReasons.Add($"Row {rowNumber}: Empty drug name");
                        response.Skipped++;
                        response.SkippedEmptyDrugName++;
                        continue;
                    }

                    // Validate required fields
                    if (string.IsNullOrWhiteSpace(row.PatientId))
                    {
                        response.SkippedReasons.Add($"Row {rowNumber}: Patient ID is required");
                        response.Skipped++;
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(row.PatientName))
                    {
                        response.SkippedReasons.Add($"Row {rowNumber}: Patient name is required");
                        response.Skipped++;
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(row.DateString))
                    {
                        response.SkippedReasons.Add($"Row {rowNumber}: Date is required");
                        response.Skipped++;
                        continue;
                    }

                    // Parse date
                    var dateResult = ParseDateFromExcel(row.DateString);
                    if (!dateResult.IsValid)
                    {
                        response.SkippedReasons.Add($"Row {rowNumber}: {dateResult.ErrorMessage}");
                        response.Skipped++;
                        continue;
                    }

                    row.ParsedDate = dateResult.Date;
                    rows.Add(row);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing Excel row {Row}", rowNumber);
                    response.SkippedReasons.Add($"Row {rowNumber}: Processing error - {ex.Message}");
                    response.Skipped++;
                }
            }

            return rows;
        }

        private async Task ProcessHistoricalPatientGroup(string groupKey, List<ExcelHistoricalPrescriptionRow> prescriptions, HistoricalPrescriptionImportResponse response)
        {
            var firstPrescription = prescriptions.First();

            // Step 1: Get or create clinic patient (uses PatientId as HFID for historical data)
            var clinicPatient = await _clinicVisitRepository.GetOrCreatePatientAsync(
                firstPrescription.PatientId,
                firstPrescription.PatientName
            );

            // Step 2: Check if visit already exists for this patient-date combination
            var existingVisit = await _clinicVisitRepository.GetExistingVisitAsync(
                clinicPatient.Id,
                firstPrescription.ParsedDate
            );

            ClinicVisit visit;

            if (existingVisit == null)
            {
                // Create new visit for historical data
                visit = new ClinicVisit
                {
                    ClinicPatientId = clinicPatient.Id,
                    ClinicId = CLINIC_ID,
                    AppointmentDate = firstPrescription.ParsedDate.Date,
                    AppointmentTime = new TimeSpan(0, 0, 0), // Midnight for historical data
                    PaymentMethod = null,
                    //Notes = "Historical import (2020-2024)"
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
                CLINIC_ID,
                clinicPatient.Id,
                visit.Id,
                RecordType.Prescription
            );

            if (existingRecord != null)
            {
                response.SkippedReasons.Add(
                    $"Patient {firstPrescription.PatientId} on {firstPrescription.ParsedDate:yyyy-MM-dd}: Prescription already exists"
                );
                response.Skipped += prescriptions.Count;
                return;
            }

            // Step 4: Create prescription record with JSON data (SAME FORMAT AS YOUR CURRENT API)
            var jsonData = CreateHistoricalPrescriptionJsonData(firstPrescription, prescriptions);

            var prescriptionRecord = new ClinicPatientRecord
            {
                ClinicId = CLINIC_ID,
                PatientId = clinicPatient.Id,
                ClinicVisitId = visit.Id,
                Type = RecordType.Prescription,
                JsonData = jsonData,
                SendToPatient = false,
                UniqueRecordId = null
            };

            await _clinicPatientRecordRepository.SaveAsync(prescriptionRecord);

            response.SuccessfullyAdded++;
            response.PatientsProcessed++;

            var summary = new AddedHistoricalPrescriptionSummary
            {
                PatientId = firstPrescription.PatientId,
                PatientName = firstPrescription.PatientName,
                Date = firstPrescription.ParsedDate.ToString("yyyy-MM-dd"),
                MedicationCount = prescriptions.Count,
                Medications = prescriptions.Select(p => p.DrugName).ToList()
            };

            response.AddedPrescriptions.Add(summary);
        }

        private string CreateHistoricalPrescriptionJsonData(ExcelHistoricalPrescriptionRow firstPrescription, List<ExcelHistoricalPrescriptionRow> prescriptions)
        {
            // Create medications array - SAME FORMAT AS YOUR CURRENT 2025 API
            var medications = prescriptions.Select(p => new
            {
                name = p.DrugName,
                dosage = p.Dosage,
                frequency = p.Duration,
                timing = p.Direction,
                instruction = p.Advice
            }).ToArray();

            // Create prescription data - SAME FORMAT AS YOUR CURRENT 2025 API
            var prescriptionData = new
            {
                patient = new
                {
                    name = firstPrescription.PatientName,
                    hfid = firstPrescription.PatientId, // Using PatientId as HFID for historical data
                    gender = "",
                    prfid = firstPrescription.PatientId,
                    dob = "",
                    mobile = "",
                    doctor = DOCTOR_NAME,
                    city = "",
                    isHistoricalData = true // Flag to identify historical imports
                },
                medications = medications,
                additionalNotes = "Historical data import (2020-2024)",
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

        private string[] ParseCsvLine(string line)
        {
            var values = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    values.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            values.Add(current.ToString());
            return values.ToArray();
        }




        // Prescripatin Notes Api

        [HttpPost("clinic/{clinicId}/prescription-note")]
        [Authorize]
        public async Task<IActionResult> SavePrescriptionNote(
        [FromRoute] int clinicId,
        [FromBody] PrescriptionNoteCreateRequest request)
        {
            HttpContext.Items["Log-Category"] = "Prescription Note Save";

            if (clinicId <= 0)
            {
                _logger.LogWarning("Invalid Clinic ID: {ClinicId}", clinicId);
                return BadRequest(ApiResponseFactory.Fail("Clinic ID must be a positive integer."));
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed for prescription note. Errors: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
            if (!isAuthorized)
            {
                _logger.LogWarning("Unauthorized prescription note attempt for Clinic ID {ClinicId}", clinicId);
                return Unauthorized(ApiResponseFactory.Fail("You are not authorized to save prescription notes for this clinic."));
            }

            await using var transaction = await _clinicRepository.BeginTransactionAsync();
            bool committed = false;

            try
            {
                var prescriptionNote = new ClinicPrescriptionNotes
                {
                    ClinicId = clinicId,
                    Notes = request.Notes
                };

                var savedNote = await _clinicalPrescriptionRepository.SavePrescriptionNoteAsync(prescriptionNote);
                await transaction.CommitAsync();
                committed = true;

                _logger.LogInformation("Prescription note saved for Clinic ID {ClinicId}", clinicId);
                return Ok(ApiResponseFactory.Success("Prescription note saved successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving prescription note for Clinic ID {ClinicId}", clinicId);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while saving the prescription note."));
            }
            finally
            {
                if (!committed && transaction.GetDbTransaction().Connection != null)
                    await transaction.RollbackAsync();
            }
        }


        [HttpGet("clinic/{clinicId}/prescription-notes")]
        [Authorize]
        public async Task<IActionResult> GetPrescriptionNotes([FromRoute] int clinicId)
        {
            HttpContext.Items["Log-Category"] = "Prescription Notes Fetch";

            if (clinicId <= 0)
            {
                _logger.LogWarning("Invalid Clinic ID received: {ClinicId}", clinicId);
                return BadRequest(ApiResponseFactory.Fail("Clinic ID must be a positive integer."));
            }

            bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
            if (!isAuthorized)
            {
                _logger.LogWarning("Unauthorized prescription notes fetch attempt for Clinic ID {ClinicId}", clinicId);
                return Unauthorized(ApiResponseFactory.Fail("You are not authorized to view prescription notes for this clinic."));
            }

            try
            {
                var notes = await _clinicalPrescriptionRepository.GetPrescriptionNotesByClinicIdAsync(clinicId);

                var response = notes.Select(n => new PrescriptionNoteResponse
                {
                    Id = n.Id,
                    Notes = n.Notes
                }).ToList();

                _logger.LogInformation("Fetched {Count} prescription notes for Clinic ID {ClinicId}", response.Count, clinicId);
                return Ok(ApiResponseFactory.Success(response, "Prescription notes fetched successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching prescription notes for Clinic ID {ClinicId}", clinicId);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while fetching prescription notes."));
            }
        }


        [HttpPatch("clinic/{clinicId}/prescription-note/{noteId}")]
        [Authorize]
        public async Task<IActionResult> UpdatePrescriptionNote(
        [FromRoute] int clinicId,
        [FromRoute] int noteId,
        [FromBody] PrescriptionNoteUpdateRequest request)
        {
            HttpContext.Items["Log-Category"] = "Prescription Note Update";

            if (clinicId <= 0 || noteId <= 0)
            {
                _logger.LogWarning("Invalid Clinic ID or Note ID. ClinicId: {ClinicId}, NoteId: {NoteId}", clinicId, noteId);
                return BadRequest(ApiResponseFactory.Fail("Clinic ID and Note ID must be positive integers."));
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed for prescription note update. Errors: {@Errors}", errors);
                return BadRequest(ApiResponseFactory.Fail(errors));
            }

            bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
            if (!isAuthorized)
            {
                _logger.LogWarning("Unauthorized prescription note update attempt for Clinic ID {ClinicId}", clinicId);
                return Unauthorized(ApiResponseFactory.Fail("You are not authorized to update prescription notes for this clinic."));
            }

            await using var transaction = await _clinicRepository.BeginTransactionAsync();
            bool committed = false;

            try
            {
                var existing = await _clinicalPrescriptionRepository.GetPrescriptionNoteByIdAsync(noteId);
                if (existing == null || existing.ClinicId != clinicId)
                {
                    _logger.LogWarning("Prescription note not found or mismatched clinic. ClinicId: {ClinicId}, NoteId: {NoteId}", clinicId, noteId);
                    return NotFound(ApiResponseFactory.Fail("Prescription note not found for the specified clinic."));
                }

                if (!string.IsNullOrWhiteSpace(request.Notes))
                {
                    existing.Notes = request.Notes;
                }

                await _clinicalPrescriptionRepository.UpdatePrescriptionNoteAsync(existing);
                await transaction.CommitAsync();
                committed = true;

                _logger.LogInformation("Prescription note updated. ClinicId: {ClinicId}, NoteId: {NoteId}", clinicId, noteId);
                return Ok(ApiResponseFactory.Success("Prescription note updated successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating prescription note. ClinicId: {ClinicId}, NoteId: {NoteId}", clinicId, noteId);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while updating the prescription note."));
            }
            finally
            {
                if (!committed && transaction.GetDbTransaction().Connection != null)
                    await transaction.RollbackAsync();
            }
        }


        [HttpDelete("clinic/{clinicId}/prescription-note/{noteId}")]
        [Authorize]
        public async Task<IActionResult> DeletePrescriptionNote(
        [FromRoute] int clinicId,
        [FromRoute] int noteId)
        {
            HttpContext.Items["Log-Category"] = "Prescription Note Delete";

            if (clinicId <= 0 || noteId <= 0)
            {
                _logger.LogWarning("Invalid Clinic ID or Note ID. ClinicId: {ClinicId}, NoteId: {NoteId}", clinicId, noteId);
                return BadRequest(ApiResponseFactory.Fail("Clinic ID and Note ID must be positive integers."));
            }

            bool isAuthorized = await _clinicAuthorizationService.IsClinicAuthorized(clinicId, User);
            if (!isAuthorized)
            {
                _logger.LogWarning("Unauthorized prescription note delete attempt for Clinic ID {ClinicId}", clinicId);
                return Unauthorized(ApiResponseFactory.Fail("You are not authorized to delete prescription notes for this clinic."));
            }

            await using var transaction = await _clinicRepository.BeginTransactionAsync();
            bool committed = false;

            try
            {
                var existing = await _clinicalPrescriptionRepository.GetPrescriptionNoteByIdAsync(noteId);
                if (existing == null || existing.ClinicId != clinicId)
                {
                    _logger.LogWarning("Prescription note not found or mismatched clinic. ClinicId: {ClinicId}, NoteId: {NoteId}", clinicId, noteId);
                    return NotFound(ApiResponseFactory.Fail("Prescription note not found for the specified clinic."));
                }

                await _clinicalPrescriptionRepository.DeletePrescriptionNoteAsync(noteId);
                await transaction.CommitAsync();
                committed = true;

                _logger.LogInformation("Prescription note deleted. ClinicId: {ClinicId}, NoteId: {NoteId}", clinicId, noteId);
                return Ok(ApiResponseFactory.Success("Prescription note deleted successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting prescription note. ClinicId: {ClinicId}, NoteId: {NoteId}", clinicId, noteId);
                return StatusCode(500, ApiResponseFactory.Fail("An error occurred while deleting the prescription note."));
            }
            finally
            {
                if (!committed && transaction.GetDbTransaction().Connection != null)
                    await transaction.RollbackAsync();
            }
        }
    }
}
