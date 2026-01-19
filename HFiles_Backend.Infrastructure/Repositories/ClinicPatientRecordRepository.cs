using HFiles_Backend.Application.DTOs.Clinics.PatientRecord;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Enums;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Buffers.Text;
using System.Text.Json;
using static System.Net.Mime.MediaTypeNames;

namespace HFiles_Backend.Infrastructure.Repositories
{
    public class ClinicPatientRecordRepository(AppDbContext context, ILogger<ClinicPatientRecordRepository> logger) : IClinicPatientRecordRepository
    {
        private readonly AppDbContext _context = context;
        private readonly ILogger<ClinicPatientRecordRepository> _logger = logger;

        public async Task SaveAsync(ClinicPatientRecord record)
        {
            await _context.ClinicPatientRecords.AddAsync(record);
            await _context.SaveChangesAsync();
        }

        public async Task<List<ClinicPatientRecord>> GetByClinicAndPatientAsync(int clinicId, int patientId)
        {
            return await _context.ClinicPatientRecords
                .Where(r => r.ClinicId == clinicId && r.PatientId == patientId)
                .ToListAsync();
        }

        public async Task<List<ClinicPatientRecord>> GetByClinicPatientVisitAsync(int clinicId, int patientId, int clinicVisitId)
        {
            return await _context.ClinicPatientRecords
                .Where(r => r.ClinicId == clinicId && r.PatientId == patientId && r.ClinicVisitId == clinicVisitId)
                .ToListAsync();
        }

        public async Task<ClinicPatientRecord?> GetReportImageRecordAsync(int clinicId, int patientId, int visitId)
        {
            return await _context.ClinicPatientRecords
                .FirstOrDefaultAsync(r =>
                    r.ClinicId == clinicId &&
                    r.PatientId == patientId &&
                    r.ClinicVisitId == visitId &&
                    r.Type == RecordType.Images);
        }

        public async Task UpdateAsync(ClinicPatientRecord record)
        {
            _context.ClinicPatientRecords.Update(record);
            await _context.SaveChangesAsync();
        }

        public async Task<PatientHistoryResponse?> GetPatientHistoryAsync(int clinicId, int patientId)
        {
            var visitGroups = await _context.ClinicVisits
                .Where(v => v.ClinicId == clinicId && v.ClinicPatientId == patientId)
                .Include(v => v.ConsentFormsSent)
                .Include(v => v.Patient)
                .ToListAsync();

            if (!visitGroups.Any())
                return null;

            var firstVisit = visitGroups.First();
            var hfId = firstVisit.Patient.HFID;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.HfId == hfId);

            var response = new PatientHistoryResponse
            {
                PatientName = firstVisit.Patient.PatientName?.Trim() ?? string.Empty,
                HfId = hfId ?? string.Empty,
                Email = user?.Email ?? string.Empty,
                Visits = new List<VisitRecordGroup>()
            };

            foreach (var visit in visitGroups)
            {
                var records = await _context.ClinicPatientRecords
                    .Where(r => r.ClinicVisitId == visit.Id)
                    .ToListAsync();

                var recordItems = new List<PatientRecordItem>();

                foreach (var r in records)
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(r.JsonData);
                        var root = doc.RootElement;

                        if (root.ValueKind == JsonValueKind.Object &&
                            root.TryGetProperty("url", out var urlElement) &&
                            urlElement.ValueKind == JsonValueKind.String)
                        {
                            recordItems.Add(new PatientRecordItem
                            {
                                Type = r.Type,
                                Url = urlElement.GetString() ?? string.Empty,
                                SendToPatient = r.SendToPatient
                            });
                        }
                        else if (root.ValueKind == JsonValueKind.Array && r.Type == RecordType.Images)
                        {
                            foreach (var element in root.EnumerateArray())
                            {
                                if (element.ValueKind == JsonValueKind.String)
                                {
                                    recordItems.Add(new PatientRecordItem
                                    {
                                        Type = r.Type,
                                        Url = element.GetString() ?? string.Empty,
                                        SendToPatient = r.SendToPatient
                                    });
                                }
                            }
                        }
                        else
                        {
                            // fallback: record exists but no valid URL
                            recordItems.Add(new PatientRecordItem
                            {
                                Type = r.Type,
                                Url = string.Empty,
                                SendToPatient = r.SendToPatient
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Malformed JSON in ClinicPatientRecord ID {RecordId}", r.Id);
                        recordItems.Add(new PatientRecordItem
                        {
                            Type = r.Type,
                            Url = string.Empty,
                            SendToPatient = r.SendToPatient
                        });
                    }
                }


                var grouped = new VisitRecordGroup
                {
                    AppointmentDate = visit.AppointmentDate,
                    IsVerified = visit.ConsentFormsSent.Any(f => f.IsVerified),
                    ConsentForms = visit.ConsentFormsSent
                        .Select(f => f.ConsentFormUrl ?? string.Empty)
                        .ToList(),
                    Records = recordItems
                };

                response.Visits.Add(grouped);
            }

            return response;
        }

