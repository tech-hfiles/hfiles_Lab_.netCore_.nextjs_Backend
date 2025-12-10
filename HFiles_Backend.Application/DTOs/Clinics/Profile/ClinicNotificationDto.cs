namespace HFiles_Backend.Application.DTOs.Clinics.Profile
{
    public class ClinicNotificationDto
    {
        public int Id { get; set; }
        public int? ClinicId { get; set; }
        public string? UserRole { get; set; } 
        public string? EntityName { get; set; } 
        public string? Category { get; set; } 
        public long Timestamp { get; set; }
        public string? Notifications { get; set; }
        public long ElapsedMinutes { get; set; }
    }
}
