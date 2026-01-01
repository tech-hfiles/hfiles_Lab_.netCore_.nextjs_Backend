using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HFiles_Backend.Domain.Entities.Clinics;

namespace HFiles_Backend.Domain.Interfaces
{
    // ✅ Changed from "public class" to "public interface"
    public interface IHigh5ChocheFormRepository
    {
        Task<High5ChocheForm> SaveAsync(High5ChocheForm form);
        Task<High5ChocheForm?> GetByIdAsync(int id);
        Task<IEnumerable<High5ChocheForm>> GetByClinicIdAsync(int clinicId);
        Task<IEnumerable<High5ChocheForm>> GetByUserIdAsync(int userId);
        Task<High5ChocheForm?> UpdateAsync(High5ChocheForm form);
        Task<bool> DeleteAsync(int id);
        Task<High5ChocheForm?> GetByClinicUserAndFormNameAsync(int clinicId, int userId, string formName);
        Task<High5ChocheForm?> GetByClinicUserFormAndConsentAsync(
    int clinicId,
    int userId,
    string formName,
    int consentId
);
    }
}