        public async Task<ClinicPatientRecord?> GetByCompositeKeyAsync(int clinicId, int patientId, int visitId, RecordType type)
        {
            return await _context.ClinicPatientRecords
                .FirstOrDefaultAsync(r =>
                    r.ClinicId == clinicId &&
                    r.PatientId == patientId &&
                    r.ClinicVisitId == visitId &&
                    r.Type == type);
        }

        public async Task<List<ClinicPatientRecord>> GetTreatmentRecordsAsync(int clinicId, int patientId, int visitId)
        {
            return await _context.ClinicPatientRecords
                .Where(r =>           
                    r.ClinicId == clinicId &&
                    r.PatientId == patientId &&
                    r.ClinicVisitId == visitId &&
                    r.Type == RecordType.Treatment)
                .ToListAsync();
        }

        public async Task<ClinicPatient?> GetByIdAsync(int patientId)
        {
            return await _context.ClinicPatients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == patientId);
        }

        public async Task<List<ClinicPatientRecord>> GetTreatmentRecordsByClinicIdAsync(int clinicId)
        {
            return await _context.ClinicPatientRecords
                .Where(r => r.ClinicId == clinicId && r.Type == RecordType.Treatment)
                .ToListAsync();
        }

        public async Task<List<ClinicPatientRecord>> GetPrescriptionRecordsByClinicIdAsync(int clinicId)
        {
            return await _context.ClinicPatientRecords
                .Where(r => r.ClinicId == clinicId && r.Type == RecordType.Prescription)
                .ToListAsync();
        }


        //public async Task<PatientHistoryResponse?> GetPatientHistoryWithFiltersAsync(
        //     int clinicId,
        //     int patientId,
        //     DateTime? startDate,
        //     DateTime? endDate,
        //     List<string> categoryFilters)
        //{
        //    IQueryable<ClinicVisit> visitQuery = _context.ClinicVisits
        //        .Where(v => v.ClinicId == clinicId && v.ClinicPatientId == patientId);

        //    // Apply date filtering
        //    if (startDate.HasValue)
        //        visitQuery = visitQuery.Where(v => v.AppointmentDate.Date >= startDate.Value.Date);

        //    if (endDate.HasValue)
        //        visitQuery = visitQuery.Where(v => v.AppointmentDate.Date <= endDate.Value.Date);

        //    // Apply includes after filtering
        //    var visitGroups = await visitQuery
        //        .Include(v => v.ConsentFormsSent)
        //            .ThenInclude(cf => cf.ConsentForm)
        //        .Include(v => v.Patient)
        //        .ToListAsync();

        //    if (!visitGroups.Any())
        //        return null;

        //    var firstVisit = visitGroups.First();
        //    var hfId = firstVisit.Patient.HFID;
        //    var user = await _context.Users.FirstOrDefaultAsync(u => u.HfId == hfId);

        //    var response = new PatientHistoryResponse
        //    {
        //        PatientName = firstVisit.Patient.PatientName?.Trim() ?? string.Empty,
        //        HfId = hfId ?? string.Empty,
        //        Email = user?.Email ?? string.Empty,
        //        Visits = new List<VisitRecordGroup>()
        //    };

        //    foreach (var visit in visitGroups)
        //    {
        //        var records = await _context.ClinicPatientRecords
        //            .Where(r => r.ClinicVisitId == visit.Id)
        //            .ToListAsync();

        //        var recordItems = new List<PatientRecordItem>();
        //        var consentForms = new List<ConsentFormInfo>();

        //        // Process consent forms
        //        foreach (var consentFormSent in visit.ConsentFormsSent)
        //        {
        //            var consentFormName = consentFormSent.ConsentForm.Title.ToLowerInvariant();

        //            // Check if consent form matches category filter
        //            if (categoryFilters.Any() && !categoryFilters.Contains(consentFormName))
        //                continue;

        //            // Only include consent forms with actual URLs (not empty or null)
        //            if (!string.IsNullOrWhiteSpace(consentFormSent.ConsentFormUrl))
        //            {
        //                consentForms.Add(new ConsentFormInfo
        //                {
        //                    Name = consentFormSent.ConsentForm.Title,
        //                    Url = consentFormSent.ConsentFormUrl,
        //                    IsVerified = consentFormSent.IsVerified,
        //                    Category = "consent_form"
        //                });
        //            }
        //        }

