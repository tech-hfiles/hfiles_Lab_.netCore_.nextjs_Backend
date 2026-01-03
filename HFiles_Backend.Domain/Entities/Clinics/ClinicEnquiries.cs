	using System;
	using System.ComponentModel.DataAnnotations;
	using System.ComponentModel.DataAnnotations.Schema;
	using System.Numerics;
	using HFiles_Backend.Domain.Entities.Users;
	using HFiles_Backend.Domain.Enums;

	namespace HFiles_Backend.Domain.Entities.Clinics
	{
		public class ClinicEnquiry
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

			// ================= Personal Details =================
			[Required, MaxLength(100)]
			public string Firstname { get; set; } = null!;

			[Required, MaxLength(100)]
			public string Lastname { get; set; } = null!;

			[EmailAddress, MaxLength(150)]
			public string? Email { get; set; }

			[MaxLength(15)]
			public string? Contact { get; set; }

			public DateTime? DateOfBirth { get; set; }

			[MaxLength(100)]
			public string? Source { get; set; }

			public DateTime? FollowUpDate { get; set; }

			// ================= Fitness =================
			[MaxLength(255)]
			public string? FitnessGoal { get; set; }

			// ================= Status =================
			[Required]
			public EnquiryStatus Status { get; set; } = EnquiryStatus.Inquiry;

			public PaymentStatus Payment { get; set; } = PaymentStatus.NA;

			// ================= Appointment =================
			public DateTime? AppointmentDate { get; set; }
			public TimeSpan? AppointmentTime { get; set; }


			public bool FirstCall { get; set; }
			public bool SecondCall { get; set; }


			// ================= Notes =================
			public string? Remark { get; set; }

		
			// ================= Tracking =================
		
			[Required(ErrorMessage = "EpochTime is required.")]
			[Range(1, long.MaxValue, ErrorMessage = "EpochTime must be a valid timestamp.")]
			public long EpochTime { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

		// Add this at the end of your ClinicEnquiry class
		public virtual ICollection<ClinicEnquiryCoach> AssignedCoaches { get; set; } = new List<ClinicEnquiryCoach>();

	}
	}
