using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Clinics
{
    /// <summary>
    /// Stores Google OAuth tokens for each clinic to access their Google Calendar
    /// </summary>
    [Table("clinic_google_tokens")]
    public class ClinicGoogleToken
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ClinicId { get; set; }

        [ForeignKey("ClinicId")]
        public ClinicSignup Clinic { get; set; } = null!;

        /// <summary>
        /// Google Calendar ID (usually "primary" or specific calendar ID)
        /// </summary>
        [MaxLength(255)]
        public string CalendarId { get; set; } = "primary";

        /// <summary>
        /// OAuth 2.0 Access Token (encrypted in DB)
        /// </summary>
        [Required]
        public string AccessToken { get; set; } = null!;

        /// <summary>
        /// OAuth 2.0 Refresh Token (encrypted in DB) - doesn't expire
        /// </summary>
        [Required]
        public string RefreshToken { get; set; } = null!;

        /// <summary>
        /// When the access token expires (access tokens expire in 1 hour)
        /// </summary>
        [Required]
        public DateTime TokenExpiry { get; set; }

        /// <summary>
        /// Scopes granted by the user
        /// </summary>
        [MaxLength(500)]
        public string Scope { get; set; } = "https://www.googleapis.com/auth/calendar";

        /// <summary>
        /// Token type (usually "Bearer")
        /// </summary>
        [MaxLength(50)]
        public string TokenType { get; set; } = "Bearer";

        /// <summary>
        /// Whether the token is currently valid
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Last time the token was refreshed
        /// </summary>
        public DateTime? LastRefreshedAt { get; set; }

        /// <summary>
        /// When this record was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Last update timestamp
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
