using System.ComponentModel.DataAnnotations;

public class AddEnquiryCoachRequest
{
	[Required]
	public int ClinicEnquiryId { get; set; }

	[Required]
	public int CoachId { get; set; }
}


public class SyncEnquiryCoachesRequest
{
	public List<int> CoachIds { get; set; } = new();
}

