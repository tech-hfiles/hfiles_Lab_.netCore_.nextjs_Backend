using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Labs
{
    public class LabNotification
    {
        [Key]
        public int Id { get; set; }

        public int LabId { get; set; }

        [MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string Message { get; set; } = string.Empty;

        public bool IsRead { get; set; } = false;

        public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        [MaxLength(250)]
        public string? RedirectUrl { get; set; }

        public int? AuditLogId { get; set; }

        [ForeignKey("AuditLogId")]
        public LabAuditLog? AuditLog { get; set; }
    }
}
