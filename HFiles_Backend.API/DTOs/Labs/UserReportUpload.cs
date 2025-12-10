using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.API.DTOs.Labs
{
    public class UserReportUpload
    {
        [Required(ErrorMessage = "HFID is required.")]
        public string HFID { get; set; } = null!;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Name is required.")]
        public string Name { get; set; } = null!;

        [Required(ErrorMessage = "At least one report type is required.")]
        [MinLength(1, ErrorMessage = "At least one report type must be provided.")]
        public List<string> ReportTypes { get; set; } = null!;

        [Required(ErrorMessage = "At least one report file is required.")]
        [MinLength(1, ErrorMessage = "At least one report file must be uploaded.")]
        public List<IFormFile> ReportFiles { get; set; } = [];

        [Required(ErrorMessage = "BranchId is required.")]
        public int BranchId { get; set; }
    }
}
