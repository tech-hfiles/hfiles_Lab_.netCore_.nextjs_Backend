using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HFiles_Backend.Infrastructure.Repositories
{
    public class Hifi5PricingPackageRepository : IHifi5PricingPackageRepository
    {
        private readonly AppDbContext _context;

        public Hifi5PricingPackageRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Hifi5PricingPackage?> GetByIdAsync(int id)
        {
            return await _context.hifi5PricingPackages
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<IEnumerable<Hifi5PricingPackage>> GetAllAsync()
        {
            return await _context.hifi5PricingPackages
                .OrderBy(p => p.ProgramCategory)
                .ThenBy(p => p.ProgramName)
                .ToListAsync();
        }

        public async Task<IEnumerable<Hifi5PricingPackage>> GetByClinicIdAsync(int clinicId)
        {
            return await _context.hifi5PricingPackages
                .Where(p => p.ClinicId == clinicId)
                .OrderBy(p => p.ProgramCategory)
                .ThenBy(p => p.ProgramName)
                .ToListAsync();
        }

        public async Task<IEnumerable<Hifi5PricingPackage>> GetByProgramCategoryAsync(string programCategory)
        {
            return await _context.hifi5PricingPackages
                .Where(p => p.ProgramCategory == programCategory)
                .OrderBy(p => p.ProgramName)
                .ToListAsync();
        }

        public async Task<IEnumerable<Hifi5PricingPackage>> GetByProgramNameAsync(string programName)
        {
            return await _context.hifi5PricingPackages
                .Where(p => p.ProgramName.Contains(programName))
                .OrderBy(p => p.ProgramCategory)
                .ToListAsync();
        }
        public async Task<List<string>> GetProgramNamesAsync()
        {
            return await _context.hifi5PricingPackages
                .Select(p => p.ProgramName)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
        }




        public async Task<Hifi5PricingPackage> AddAsync(Hifi5PricingPackage package)
        {
            package.EpochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await _context.hifi5PricingPackages.AddAsync(package);
            await _context.SaveChangesAsync();
            return package;
        }

        public async Task<Hifi5PricingPackage> UpdateAsync(Hifi5PricingPackage package)
        {
            package.EpochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Attach the entity and mark as modified to avoid unnecessary SELECT query
            var entry = _context.Entry(package);
            if (entry.State == EntityState.Detached)
            {
                _context.hifi5PricingPackages.Attach(package);
                entry.State = EntityState.Modified;
            }

            var affectedRows = await _context.SaveChangesAsync();

            // Throw exception if entity wasn't found (0 rows affected)
            if (affectedRows == 0)
            {
                throw new DbUpdateConcurrencyException("Entity not found or no changes were made.");
            }

            return package;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var package = await GetByIdAsync(id);
            if (package == null)
                return false;

            _context.hifi5PricingPackages.Remove(package);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.hifi5PricingPackages
                .AsNoTracking()
                .AnyAsync(p => p.Id == id);
        }
    }
}