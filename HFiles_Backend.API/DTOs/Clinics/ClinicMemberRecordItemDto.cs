using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.API.DTOs.Clinics
{
    public class ClinicMemberRecordItemDto
    {
        [Required]
        public string ReportName { get; set; } = null!;

       
        public string? ReportType { get; set; } 

        [Required]
        public IFormFile File { get; set; } = null!;
    }
}
