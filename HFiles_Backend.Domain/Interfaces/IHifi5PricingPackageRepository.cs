using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HFiles_Backend.Domain.Entities.Clinics;

namespace HFiles_Backend.Domain.Interfaces
{
    public interface IHifi5PricingPackageRepository
    {
        Task<Hifi5PricingPackage?> GetByIdAsync(int id);
        Task<IEnumerable<Hifi5PricingPackage>> GetAllAsync();
        Task<IEnumerable<Hifi5PricingPackage>> GetByClinicIdAsync(int clinicId);
        Task<IEnumerable<Hifi5PricingPackage>> GetByProgramCategoryAsync(string programCategory);
        Task<IEnumerable<Hifi5PricingPackage>> GetByProgramNameAsync(string programName);

        Task<List<string>> GetProgramNamesAsync();

        Task<Hifi5PricingPackage> AddAsync(Hifi5PricingPackage package);
        Task<Hifi5PricingPackage?> UpdateAsync(Hifi5PricingPackage package);
        Task<bool> DeleteAsync(int id);
        Task<bool> ExistsAsync(int id);
    }
}