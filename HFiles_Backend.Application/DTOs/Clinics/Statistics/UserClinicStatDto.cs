namespace HFiles_Backend.Application.DTOs.Clinics.Statistics
{
    public class UserClinicStatDto
    {
        public int UserId { get; set; }
        public string? HFID { get; set; }
        public string FullName { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;
        public string Email { get; set; } = null!;
        public DateTime FirstVisitDate { get; set; }
        public DateTime UserCreatedDate { get; set; }
    }
}
