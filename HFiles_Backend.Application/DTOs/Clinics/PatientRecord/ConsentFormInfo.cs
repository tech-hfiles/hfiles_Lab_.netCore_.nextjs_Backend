namespace HFiles_Backend.Application.DTOs.Clinics.PatientRecord
{
    public class ConsentFormInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
        public string Category { get; set; } = string.Empty;
    }
}
