using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.API.DTOs.Clinics
{
    public class ConsentFormUploadRequest
    {
        [Required]
        public string ConsentFormTitle { get; set; } = null!;

        [Required]
        public IFormFile PdfFile { get; set; } = null!;
    }
}
