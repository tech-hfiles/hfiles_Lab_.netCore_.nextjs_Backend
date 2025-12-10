using HFiles_Backend.Domain.Entities.Labs;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Users
{
    /// <summary>
    /// Tracks user-specific notification state (read/unread, dismissed)
    /// Links to LabAuditLog for actual notification content
    /// This entity is shared between Clinic Backend and User Backend
    /// </summary>
    [Table("usernotifications")]
    public class UserNotification
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// The user who will receive this notification
        /// </summary>
        [Required]
        public int UserId { get; set; }

        /// <summary>
        /// Reference to the audit log entry (notification source)
        /// </summary>
        [Required]
        public int AuditLogId { get; set; }

        /// <summary>
        /// Has the user read/viewed this notification?
        /// </summary>
        public bool IsRead { get; set; } = false;

        /// <summary>
        /// Has the user dismissed this notification?
        /// </summary>
        public bool IsDismissed { get; set; } = false;

        /// <summary>
        /// When the notification was created
        /// </summary>
        public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        /// <summary>
        /// When the notification was read (null if unread)
        /// </summary>
        public long? ReadAt { get; set; }

        /// <summary>
        /// When the notification was dismissed (null if not dismissed)
        /// </summary>
        public long? DismissedAt { get; set; }

        /// <summary>
        /// Notification priority (1=Low, 2=Normal, 3=High, 4=Urgent)
        /// </summary>
        public int Priority { get; set; } = 2;

        /// <summary>
        /// Navigation property to audit log
        /// </summary>
        [ForeignKey(nameof(AuditLogId))]
        public LabAuditLog? AuditLog { get; set; }
    }
}
