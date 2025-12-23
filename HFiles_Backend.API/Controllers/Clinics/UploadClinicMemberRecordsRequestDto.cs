using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.API.DTOs.Clinics
{
    public class UploadClinicMemberRecordsRequestDto
    {
        [Required]
        public int ClinicId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public List<ClinicMemberRecordItemDto> Records { get; set; } = new();
    }
}
