

using System;
using System.ComponentModel.DataAnnotations;
using HFiles_Backend.Domain.Enums;

namespace HFiles_Backend.Domain.DTOs.Clinics
{
	public class High5AppointmentDto
	{
		public int Id { get; set; }

		// ================= Clinic =================
		[Required(ErrorMessage = "Clinic ID is required.")]
		public int ClinicId { get; set; }

		// ================= User =================
		[Required(ErrorMessage = "User ID is required.")]
		public int UserId { get; set; }

		// ================= Package =================
		[Required(ErrorMessage = "Package ID is required.")]
		public int PackageId { get; set; }

		[Required, MaxLength(150)]
		public string PackageName { get; set; } = null!;

		// ================= Package Schedule =================
		[Required(ErrorMessage = "Package date is required.")]
		public DateTime PackageDate { get; set; }

		[Required(ErrorMessage = "Package time is required.")]
		public TimeSpan PackageTime { get; set; }

		// ================= Coach =================
		public int? CoachId { get; set; }

		// ================= Status =================
		public High5AppointmentStatus Status { get; set; }


	}
	public class High5AppointmentUpdateDto
	{
		public int? PackageId { get; set; }
		public string? PackageName { get; set; }
		public DateTime? PackageDate { get; set; }
		public TimeSpan? PackageTime { get; set; }
		public int? CoachId { get; set; }
		public High5AppointmentStatus? Status { get; set; }
	}

}

