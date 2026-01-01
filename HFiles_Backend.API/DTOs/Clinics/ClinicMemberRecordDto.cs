namespace HFiles_Backend.API.DTOs.Clinics
{
    public class ClinicMemberRecordDto
    {
        public int Id { get; set; }
        public int ClinicId { get; set; }
        public int UserId { get; set; }
        public string ReportName { get; set; } = null!;
        public string ReportUrl { get; set; } = null!;
        public string? ReportType { get; set; } 
        public long FileSize { get; set; }
        public long EpochTime { get; set; }
    }
}
