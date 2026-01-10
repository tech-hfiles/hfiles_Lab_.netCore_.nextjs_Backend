using System.Text.Json.Serialization;
using HFiles_Backend.Domain.Enums;
using System.Text.Json.Serialization;

public class CreateClinicEnquiryDto
{
	public int ClinicId { get; set; }      // Required for CREATE
	public int UserId { get; set; }        // Required for CREATE

	public string? Firstname { get; set; }
	public string? Lastname { get; set; }
	public string? Email { get; set; }
	public string? Contact { get; set; }

	public DateTime? DateOfBirth { get; set; }
	public string? Source { get; set; }
	public DateTime? FollowUpDate { get; set; }
	public string? FitnessGoal { get; set; }

	public EnquiryStatus? Status { get; set; }
	public PaymentStatus? Payment { get; set; }

	public DateTime? AppointmentDate { get; set; }
	public TimeSpan? AppointmentTime { get; set; }
	public int? PricingPackageId { get; set; }

	public string? Remark { get; set; }

	// 🔹 CALL STATUS (IMPORTANT)
	public bool? FirstCall { get; set; }
	public bool? SecondCall { get; set; }
}


public class UpdateClinicEnquiryDto
{
	[JsonPropertyName("status")]
	public EnquiryStatus? Status { get; set; }

	[JsonPropertyName("payment")]
	public PaymentStatus? Payment { get; set; }

	[JsonPropertyName("appointmentDate")]
	public DateTime? AppointmentDate { get; set; }

	[JsonPropertyName("appointmentTime")]
	public TimeSpan? AppointmentTime { get; set; }

	[JsonPropertyName("remark")]
	public string? Remark { get; set; }

	[JsonPropertyName("followUpDate")]
	public DateTime? FollowUpDate { get; set; }

	[JsonPropertyName("firstCall")]
	public bool? FirstCall { get; set; }

	[JsonPropertyName("secondCall")]
	public bool? SecondCall { get; set; }
}
