using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Clinics
{
	[Table("ClinicEnquiryCoaches")]
	public class ClinicEnquiryCoach
	{
		[Key]
		public int Id { get; set; }

		[ForeignKey("ClinicEnquiry")]
		public int EnquiryId { get; set; }
		public ClinicEnquiry ClinicEnquiry { get; set; }

		[ForeignKey("ClinicMember")]
		public int CoachId { get; set; }
		public ClinicMember ClinicMember { get; set; }

		public long EpochTime { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
	}
}
