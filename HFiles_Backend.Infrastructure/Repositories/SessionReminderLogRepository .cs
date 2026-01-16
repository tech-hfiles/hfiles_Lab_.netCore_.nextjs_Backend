using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces.Clinics;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HFiles_Backend.Infrastructure.Repositories.Clinics
{
    public class SessionReminderLogRepository : ISessionReminderLogRepository
    {
        private readonly AppDbContext _context; // CHANGED: ApplicationDbContext -> AppDbContext

        public SessionReminderLogRepository(AppDbContext context) // CHANGED: ApplicationDbContext -> AppDbContext
        {
            _context = context;
        }

        public async Task<bool> HasReminderBeenSentAsync(int userId, int? packageId, string reminderType, DateTime lastSessionDate)
        {
            return await _context.SessionReminderLogs
                .AnyAsync(log =>
                    log.UserId == userId &&
                    log.PackageId == packageId &&
                    log.ReminderType == reminderType &&
                    log.LastSessionDate.Date == lastSessionDate.Date);
        }

        public async Task<SessionReminderLog> CreateReminderLogAsync(SessionReminderLog log)
        {
            _context.SessionReminderLogs.Add(log);
            await _context.SaveChangesAsync();
            return log;
        }

        public async Task<List<SessionReminderLog>> GetReminderLogsByClinicAsync(int clinicId)
        {
            return await _context.SessionReminderLogs
                .Where(log => log.ClinicId == clinicId)
                .OrderByDescending(log => log.SentAt)
                .ToListAsync();
        }
    }
}