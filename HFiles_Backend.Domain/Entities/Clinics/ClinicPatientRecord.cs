using HFiles_Backend.Domain.Entities.Users;
using HFiles_Backend.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Clinics
{
    public class ClinicPatientRecord
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ClinicId { get; set; }

        [ForeignKey("ClinicId")]
        public ClinicSignup Clinic { get; set; } = null!;

        [Required]
        public int PatientId { get; set; }

        [ForeignKey("PatientId")]
        public ClinicPatient Patient { get; set; } = null!;

        [Required]
        public int ClinicVisitId { get; set; }

        [ForeignKey("ClinicVisitId")]
        public ClinicVisit Visit { get; set; } = null!;

        [Required]
        public RecordType Type { get; set; }

        [MaxLength(20)]
        public string? UniqueRecordId { get; set; }

        public int? Reference_Id { get; set; }
        public bool? payment_verify { get; set; }

        public bool Is_Cansel { get; set; }
        public bool? Is_editable { get; set; }

        [Required]
        public string JsonData { get; set; } = null!;
        public bool SendToPatient { get; set; }

		[Required]
		public bool IsGoalSettingRequired { get; set; } = true;


		[Required(ErrorMessage = "EpochTime is required.")]
        [Range(1, long.MaxValue, ErrorMessage = "EpochTime must be a valid timestamp.")]



		public long EpochTime { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
