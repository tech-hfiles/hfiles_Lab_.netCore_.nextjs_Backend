namespace HFiles_Backend.API.Interfaces
{
    public interface IGoogleCalendarService
    {
        Task<string?> CreateAppointmentAsync(
            int clinicId,
            string patientName,
            string clinicName,
            DateTime appointmentDate,
            TimeSpan appointmentTime,
            string? phoneNumber = null,
            int durationMinutes = 30);

        Task<bool> UpdateAppointmentAsync(
            int clinicId,
            string eventId,
            DateTime newDate,
            TimeSpan newTime,
            int durationMinutes = 30);

        Task<bool> CancelAppointmentAsync(int clinicId, string eventId);
        Task<bool> DeleteAppointmentAsync(int clinicId, string eventId);
        Task<string?> GetCalendarEmbedUrlAsync(int clinicId);
    }
}
