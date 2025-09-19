namespace HFiles_Backend.Application.DTOs.Clinics.ConsentForm
{
    // Helper class for consent form link information
    public class ConsentFormLinkInfo
    {
        public int ConsentFormId { get; set; }
        public string ConsentFormName { get; set; } = null!;
        public string ConsentFormLink { get; set; } = null!;
    }
}
