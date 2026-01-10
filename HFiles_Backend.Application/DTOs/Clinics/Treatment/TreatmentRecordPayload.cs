using Newtonsoft.Json;

namespace HFiles_Backend.Application.DTOs.Clinics.Treatment
{
	public class TreatmentRecordPayload
	{
		[JsonProperty("treatments")]
		public List<TreatmentDetail> Treatments { get; set; } = new();
	}

	public class TreatmentDetail
	{
		[JsonProperty("coach")]
		public string? Coach { get; set; }

		[JsonProperty("name")]
		public string Name { get; set; } = string.Empty;
	}

}
