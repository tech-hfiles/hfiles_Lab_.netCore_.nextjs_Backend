namespace HFiles_Backend.Application.DTOs.Clinics.ConsentForm
{
    namespace HFiles_Backend.API.DTOs.Clinics
    {
        public class ClinicConsentFormResponse
        {
            public string Title { get; set; } = null!;
            public string? ConsentFormUrl { get; set; }
            public bool IsVerified { get; set; }
        }
    }
}
