using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HFiles_Backend.Infrastructure.Repositories
{
    public class ClinicTreatmentRepository(AppDbContext context) : IClinicTreatmentRepository
    {
        private readonly AppDbContext _context = context;

        public async Task<ClinicTreatment?> GetByIdAsync(int treatmentId)
            => await _context.ClinicTreatments.FirstOrDefaultAsync(t => t.Id == treatmentId);

        public async Task<List<ClinicTreatment>> GetByClinicIdAsync(int clinicId)
            => await _context.ClinicTreatments.Where(t => t.ClinicId == clinicId).ToListAsync();

        public async Task SaveAsync(ClinicTreatment treatment)
        {
            await _context.ClinicTreatments.AddAsync(treatment);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(ClinicTreatment treatment)
        {
            _context.ClinicTreatments.Update(treatment);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int treatmentId)
        {
            var treatment = await _context.ClinicTreatments
           .FirstOrDefaultAsync(t => t.Id == treatmentId);

            if (treatment != null)
            {
                _context.ClinicTreatments.Remove(treatment);
                await _context.SaveChangesAsync();

            }
        }
    }

}
