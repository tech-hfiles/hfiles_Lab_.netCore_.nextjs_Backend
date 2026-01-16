using HFiles_Backend.Domain.Entities.Clinics;

namespace HFiles_Backend.Domain.Interfaces.Clinics
{
    public interface ISessionReminderLogRepository
    {
        Task<bool> HasReminderBeenSentAsync(int userId, int? packageId, string reminderType, DateTime lastSessionDate);
        Task<SessionReminderLog> CreateReminderLogAsync(SessionReminderLog log);
        Task<List<SessionReminderLog>> GetReminderLogsByClinicAsync(int clinicId);
    }
}