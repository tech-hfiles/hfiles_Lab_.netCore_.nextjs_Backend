using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HFiles_Backend.Infrastructure.Repositories
{
    public class ClinicPrescriptionRepository(AppDbContext context, ILogger<ClinicPrescriptionRepository> logger) : IClinicPrescriptionRepository
    {
        private readonly AppDbContext _context = context;
        private readonly ILogger<ClinicPrescriptionRepository> _logger = logger;

        public async Task SavePrescriptionAsync(ClinicPrescription prescription)
        {
            _context.ClinicPrescriptions.Add(prescription);
            await _context.SaveChangesAsync();
        }

        public async Task<List<ClinicPrescription>> GetPrescriptionsByClinicIdAsync(int clinicId)
            => await _context.ClinicPrescriptions
                .Where(p => p.ClinicId == clinicId)
                .ToListAsync();

        public async Task<ClinicPrescription?> GetByIdAsync(int prescriptionId)
        {
            return await _context.ClinicPrescriptions
                .Include(p => p.Clinic)
                .FirstOrDefaultAsync(p => p.Id == prescriptionId);
        }

        public async Task UpdatePrescriptionAsync(ClinicPrescription prescription)
        {
            _context.ClinicPrescriptions.Update(prescription);
            await _context.SaveChangesAsync();
        }

        public async Task DeletePrescriptionAsync(int prescriptionId)
        {
            var prescription = await _context.ClinicPrescriptions
                .FirstOrDefaultAsync(p => p.Id == prescriptionId);

            if (prescription != null)
            {
                _context.ClinicPrescriptions.Remove(prescription);
                await _context.SaveChangesAsync();
            }
        }


        public async Task<ClinicPrescriptionNotes> SavePrescriptionNoteAsync(ClinicPrescriptionNotes note)
        {
            try
            {
                await _context.clinicPrescriptionNotes.AddAsync(note);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Prescription note saved: {NoteId} for Clinic: {ClinicId}",
                    note.Id, note.ClinicId);

                return note;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving prescription note for clinic: {ClinicId}", note.ClinicId);
                throw;
            }
        }

        public async Task<List<ClinicPrescriptionNotes>> GetPrescriptionNotesByClinicIdAsync(int clinicId)
        {
            try
            {
                return await _context.clinicPrescriptionNotes
                    .Where(n => n.ClinicId == clinicId)
                    .OrderByDescending(n => n.Id)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving prescription notes for clinic: {ClinicId}", clinicId);
                throw;
            }
        }

        public async Task<ClinicPrescriptionNotes?> GetByNotesIdAsync(int noteId)
        {
            try
            {
                return await _context.clinicPrescriptionNotes
                    .Include(n => n.Clinic)
                    .FirstOrDefaultAsync(n => n.Id == noteId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving prescription note by ID: {NoteId}", noteId);
                throw;
            }
        }

        public async Task UpdatePrescriptionNoteAsync(ClinicPrescriptionNotes note)
        {
            try
            {
                _context.clinicPrescriptionNotes.Update(note);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Prescription note updated: {NoteId}", note.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating prescription note: {NoteId}", note.Id);
                throw;
            }
        }

        public async Task DeletePrescriptionNoteAsync(int noteId)
        {
            try
            {
                var note = await _context.clinicPrescriptionNotes.FindAsync(noteId);
                if (note != null)
                {
                    _context.clinicPrescriptionNotes.Remove(note);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Prescription note deleted: {NoteId}", noteId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting prescription note: {NoteId}", noteId);
                throw;
            }
        }

        public async Task<ClinicPrescriptionNotes?> GetPrescriptionNoteByIdAsync(int noteId)
        {
            return await _context.clinicPrescriptionNotes
            .Include(n => n.Clinic)
            .FirstOrDefaultAsync(n => n.Id == noteId);

        }
    }
}
