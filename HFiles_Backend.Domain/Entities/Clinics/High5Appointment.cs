using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HFiles_Backend.Domain.Entities.Users;
using HFiles_Backend.Domain.Enums;

namespace HFiles_Backend.Domain.Entities.Clinics
{
	public class High5Appointment
	{
		[Key]
		public int Id { get; set; }

		// ================= Clinic =================
		[Required]
		[Range(1, int.MaxValue)]
		public int ClinicId { get; set; }

		[ForeignKey(nameof(ClinicId))]
		public ClinicSignup Clinic { get; set; } = null!;

		// ================= User (Login User) =================
		[Required]
		[Range(1, int.MaxValue)]
		public int UserId { get; set; }

		[ForeignKey(nameof(UserId))]
		public User User { get; set; } = null!;

		// ================= Patient (Clinic Patient) =================
		[Required(ErrorMessage = "Patient ID is required.")]
		[Range(1, int.MaxValue, ErrorMessage = "Patient ID must be greater than zero.")]
		public int PatientId { get; set; }

		// ================= Clinic Visit =================
		[Required(ErrorMessage = "Clinic Visit ID is required.")]
		[Range(1, int.MaxValue, ErrorMessage = "Clinic Visit ID must be greater than zero.")]
		public int ClinicVisitId { get; set; }

		// ================= Package =================
		[Required]
		[Range(1, int.MaxValue)]
		public int PackageId { get; set; }

		[Required, MaxLength(150)]
		public string PackageName { get; set; } = null!;

		// ================= Package Schedule =================
		[Required]
		public DateTime PackageDate { get; set; }

		[Required]
		public TimeSpan PackageTime { get; set; }

		// ================= Coach =================
		[Range(1, int.MaxValue)]
		public int? CoachId { get; set; }

		[ForeignKey(nameof(CoachId))]
		public ClinicMember? CoachMember { get; set; }

		// ================= Status =================
		[Required]
		public High5AppointmentStatus Status { get; set; } = High5AppointmentStatus.Scheduled;

		// ================= Tracking =================
		[Required]
		[Range(1, long.MaxValue)]
		public long EpochTime { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

		// ================= Record Reference =================
		[MaxLength(20)]
		public string? UniqueRecordId { get; set; }

	}
}
