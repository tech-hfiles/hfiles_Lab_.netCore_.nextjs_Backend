using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HFiles_Backend.Infrastructure.Repositories
{
    public class ClinicPatientMedicalHistoryRepository(
        AppDbContext context,
        ILogger<ClinicPatientMedicalHistoryRepository> logger) : IClinicPatientMedicalHistoryRepository
    {
        private readonly AppDbContext _context = context ?? throw new ArgumentNullException(nameof(context));
        private readonly ILogger<ClinicPatientMedicalHistoryRepository> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        public async Task<ClinicPatientMedicalHistory?> GetByClinicPatientIdAsync(int patientId, int clinicId)
        {
            try
            {
                return await (from history in _context.ClinicPatientMedicalHistories
                              join clinicPatient in _context.ClinicPatients
                                  on history.ClinicPatientId equals clinicPatient.Id
                              join user in _context.Users
                                  on clinicPatient.HFID equals user.HfId
                              where user.Id == patientId
                                  && history.ClinicId == clinicId
                                  && history.DeletedBy == 0
                                  && user.DeletedBy == 0
                              select history)
                    .Include(h => h.ClinicPatient)
                    .Include(h => h.Clinic)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error retrieving medical history for Patient ID: {PatientId}, Clinic ID: {ClinicId}",
                    patientId, clinicId);
                throw;
            }
        }

        public async Task<ClinicPatientMedicalHistory?> GetByIdAsync(int id)
        {
            try
            {
                return await _context.ClinicPatientMedicalHistories
                    .Include(h => h.ClinicPatient)
                    .Include(h => h.Clinic)
                    .FirstOrDefaultAsync(h => h.Id == id && h.DeletedBy == 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving medical history by ID: {Id}", id);
                throw;
            }
        }

        public async Task<ClinicPatientMedicalHistory> CreateAsync(ClinicPatientMedicalHistory history)
        {
            try
            {
                if (history == null)
                    throw new ArgumentNullException(nameof(history));

                await _context.ClinicPatientMedicalHistories.AddAsync(history);
                _logger.LogInformation("Medical history created for ClinicPatient ID: {ClinicPatientId}", history.ClinicPatientId);
                return history;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating medical history for ClinicPatient ID: {ClinicPatientId}",
                    history?.ClinicPatientId);
                throw;
            }
        }

        public Task<ClinicPatientMedicalHistory> UpdateAsync(ClinicPatientMedicalHistory history)
        {
            try
            {
                if (history == null)
                    throw new ArgumentNullException(nameof(history));

                // Mark entity as modified (Entity Framework will track changes)
                _context.Entry(history).State = EntityState.Modified;

                _logger.LogInformation("Medical history updated for ID: {Id}", history.Id);
                return Task.FromResult(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating medical history ID: {Id}", history?.Id);
                throw;
            }
        }

        public async Task<bool> ExistsAsync(int patientId, int clinicId)
        {
            try
            {
                // Using join-based approach for optimal performance (single query)
                return await (from history in _context.ClinicPatientMedicalHistories
                              join clinicPatient in _context.ClinicPatients
                                  on history.ClinicPatientId equals clinicPatient.Id
                              join user in _context.Users
                                  on clinicPatient.HFID equals user.HfId
                              where user.Id == patientId
                                  && history.ClinicId == clinicId
                                  && history.DeletedBy == 0
                                  && user.DeletedBy == 0
                              select history)
                    .AnyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error checking existence of medical history for Patient ID: {PatientId}, Clinic ID: {ClinicId}",
                    patientId, clinicId);
                throw;
            }
        }

        public async Task SaveChangesAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving changes to database");
                throw;
            }
        }

        public async Task<ClinicPatientMedicalHistory?> GetByClinicPatientIdDirectAsync(int clinicPatientId, int clinicId)
        {
            return await _context.ClinicPatientMedicalHistories
                .Include(h => h.ClinicPatient)
                .Include(h => h.Clinic)
                .FirstOrDefaultAsync(h =>
                    h.ClinicPatientId == clinicPatientId
                    && h.ClinicId == clinicId
                    && h.DeletedBy == 0);
        }
    }
}
