using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Clinics
{
    public class SessionReminderLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ClinicId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int PackageId { get; set; }

        [Required]
        [MaxLength(20)]
        public string ReminderType { get; set; } = null!; // "5-Day" or "1-Day"

        [Required]
        public DateTime LastSessionDate { get; set; }

        [Required]
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(255)]
        public string EmailSentTo { get; set; } = null!;

        [MaxLength(200)]
        public string? PackageName { get; set; }

        // Composite unique index to prevent duplicate emails
        // Add this in your DbContext OnModelCreating:
        // modelBuilder.Entity<SessionReminderLog>()
        //     .HasIndex(s => new { s.UserId, s.PackageId, s.ReminderType, s.LastSessionDate })
        //     .IsUnique();
    }
}