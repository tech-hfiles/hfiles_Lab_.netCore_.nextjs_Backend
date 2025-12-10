namespace HFiles_Backend.Application.DTOs.Clinics.PatientRecord
{
    public class ConsentFormSimple
    {
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
    }
}
