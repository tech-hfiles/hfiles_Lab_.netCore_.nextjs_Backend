using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.API.DTOs.Labs
{
    public class UserReportBatchUpload
    {
        [Required(ErrorMessage = "At least one entry is required.")]
        [MinLength(1, ErrorMessage = "The Entries list cannot be empty.")]
        public List<UserReportUpload> Entries { get; set; } = [];
    }
}
