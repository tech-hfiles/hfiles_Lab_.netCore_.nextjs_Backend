using System.ComponentModel.DataAnnotations;

public class AddEnquiryCoachRequest
{
	[Required]
	public int ClinicEnquiryId { get; set; }

	[Required]
	public int CoachId { get; set; }
}
