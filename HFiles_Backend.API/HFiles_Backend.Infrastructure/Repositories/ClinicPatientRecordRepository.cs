using HFiles_Backend.Application.DTOs.Clinics.PatientRecord;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Enums;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

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
                    catch (JsonException ex)
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
                    var consentFormName = consentFormSent.ConsentForm.Title.ToLowerInvariant();

                    // Check if consent form matches category filter
                    if (categoryFilters.Any() && !categoryFilters.Contains(consentFormName))
                        continue;

                    consentForms.Add(new ConsentFormInfo
                    {
                        Name = consentFormSent.ConsentForm.Title,
                        Url = consentFormSent.ConsentFormUrl ?? string.Empty,
                        IsVerified = consentFormSent.IsVerified,
                        Category = "consent_form"
                    });
                }

                // Process patient records (prescription, treatment, invoice, receipt)
                foreach (var r in records)
                {
                    var recordCategory = GetRecordCategory(r.Type);

                    // Check if record matches category filter
                    if (categoryFilters.Any() && !categoryFilters.Contains(recordCategory))
                        continue;

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
                                SendToPatient = r.SendToPatient,
                                Category = recordCategory
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
                                        SendToPatient = r.SendToPatient,
                                        Category = recordCategory
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
                                SendToPatient = r.SendToPatient,
                                Category = recordCategory
                            });
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Malformed JSON in ClinicPatientRecord ID {RecordId}", r.Id);
                        recordItems.Add(new PatientRecordItem
                        {
                            Type = r.Type,
                            Url = string.Empty,
                            SendToPatient = r.SendToPatient,
                            Category = recordCategory
                        });
                    }
                }

                // Only include visit if it has matching records or consent forms
                if (recordItems.Any() || consentForms.Any() || !categoryFilters.Any())
                {
                    var grouped = new VisitRecordGroup
                    {
                        AppointmentDate = visit.AppointmentDate,
                        IsVerified = visit.ConsentFormsSent.Any(f => f.IsVerified),
                        ConsentForms = consentForms.Select(cf => cf.Url).ToList(),
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
                RecordType.Receipt => "receipt",
                RecordType.Images => "images",
                _ => "other"
            };
        }
    }
}
