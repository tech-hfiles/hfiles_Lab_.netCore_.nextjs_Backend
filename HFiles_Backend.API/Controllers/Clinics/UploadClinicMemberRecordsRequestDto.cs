using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.API.DTOs.Clinics
{
    public class UploadClinicMemberRecordsRequestDto
    {
        [Required(ErrorMessage = "Clinic ID is required.")]
        public int ClinicId { get; set; }

        [Required(ErrorMessage = "Clinic Member ID is required.")]
        public int ClinicMemberId { get; set; }

        [Required(ErrorMessage = "At least one record is required.")]
        public List<ClinicMemberRecordItemDto> Records { get; set; } = new();
    }
}