        //        // Process patient records (prescription, treatment, invoice, receipt and symptom diary)
        //        foreach (var r in records)
        //        {
        //            var recordCategory = GetRecordCategory(r.Type);

        //            // Check if record matches category filter
        //            if (categoryFilters.Any() && !categoryFilters.Contains(recordCategory))
        //                continue;

        //            try
        //            {
        //                using var doc = JsonDocument.Parse(r.JsonData);
        //                var root = doc.RootElement;

        //                if (root.ValueKind == JsonValueKind.Object &&
        //                    root.TryGetProperty("url", out var urlElement) &&
        //                    urlElement.ValueKind == JsonValueKind.String)
        //                {
        //                    var urlValue = urlElement.GetString();
        //                    // Only include records with actual URLs (not empty or null)
        //                    if (!string.IsNullOrWhiteSpace(urlValue))
        //                    {
        //                        recordItems.Add(new PatientRecordItem
        //                        {
        //                            Type = r.Type,
        //                            Url = urlValue,
        //                            SendToPatient = r.SendToPatient,
        //                            Category = recordCategory
        //                        });
        //                    }
        //                }
        //                else if (root.ValueKind == JsonValueKind.Array && r.Type == RecordType.Images)
        //                {
        //                    foreach (var element in root.EnumerateArray())
        //                    {
        //                        if (element.ValueKind == JsonValueKind.String)
        //                        {
        //                            var urlValue = element.GetString();
        //                            // Only include images with actual URLs (not empty or null)
        //                            if (!string.IsNullOrWhiteSpace(urlValue))
        //                            {
        //                                recordItems.Add(new PatientRecordItem
        //                                {
        //                                    Type = r.Type,
        //                                    Url = urlValue,
        //                                    SendToPatient = r.SendToPatient,
        //                                    Category = recordCategory
        //                                });
        //                            }
        //                        }
        //                    }
        //                }
        //                else
        //                {
        //                    // Don't include fallback records with empty URLs
        //                    // This eliminates records that exist but have no valid URL
        //                }
        //            }
        //            catch (Exception ex)
        //            {
        //                _logger.LogWarning(ex, "Malformed JSON in ClinicPatientRecord ID {RecordId}", r.Id);
        //                // Don't include records with malformed JSON and no valid URL
        //            }
        //        }

        //        // Only include visit if it has matching records or consent forms
        //        if (recordItems.Any() || consentForms.Any() || !categoryFilters.Any())
        //        {
        //            var grouped = new VisitRecordGroup
        //            {
        //                AppointmentDate = visit.AppointmentDate,
        //                IsVerified = visit.ConsentFormsSent.Any(f => f.IsVerified),
        //                ConsentForms = consentForms.Select(cf => cf.Url).ToList(), // Keep for backward compatibility
        //                ConsentFormsWithNames = consentForms.Select(cf => new ConsentFormSimple
        //                {
        //                    Name = cf.Name,
        //                    Url = cf.Url,
        //                    IsVerified = cf.IsVerified
        //                }).ToList(),
        //                ConsentFormsDetails = consentForms,
        //                Records = recordItems
        //            };

        //            response.Visits.Add(grouped);
        //        }
        //    }

        //    return response;
        //}

        public async Task<PatientHistoryResponse?> GetPatientHistoryWithFiltersAsync(
     int clinicId,
     int patientId,
     DateTime? startDate,
     DateTime? endDate,
     List<string> categoryFilters)
        {
            IQueryable<ClinicVisit> visitQuery = _context.ClinicVisits
                .Where(v => v.ClinicId == clinicId && v.ClinicPatientId == patientId);

            // Apply date filtering
            if (startDate.HasValue)
                visitQuery = visitQuery.Where(v => v.AppointmentDate.Date >= startDate.Value.Date);

            if (endDate.HasValue)
                visitQuery = visitQuery.Where(v => v.AppointmentDate.Date <= endDate.Value.Date);

            // Apply includes after filtering
            var visitGroups = await visitQuery
                .Include(v => v.ConsentFormsSent)
                    .ThenInclude(cf => cf.ConsentForm)
                .Include(v => v.Patient)
                .ToListAsync();

            if (!visitGroups.Any())
                return null;

            var firstVisit = visitGroups.First();
            var hfId = firstVisit.Patient.HFID;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.HfId == hfId);

            var response = new PatientHistoryResponse
            {
                PatientName = firstVisit.Patient.PatientName?.Trim() ?? string.Empty,
                HfId = hfId ?? string.Empty,
                Email = user?.Email ?? string.Empty,
                Visits = new List<VisitRecordGroup>()
            };

            // ✅ Check if filters are applied
            bool hasFilters = categoryFilters != null && categoryFilters.Any();

