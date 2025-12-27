using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HFiles_Backend.Domain.Entities.Users;

namespace HFiles_Backend.Domain.Entities.Clinics
{
    public class High5ChocheForm
    {
        [Key]
        public int Id { get; set; }

        public int ClinicId { get; set; }

        [ForeignKey("ClinicId")]
        public ClinicSignup Clinics { get; set; } = null!;

        [Required(ErrorMessage = "User ID is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "User ID must be greater than zero.")]
        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        public string FormName   { get; set; }

        [Required]
        public string JsonData { get; set; } = null!;
        public bool? IsSend { get; set; }

        [Required(ErrorMessage = "EpochTime is required.")]
        [Range(1, long.MaxValue, ErrorMessage = "EpochTime must be a valid timestamp.")]
        public long EpochTime { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
