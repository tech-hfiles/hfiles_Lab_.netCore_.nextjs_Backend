namespace HFiles_Backend.Application.DTOs.Clinics.ConsentForm
{
    namespace HFiles_Backend.API.DTOs.Clinics
    {
        public class ClinicConsentFormResponse
        {
            public int ConsentFormId { get; set; }
            public int ClinicConsentFormId { get; set; }
            public string Title { get; set; } = null!;
            public string? ConsentFormUrl { get; set; }
            public bool IsVerified { get; set; }
            public long? EpochTime { get; set; }
        }
    }
}
