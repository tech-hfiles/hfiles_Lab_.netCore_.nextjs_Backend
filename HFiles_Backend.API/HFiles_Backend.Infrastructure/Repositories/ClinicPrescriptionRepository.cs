using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HFiles_Backend.Infrastructure.Repositories
{
    public class ClinicPrescriptionRepository(AppDbContext context) : IClinicPrescriptionRepository
    {
        private readonly AppDbContext _context = context;

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
    }
}
