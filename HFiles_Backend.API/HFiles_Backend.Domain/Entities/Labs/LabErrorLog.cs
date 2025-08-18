using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Labs
{
    [Table("laberrorlogs")]
    public class LabErrorLog
    {
        public int Id { get; set; }
        public int? LabId { get; set; }
        public int? UserId { get; set; }
        public string? UserRole { get; set; }
        public string? EntityName { get; set; }
        public int? EntityId { get; set; }
        public string? Action { get; set; }
        public string? ErrorMessage { get; set; }
        public string? StackTrace { get; set; }
        public long? Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public string? IpAddress { get; set; }
        public string? SessionId { get; set; }
        public string? Url { get; set; }
        public string? HttpMethod { get; set; }
        public string? Category { get; set; }
    }


}
