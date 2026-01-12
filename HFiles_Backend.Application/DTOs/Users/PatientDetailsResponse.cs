namespace HFiles_Backend.Application.DTOs.Users
{
    public class PatientDetailsResponse
    {
        public int PatientId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Gender { get; set; }
        public string? DOB { get; set; }
        public string? BloodGroup { get; set; }
        public string? HfId { get; set; }
        public string? ProfileURL { get; set; }
        public string? PhoneNumber { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public decimal AmountDue { get; set; } = 0;
        public string? Email { get; set; }
        public string? CountryCallingCode { get; set; }
    }
}
