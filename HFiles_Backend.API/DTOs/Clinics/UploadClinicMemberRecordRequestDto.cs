using Microsoft.AspNetCore.Http;

namespace HFiles_Backend.API.DTOs.Clinics
{
    public class UploadClinicMemberRecordRequestDto
    {
        public int ClinicId { get; set; }
        public int UserId { get; set; }
        public string ReportName { get; set; } = null!;
        public string ReportType { get; set; } = null!;
        public IFormFile File { get; set; } = null!;
    }
}