            foreach (var visit in visitGroups)
            {
                var records = await _context.ClinicPatientRecords
                    .Where(r => r.ClinicVisitId == visit.Id)
                    .ToListAsync();

                var recordItems = new List<PatientRecordItem>();
                var consentForms = new List<ConsentFormInfo>();

                // Process consent forms
                foreach (var consentFormSent in visit.ConsentFormsSent)
                {
                    // Only include consent forms with actual URLs (not empty or null)
                    if (string.IsNullOrWhiteSpace(consentFormSent.ConsentFormUrl))
                        continue;

                    // ✅ FIXED: Check if "consent_form" is in the filter
                    if (hasFilters && !categoryFilters.Contains("consent_form"))
                        continue;

                    consentForms.Add(new ConsentFormInfo
                    {
                        Name = consentFormSent.ConsentForm.Title,
                        Url = consentFormSent.ConsentFormUrl,
                        IsVerified = consentFormSent.IsVerified,
                        Category = "consent_form"
                    });
                }

                // Process patient records (prescription, treatment, invoice, receipt and symptom diary)
                foreach (var r in records)
                {
                    var recordCategory = GetRecordCategory(r.Type);

                    // ✅ FIXED: Check if the specific record category is in the filter
                    if (hasFilters && !categoryFilters.Contains(recordCategory))
                        continue;

                    try
                    {
                        using var doc = JsonDocument.Parse(r.JsonData);
                        var root = doc.RootElement;

                        if (root.ValueKind == JsonValueKind.Object &&
                            root.TryGetProperty("url", out var urlElement) &&
                            urlElement.ValueKind == JsonValueKind.String)
                        {
                            var urlValue = urlElement.GetString();
                            // Only include records with actual URLs (not empty or null)
                            if (!string.IsNullOrWhiteSpace(urlValue))
                            {
                                recordItems.Add(new PatientRecordItem
                                {
                                    Type = r.Type,
                                    Url = urlValue,
                                    SendToPatient = r.SendToPatient,
                                    Category = recordCategory
                                });
                            }
                        }
                        else if (root.ValueKind == JsonValueKind.Array && r.Type == RecordType.Images)
                        {
                            foreach (var element in root.EnumerateArray())
                            {
                                if (element.ValueKind == JsonValueKind.String)
                                {
                                    var urlValue = element.GetString();
                                    // Only include images with actual URLs (not empty or null)
                                    if (!string.IsNullOrWhiteSpace(urlValue))
                                    {
                                        recordItems.Add(new PatientRecordItem
                                        {
                                            Type = r.Type,
                                            Url = urlValue,
                                            SendToPatient = r.SendToPatient,
                                            Category = recordCategory
                                        });
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Malformed JSON in ClinicPatientRecord ID {RecordId}", r.Id);
                    }
                }

                // ✅ FIXED: Only include visit if it has matching records OR consent forms (when filters are applied)
                // If no filters, include all visits with any data
                bool shouldIncludeVisit = !hasFilters || (recordItems.Any() || consentForms.Any());

                if (shouldIncludeVisit)
                {
                    var grouped = new VisitRecordGroup
                    {
                        AppointmentDate = visit.AppointmentDate,
                        IsVerified = visit.ConsentFormsSent.Any(f => f.IsVerified),
                        ConsentForms = consentForms.Select(cf => cf.Url).ToList(),
                        ConsentFormsWithNames = consentForms.Select(cf => new ConsentFormSimple
                        {
                            Name = cf.Name,
                            Url = cf.Url,
                            IsVerified = cf.IsVerified
                        }).ToList(),
                        ConsentFormsDetails = consentForms,
                        Records = recordItems
                    };

                    response.Visits.Add(grouped);
                }
            }

            return response;
        }

        private string GetRecordCategory(RecordType recordType)
        {
            return recordType switch
            {
                RecordType.Treatment => "treatment",
                RecordType.Prescription => "prescription",
                RecordType.Invoice => "invoice",
                //RecordType.Invoice => "invoice",
                RecordType.Receipt => "receipt",
                RecordType.Images => "images",
                RecordType.SymptomDiary => "symptom diary",
                _ => "other"
            };
        }

        public async Task<bool> PrescriptionExistsForVisitAsync(int clinicId, int patientId, int visitId)
        {
            return await _context.ClinicPatientRecords
                .AnyAsync(r =>
                    r.ClinicId == clinicId &&
                    r.PatientId == patientId &&
                    r.ClinicVisitId == visitId &&
                    r.Type == RecordType.Prescription);
        }

        public async Task<List<ClinicPatientRecord>> GetUnsentTreatmentRecordsAsync(int clinicId)
        {
            return await _context.ClinicPatientRecords
                .Where(r => r.ClinicId == clinicId &&
                            r.Type == RecordType.Treatment &&
                            !r.SendToPatient)
                .ToListAsync();
        }

        public async Task<List<ClinicPatientRecord>> GetInvoiceRecordsByClinicIdAsync(int clinicId)
        {
            return await _context.ClinicPatientRecords
                .Where(r => r.ClinicId == clinicId && r.Type == RecordType.Invoice)
                .ToListAsync();
        }

        public async Task<List<ClinicPatientRecord>> GetUnsentInvoiceRecordsAsync(int clinicId)
        {
            return await _context.ClinicPatientRecords
                .Where(r => r.ClinicId == clinicId &&
                            r.Type == RecordType.Invoice &&
                            !r.SendToPatient)
                .ToListAsync();
        }

        public async Task<List<ClinicPatientRecord>> GetReceiptRecordsByClinicIdAsync(int clinicId)
        {
            return await _context.ClinicPatientRecords
                .Where(r => r.ClinicId == clinicId && r.Type == RecordType.Receipt)
                .ToListAsync();
        }

        public async Task<List<ClinicPatientRecord>> GetUnsentReceiptRecordsAsync(int clinicId)
        {
            return await _context.ClinicPatientRecords
                .Where(r => r.ClinicId == clinicId &&
                            r.Type == RecordType.Receipt &&
                            !r.SendToPatient)
                .ToListAsync();
        }

        public async Task<bool> DeleteAsync(int uniqueRecordId)
        {
            var record = await _context.ClinicPatientRecords
                .FirstOrDefaultAsync(r => r.Id == uniqueRecordId);

            if (record == null) return false;

            _context.ClinicPatientRecords.Remove(record);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<ClinicPatientRecord?> GetByUniqueRecordIdAsync(int uniqueRecordId)
        {
            var records = await _context.ClinicPatientRecords
                .FirstOrDefaultAsync(r => r.Id == uniqueRecordId);
            return records;
        }
        public async Task<ClinicPatientRecord?> GetByUniqueRecordIdAsync(int clinicId, string uniqueRecordId)
        {
            return await _context.ClinicPatientRecords
                .FirstOrDefaultAsync(r =>
                    r.ClinicId == clinicId &&
                    r.UniqueRecordId == uniqueRecordId);
        }

        public async Task<ClinicPatientRecord?> GetRecordByIdAsync(int recordId)
        {
            return await _context.ClinicPatientRecords
                .FirstOrDefaultAsync(r => r.Id == recordId);
        }

        public async Task<ClinicPatientRecord> GetRecordByUniqueRecordIdAsync(string uniqueRecordId)
        {
            if (string.IsNullOrWhiteSpace(uniqueRecordId))
                return null;

            return await _context.ClinicPatientRecords
                .Where(r => r.UniqueRecordId == uniqueRecordId && r.Type == RecordType.Receipt)
                .OrderByDescending(r => r.Id) // Get the latest one if multiple exist
                .FirstOrDefaultAsync();
        }


        //public async Task<decimal> GetLastReceiptAmountDueByHfIdAsync(string hfId)
        //{
        //    try
        //    {
        //        // Get the patient by HFID first
        //        var clinicPatient = await _context.ClinicPatients
        //            .FirstOrDefaultAsync(p => p.HFID == hfId);

        //        if (clinicPatient == null)
        //        {
        //            _logger.LogInformation("No clinic patient found for HFID: {HfId}", hfId);
        //            return 0;
        //        }

        //        // Get the last receipt record ordered by EpochTime (most recent)
        //        var lastReceiptRecord = await _context.ClinicPatientRecords
        //            .Where(r => r.PatientId == clinicPatient.Id && r.Type == RecordType.Receipt)
        //            .OrderByDescending(r => r.EpochTime)
        //            .FirstOrDefaultAsync();

        //        if (lastReceiptRecord == null)
        //        {
        //            _logger.LogInformation("No receipt found for patient with HFID: {HfId}", hfId);
        //            return 0;
        //        }

        //        // Parse the JSON data to extract amountDue
        //        using var doc = JsonDocument.Parse(lastReceiptRecord.JsonData);
        //        var root = doc.RootElement;

        //        if (root.TryGetProperty("summary", out var summary) &&
        //            summary.TryGetProperty("amountDue", out var amountDue))
        //        {
        //            if (amountDue.ValueKind == JsonValueKind.Number)
        //            {
        //                return amountDue.GetDecimal();
        //            }
        //        }

        //        _logger.LogWarning("Could not extract amountDue from receipt JSON for HFID: {HfId}", hfId);
        //        return 0;
        //    }
        //    catch (JsonException ex)
        //    {
        //        _logger.LogError(ex, "Error parsing receipt JSON for HFID: {HfId}", hfId);
        //        return 0;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error fetching last receipt amount due for HFID: {HfId}", hfId);
        //        return 0;
        //    }
        //}

        public async Task<decimal> GetTotalAmountDueByHfIdAsync(string hfId)
        {
            try
            {
                // 1. Find patient by HFID
                var patient = await _context.ClinicPatients
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.HFID == hfId);

                if (patient == null)
                {
                    _logger.LogWarning("Patient with HFID {HfId} not found", hfId);
                    return 0;
                }

                _logger.LogInformation("Found patient ID {PatientId} for HFID {HfId}", patient.Id, hfId);

                // 2. Get ALL records
                var allRecords = await _context.ClinicPatientRecords
                    .AsNoTracking()
                    .Where(r => r.PatientId == patient.Id
                             && (r.Type == RecordType.Invoice || r.Type == RecordType.Receipt))
                    .OrderByDescending(r => r.EpochTime)
                    .ToListAsync();

                var invoices = allRecords
                    .Where(r => r.Type == RecordType.Invoice
                             && !string.IsNullOrWhiteSpace(r.UniqueRecordId))
                    .ToList();

                var receipts = allRecords
                    .Where(r => r.Type == RecordType.Receipt
                             && !string.IsNullOrWhiteSpace(r.UniqueRecordId))
                    .ToList();

                if (!invoices.Any())
                {
                    _logger.LogInformation("No invoices found for HFID {HfId}", hfId);
                    return 0;
                }

                _logger.LogInformation("Found {InvoiceCount} invoices and {ReceiptCount} receipts",
                    invoices.Count, receipts.Count);

                decimal totalAmountDue = 0;

                // 3. Process each invoice
                foreach (var invoice in invoices)
                {
                    decimal invoiceAmountDue = 0;

                    try
                    {
                        // Check if this invoice has any receipts
                        var receiptsForInvoice = receipts.Where(receipt =>
                        {
                            try
                            {
                                using var doc = JsonDocument.Parse(receipt.JsonData);
                                var root = doc.RootElement;

                                if (root.TryGetProperty("services", out var services) &&
                                    services.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var service in services.EnumerateArray())
                                    {
                                        if (service.TryGetProperty("invoiceNumber", out var invNum) &&
                                            invNum.GetString() == invoice.UniqueRecordId)
                                        {
                                            return true;
                                        }
                                    }
                                }
                                return false;
                            }
                            catch
                            {
                                return false;
                            }
                        }).OrderByDescending(r => r.EpochTime).ToList();

                        if (receiptsForInvoice.Any())
                        {
                            // Invoice HAS receipts - use latest receipt's summary.amountDue
                            var latestReceipt = receiptsForInvoice.First();

                            using var receiptDoc = JsonDocument.Parse(latestReceipt.JsonData);
                            var receiptRoot = receiptDoc.RootElement;

                            if (receiptRoot.TryGetProperty("summary", out var summary) &&
                                summary.TryGetProperty("amountDue", out var amountDueElement))
                            {
                                invoiceAmountDue = amountDueElement.GetDecimal();
                            }

                            _logger.LogInformation(
                                "Invoice {InvoiceId} has receipt {ReceiptId}, amountDue: ₹{AmountDue}",
                                invoice.UniqueRecordId, latestReceipt.UniqueRecordId, invoiceAmountDue);
                        }
                        else
                        {
                            // Invoice has NO receipts - use invoice's amountDue field
                            using var invoiceDoc = JsonDocument.Parse(invoice.JsonData);
                            var invoiceRoot = invoiceDoc.RootElement;

                            // ✅ ONLY use amountDue from invoice (not grandTotal)
                            if (invoiceRoot.TryGetProperty("amountDue", out var amountDueElement))
                            {
                                invoiceAmountDue = amountDueElement.GetDecimal();
                            }

                            _logger.LogInformation(
                                "Invoice {InvoiceId} has NO receipts, using invoice.amountDue: ₹{AmountDue}",
                                invoice.UniqueRecordId, invoiceAmountDue);
                        }

                        totalAmountDue += invoiceAmountDue;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing invoice {InvoiceId}",
                            invoice.UniqueRecordId ?? invoice.Id.ToString());
                    }
                }

                _logger.LogInformation(
                    "Total Amount Due for HFID {HfId}: ₹{TotalAmountDue} from {InvoiceCount} invoices",
                    hfId, totalAmountDue, invoices.Count);

                return totalAmountDue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating total amount due for HFID {HfId}", hfId);
                return 0;
            }
        }


		public async Task<string?> GetCouchnameLatestPackageNameByPatientIdAsync(int patientId)
		{
			try
			{
				// Get the latest membership plan for this patient
				var latestMembershipPlan = await _context.ClinicPatientRecords
					.AsNoTracking()
					.Where(r => r.PatientId == patientId
							 && r.Type == RecordType.MembershipPlan
							 && !string.IsNullOrWhiteSpace(r.UniqueRecordId))
					.OrderByDescending(r => r.EpochTime)
					.FirstOrDefaultAsync();

				if (latestMembershipPlan == null)
					return null;

				// Parse JSON to get coach names from treatments
				using var doc = JsonDocument.Parse(latestMembershipPlan.JsonData);
				var root = doc.RootElement;

				if (root.TryGetProperty("treatments", out var treatments) &&
					treatments.ValueKind == JsonValueKind.Array)
				{
					var coachNames = new List<string>();

					foreach (var treatment in treatments.EnumerateArray())
					{
						if (treatment.TryGetProperty("coach", out var coachElement))
						{
							var coachName = coachElement.GetString();
							if (!string.IsNullOrWhiteSpace(coachName) && !coachNames.Contains(coachName))
								coachNames.Add(coachName);
						}
					}

					if (coachNames.Any())
						return string.Join(", ", coachNames);
				}

				return null;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error fetching latest coach name for PatientId {PatientId}", patientId);
				return null;
			}
		}

		public async Task<string?> GetLatestPackageNameByPatientIdAsync(int patientId)
        {
            try
            {
                // Get the latest membership plan for this patient
                var latestMembershipPlan = await _context.ClinicPatientRecords
                    .AsNoTracking()
                    .Where(r => r.PatientId == patientId
                             && r.Type == RecordType.MembershipPlan
                             && !string.IsNullOrWhiteSpace(r.UniqueRecordId))
                    .OrderByDescending(r => r.EpochTime)
                    .FirstOrDefaultAsync();

                if (latestMembershipPlan == null)
                    return null;

                // Parse JSON to get package/treatment names
                using var doc = JsonDocument.Parse(latestMembershipPlan.JsonData);
                var root = doc.RootElement;

                if (root.TryGetProperty("treatments", out var treatments) &&
                    treatments.ValueKind == JsonValueKind.Array)
                {
                    var treatmentNames = new List<string>();

                    foreach (var treatment in treatments.EnumerateArray())
                    {
                        if (treatment.TryGetProperty("name", out var nameElement))
                        {
                            var name = nameElement.GetString();
                            if (!string.IsNullOrWhiteSpace(name))
                                treatmentNames.Add(name);
                        }
                    }

                    if (treatmentNames.Any())
                        return string.Join(", ", treatmentNames);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching latest package name for PatientId {PatientId}", patientId);
                return null;
            }
        }

		

		//receipt upload doc 
		public async Task<ClinicPatientRecord?> GetReceiptDocumentByReceiptNumberAsync(int clinicId, string receiptNumber)
        {
            return await _context.ClinicPatientRecords
                .FirstOrDefaultAsync(r =>
                    r.ClinicId == clinicId &&
                    r.UniqueRecordId == receiptNumber &&
                    r.Type == RecordType.ReceiptDocuments);
        }

        public async Task<List<ClinicPatientRecord>> GetReceiptDocumentsByVisitAsync(int clinicId, int patientId, int visitId)
        {
            return await _context.ClinicPatientRecords
                .Where(r =>
                    r.ClinicId == clinicId &&
                    r.PatientId == patientId &&
                    r.ClinicVisitId == visitId &&
                    r.Type == RecordType.ReceiptDocuments)
                .OrderByDescending(r => r.EpochTime)
                .ToListAsync();
        }

        

        public async Task<bool> DeleteDocumentsAsync(int recordId)
        {
            var record = await _context.ClinicPatientRecords
                .FirstOrDefaultAsync(r => r.Id == recordId);

            if (record == null)
                return false;

            _context.ClinicPatientRecords.Remove(record);
            await _context.SaveChangesAsync();
            return true;
        }




		public async Task<string?> GetLatestCoachNameByPatientIdAsync(int patientId)
		{
			try
			{
				// Get the latest membership plan for this patient
				var latestMembershipPlan = await _context.ClinicPatientRecords
					.AsNoTracking()
					.Where(r => r.PatientId == patientId
							 && r.Type == RecordType.MembershipPlan
							 && !string.IsNullOrWhiteSpace(r.UniqueRecordId))
					.OrderByDescending(r => r.EpochTime)
					.FirstOrDefaultAsync();

				if (latestMembershipPlan == null)
					return null;

				// Parse JSON to get coach name
				using var doc = JsonDocument.Parse(latestMembershipPlan.JsonData);
				var root = doc.RootElement;

				// Try to get coach name from the JSON
				if (root.TryGetProperty("coachName", out var coachNameElement))
				{
					return coachNameElement.GetString();
				}

				// Alternative: If coach is nested in a different structure
				if (root.TryGetProperty("coach", out var coachElement))
				{
					if (coachElement.TryGetProperty("name", out var nameElement))
					{
						return nameElement.GetString();
					}
				}

				return null;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error fetching latest coach name for PatientId {PatientId}", patientId);
				return null;
			}
		}

		// Optional: Get all unique coach names for dropdown filters
		public async Task<List<string>> GetAllCoachNamesAsync(int clinicId)
		{
			try
			{
				var membershipPlans = await _context.ClinicPatientRecords
					.AsNoTracking()
					.Where(r => r.ClinicId == clinicId
							 && r.Type == RecordType.MembershipPlan
							 && !string.IsNullOrWhiteSpace(r.JsonData))
					.ToListAsync();

				var coachNames = new HashSet<string>();

				foreach (var plan in membershipPlans)
				{
					try
					{
						using var doc = JsonDocument.Parse(plan.JsonData);
						var root = doc.RootElement;

						if (root.TryGetProperty("coachName", out var coachNameElement))
						{
							var coachName = coachNameElement.GetString();
							if (!string.IsNullOrWhiteSpace(coachName))
								coachNames.Add(coachName);
						}
						else if (root.TryGetProperty("coach", out var coachElement))
						{
							if (coachElement.TryGetProperty("name", out var nameElement))
							{
								var coachName = nameElement.GetString();
								if (!string.IsNullOrWhiteSpace(coachName))
									coachNames.Add(coachName);
							}
						}
					}
					catch (Exception ex)
					{
						_logger.LogWarning(ex, "Failed to parse membership plan JSON for record ID {RecordId}", plan.Id);
					}
				}

				return coachNames.OrderBy(c => c).ToList();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error fetching all coach names for ClinicId {ClinicId}", clinicId);
				return new List<string>();
			}
		}

		// Optional: Get all unique package names for dropdown filters
		public async Task<List<string>> GetAllPackageNamesAsync(int clinicId)
		{
			try
			{
				var membershipPlans = await _context.ClinicPatientRecords
					.AsNoTracking()
					.Where(r => r.ClinicId == clinicId
							 && r.Type == RecordType.MembershipPlan
							 && !string.IsNullOrWhiteSpace(r.JsonData))
					.ToListAsync();

				var packageNames = new HashSet<string>();

				foreach (var plan in membershipPlans)
				{
					try
					{
						using var doc = JsonDocument.Parse(plan.JsonData);
						var root = doc.RootElement;

						if (root.TryGetProperty("treatments", out var treatments) &&
							treatments.ValueKind == JsonValueKind.Array)
						{
							foreach (var treatment in treatments.EnumerateArray())
							{
								if (treatment.TryGetProperty("name", out var nameElement))
								{
									var name = nameElement.GetString();
									if (!string.IsNullOrWhiteSpace(name))
										packageNames.Add(name);
								}
							}
						}
					}
					catch (Exception ex)
					{
						_logger.LogWarning(ex, "Failed to parse membership plan JSON for record ID {RecordId}", plan.Id);
					}
				}

				return packageNames.OrderBy(p => p).ToList();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error fetching all package names for ClinicId {ClinicId}", clinicId);
				return new List<string>();
			}
		}


		public async Task<ClinicPatientRecord?> GetByPatientAndVisitAsync(
	int patientId,
	int clinicVisitId)
		{
			return await _context.ClinicPatientRecords
				.FirstOrDefaultAsync(x =>
					x.PatientId == patientId &&
					x.ClinicVisitId == clinicVisitId &&
					!x.Is_Cansel
				);
		}

		public async Task<List<ClinicPatientRecord>> GetAllByPatientAndVisitAsync(
	int patientId,
	int clinicVisitId)
		{
			return await _context.ClinicPatientRecords
				.Where(r =>
					r.PatientId == patientId &&
					r.ClinicVisitId == clinicVisitId &&
					!r.Is_Cansel)
				.ToListAsync();
		}

		public async Task UpdateRangeAsync(List<ClinicPatientRecord> records)
		{
			_context.ClinicPatientRecords.UpdateRange(records);
			await _context.SaveChangesAsync();
		}



	

        public async Task<string?> GetLatestPaymentModeFromReceiptAsync(int patientId)
        {
            var receiptJson = await _context.ClinicPatientRecords
                .AsNoTracking()
                .Where(r =>
                    r.PatientId == patientId &&
                    r.Type == RecordType.Receipt)
                .OrderByDescending(r => r.EpochTime)
                .Select(r => r.JsonData)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(receiptJson))
                return null;

            try
            {
                var json = JObject.Parse(receiptJson);
                return json["services"]?
                    .FirstOrDefault()?["ModeOfPayment"]?
                    .ToString();
            }
            catch
            {
                return null;
            }
        }
    }
}
