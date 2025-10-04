using HFiles_Backend.API.Interfaces;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Enums;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HFiles_Backend.API.Services
{
    public class UniqueIdGeneratorService(
        AppDbContext context,
        ILogger<UniqueIdGeneratorService> logger) : IUniqueIdGeneratorService
    {
        private readonly AppDbContext _context = context;
        private readonly ILogger<UniqueIdGeneratorService> _logger = logger;

        // Prefixes for each record type
        private static readonly Dictionary<RecordType, string> RecordPrefixes = new()
    {
        { RecordType.Prescription, "PRC" },
        { RecordType.Treatment, "TRT" },
        { RecordType.Invoice, "INV" },
        { RecordType.Receipt, "REC" }
    };           

        public async Task<string> GenerateUniqueIdAsync(int clinicId, RecordType recordType)
        {
            // Use transaction with serializable isolation to prevent race conditions
            //using var transaction = await _context.Database.BeginTransactionAsync(
            //    System.Data.IsolationLevel.Serializable);

            try
            {
                // Get or create counter for this clinic and record type
                var counter = await _context.ClinicRecordCounters
                    .FirstOrDefaultAsync(c =>
                        c.ClinicId == clinicId &&
                        c.RecordType == recordType);

                if (counter == null)
                {
                    counter = new ClinicRecordCounter
                    {
                        ClinicId = clinicId,
                        RecordType = recordType,
                        LastNumber = 0
                    };
                    _context.ClinicRecordCounters.Add(counter);
                }

                // Increment counter
                counter.LastNumber++;

                await _context.SaveChangesAsync();
                //await transaction.CommitAsync();

                // Generate formatted ID
                var prefix = RecordPrefixes[recordType];
                var uniqueId = $"{prefix}{counter.LastNumber:D4}";

                _logger.LogInformation(
                    "Generated unique ID {UniqueId} for Clinic {ClinicId}, RecordType {RecordType}",
                    uniqueId, clinicId, recordType);

                return uniqueId;
            }
            catch (Exception ex)
            {
                //await transaction.RollbackAsync();
                _logger.LogError(ex,
                    "Error generating unique ID for Clinic {ClinicId}, RecordType {RecordType}",
                    clinicId, recordType);
                throw;
            }
        }


        // NEW: Preview next available ID without incrementing counter
        public async Task<string> GetNextAvailableIdAsync(int clinicId, RecordType recordType)
        {
            try
            {
                var counter = await _context.ClinicRecordCounters
                    .AsNoTracking() // Read-only query
                    .FirstOrDefaultAsync(c =>
                        c.ClinicId == clinicId &&
                        c.RecordType == recordType);

                int nextNumber = counter?.LastNumber + 1 ?? 1;
                var prefix = RecordPrefixes[recordType];
                var nextId = $"{prefix}{nextNumber:D4}";

                return nextId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error getting next available ID for Clinic {ClinicId}, RecordType {RecordType}",
                    clinicId, recordType);
                throw;
            }
        }
    }
}
