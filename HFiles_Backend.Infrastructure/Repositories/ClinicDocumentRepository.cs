using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;  // Correct namespace
using HFiles_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HFiles_Backend.Infrastructure.Repositories
{
    public class ClinicDocumentRepository : IClinicDocumentRepository
    {
        private readonly AppDbContext _context;  // Changed to AppDbContext based on your Program.cs

        public ClinicDocumentRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ClinicSignup?> GetClinicByIdAsync(int clinicId)
        {
            return await _context.Set<ClinicSignup>()
                .FirstOrDefaultAsync(c => c.Id == clinicId);
        }

        public async Task<ClinicPatient?> GetPatientByIdAsync(int patientId)
        {
            return await _context.Set<ClinicPatient>()
                .FirstOrDefaultAsync(p => p.Id == patientId);
        }

        public async Task<bool> IsPatientBelongsToClinicAsync(int patientId, int clinicId)
        {
            return await _context.Set<ClinicDocument_Storage>()
                .AnyAsync(p => p.Id == patientId && p.ClinicId == clinicId);
        }

        public async Task<ClinicDocument_Storage> SaveDocumentAsync(ClinicDocument_Storage document)
        {
            await _context.Set<ClinicDocument_Storage>().AddAsync(document);
            await _context.SaveChangesAsync();
            return document;
        }

        public async Task<List<ClinicDocument_Storage>> GetPatientDocumentsAsync(int patientId, int clinicId)
        {
            return await _context.Set<ClinicDocument_Storage>()
                .Where(d => d.PatientId == patientId && d.ClinicId == clinicId && d.IsDeleted != true)
                .OrderByDescending(d => d.EpochTime)
                .ToListAsync();
        }

        public async Task<bool> SoftDeleteDocumentAsync(int documentId, int clinicId, int patientId)
        {
            var document = await _context.Set<ClinicDocument_Storage>()
                .FirstOrDefaultAsync(d => d.Id == documentId && d.ClinicId == clinicId && d.PatientId == patientId);

            if (document == null)
                return false;

            document.IsDeleted = true;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IDbContextTransaction> BeginTransactionAsync()
        {
            return await _context.Database.BeginTransactionAsync();
        }
        public async Task<ClinicDocument_Storage?> GetDocumentByIdAsync(int documentId, int clinicId, int patientId)
        {
            return await _context.ClinicDocument_Storage
                .FirstOrDefaultAsync(d =>
                    d.Id == documentId &&
                    d.ClinicId == clinicId &&
                    d.PatientId == patientId &&
                    d.IsDeleted != true);  // Changed from !d.IsDeleted to d.IsDeleted != true
        }

        public async Task<bool> UpdateDocumentFileNameAsync(int documentId, int clinicId, int patientId, string newFileName)
        {
            var document = await GetDocumentByIdAsync(documentId, clinicId, patientId);

            if (document == null)
            {
                return false;
            }

            document.FileName = newFileName;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}