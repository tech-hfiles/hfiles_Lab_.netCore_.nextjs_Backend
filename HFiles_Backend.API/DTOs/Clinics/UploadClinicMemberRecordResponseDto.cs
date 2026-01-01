namespace HFiles_Backend.API.DTOs.Clinics
{
    public class UploadClinicMemberRecordResponseDto
    {
        public string ReportName { get; set; } = null!;
        public string ReportUrl { get; set; } = null!;
        public string? ReportType { get; set; }
        public long FileSize { get; set; }
    }
}
