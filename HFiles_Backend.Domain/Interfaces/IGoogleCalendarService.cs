namespace HFiles_Backend.Domain.Interfaces
{
    public interface IGoogleCalendarService
    {
        /// <summary>
        /// Create a new appointment in Google Calendar
        /// </summary>
        Task<string?> CreateAppointmentAsync(
            int clinicId,
            string patientName,
            string clinicName,
            DateTime appointmentDate,
            TimeSpan appointmentTime,
            string phoneNumber);

        /// <summary>
        /// Update an existing appointment in Google Calendar
        /// </summary>
        Task<bool> UpdateAppointmentAsync(
            int clinicId,
            string eventId,
            string patientName,
            string clinicName,
            DateTime appointmentDate,
            TimeSpan appointmentTime,
            string phoneNumber);

        /// <summary>
        /// Delete an appointment from Google Calendar
        /// </summary>
        Task<bool> DeleteAppointmentAsync(int clinicId, string eventId);

        /// <summary>
        /// Cancel an appointment (marks as cancelled, doesn't delete)
        /// </summary>
        Task<bool> CancelAppointmentAsync(int clinicId, string eventId);

        /// <summary>
        /// Get appointment details from Google Calendar
        /// </summary>
        Task<object?> GetAppointmentAsync(int clinicId, string eventId);

        /// <summary>
        /// Get embeddable Google Calendar URL for a clinic
        /// </summary>
        Task<string?> GetCalendarEmbedUrlAsync(int clinicId);
    }
}