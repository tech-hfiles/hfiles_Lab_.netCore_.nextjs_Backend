using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Entities.Users;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Labs
{
    [Table("labauditlogs")]
    public class LabAuditLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int? LabId { get; set; }
        public int? UserId { get; set; }
        public string? UserRole { get; set; }
        public int? BranchId { get; set; }
        public string? EntityName { get; set; }
        public string? Details { get; set; }
        public string? Notifications { get; set; }
        public int? SentToUserId { get; set; }
        public string? SentToUserNotifications { get; set; }
        public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public string? IpAddress { get; set; }
        public string? SessionId { get; set; }
        public string? Url { get; set; }
        public string? HttpMethod { get; set; }
        public string? Category { get; set; }
    }

}
