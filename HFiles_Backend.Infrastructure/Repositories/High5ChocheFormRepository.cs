using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using HFiles_Backend.Domain.Entities.Clinics; // ✅ Add this
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore; // ✅ Add this for Include() and LINQ methods

namespace HFiles_Backend.Infrastructure.Repositories
{
    public class High5ChocheFormRepository : IHigh5ChocheFormRepository
    {
        private readonly AppDbContext _context;

        public High5ChocheFormRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<High5ChocheForm> SaveAsync(High5ChocheForm form)
        {
            await _context.high5ChocheForms.AddAsync(form);
            await _context.SaveChangesAsync();
            return form;
        }

        public async Task<High5ChocheForm?> GetByIdAsync(int id)
        {
            return await _context.high5ChocheForms
                .Include(f => f.Clinics)
                .Include(f => f.User)
                .FirstOrDefaultAsync(f => f.Id == id);
        }

        public async Task<IEnumerable<High5ChocheForm>> GetByClinicIdAsync(int clinicId)
        {
            return await _context.high5ChocheForms
                .Include(f => f.User)
                .Where(f => f.ClinicId == clinicId)
                .OrderByDescending(f => f.EpochTime)
                .ToListAsync();
        }

        public async Task<IEnumerable<High5ChocheForm>> GetByUserIdAsync(int userId)
        {
            return await _context.high5ChocheForms
                .Include(f => f.Clinics)
                .Where(f => f.UserId == userId)
                .OrderByDescending(f => f.EpochTime)
                .ToListAsync();
        }

        public async Task<High5ChocheForm?> UpdateAsync(High5ChocheForm form)
        {
            _context.high5ChocheForms.Update(form);
            await _context.SaveChangesAsync();
            return form;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var form = await _context.high5ChocheForms.FindAsync(id);
            if (form == null) return false;

            _context.high5ChocheForms.Remove(form);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<High5ChocheForm?> GetByClinicUserAndFormNameAsync(
    int clinicId, int userId, string formName)
        {
            var trimmedFormName = formName.Trim();

            return await _context.high5ChocheForms
                .FirstOrDefaultAsync(f =>
                    f.ClinicId == clinicId &&
                    f.UserId == userId &&
                    f.FormName.Trim() == trimmedFormName
                );
        }


        public async Task<High5ChocheForm?> GetByConsentIdAsync(
             int clinicId,
             int userId,
             string consentId)
        {
            var forms = await _context.high5ChocheForms
                .Where(f => f.ClinicId == clinicId && f.UserId == userId)
                .ToListAsync(); // Load data into memory first

            return forms.FirstOrDefault(f =>
            {
                try
                {
                    var jsonDoc = JsonDocument.Parse(f.JsonData);
                    if (jsonDoc.RootElement.TryGetProperty("urlParameters", out var urlParams))
                    {
                        if (urlParams.TryGetProperty("consentId", out var consentIdElement))
                        {
                            return consentIdElement.GetString() == consentId;
                        }
                    }
                }
                catch
                {
                    return false;
                }
                return false;
            });
        }
}
}