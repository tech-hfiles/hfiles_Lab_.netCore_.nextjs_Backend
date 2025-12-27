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
		[Required(ErrorMessage = "Clinic ID is required.")]
		[Range(1, int.MaxValue, ErrorMessage = "Clinic ID must be greater than zero.")]
		public int ClinicId { get; set; }

		[ForeignKey(nameof(ClinicId))]
		public ClinicSignup Clinic { get; set; } = null!;

		// ================= User =================
		[Required(ErrorMessage = "User ID is required.")]
		[Range(1, int.MaxValue, ErrorMessage = "User ID must be greater than zero.")]
		public int UserId { get; set; }

		[ForeignKey(nameof(UserId))]
		public User User { get; set; } = null!;

		// ================= Package =================
		[Required(ErrorMessage = "Package ID is required.")]
		[Range(1, int.MaxValue, ErrorMessage = "Package ID must be greater than zero.")]
		public int PackageId { get; set; }

		[Required, MaxLength(150)]
		public string PackageName { get; set; } = null!;

		// ================= Package Schedule =================
		[Required(ErrorMessage = "Package date is required.")]
		public DateTime PackageDate { get; set; }

		[Required(ErrorMessage = "Package time is required.")]
		public TimeSpan PackageTime { get; set; }

		// ================= Coach =================
		[Range(1, int.MaxValue, ErrorMessage = "Coach ID must be greater than zero.")]
		public int? CoachId { get; set; }

		[ForeignKey(nameof(CoachId))]  // ADD THIS LINE
		public ClinicMember? CoachMember { get; set; }  // ADD THIS LINE - Navigation to ClinicMember


		// ================= Status =================
		[Required]
		public High5AppointmentStatus Status { get; set; } = High5AppointmentStatus.Scheduled;



		// ================= Tracking =================
		[Required(ErrorMessage = "EpochTime is required.")]
		[Range(1, long.MaxValue, ErrorMessage = "EpochTime must be a valid timestamp.")]
		public long EpochTime { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
	}
}

