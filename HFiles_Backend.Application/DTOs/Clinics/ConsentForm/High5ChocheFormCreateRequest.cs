using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HFiles_Backend.Application.DTOs.Clinics.ConsentForm
{
    public class High5ChocheFormCreateRequest
    {
        [Required(ErrorMessage = "Form name is required.")]
        [MaxLength(100, ErrorMessage = "Form name cannot exceed 100 characters.")]
        public string FormName { get; set; } = null!;

        [Required(ErrorMessage = "JSON data is required.")]
        public string JsonData { get; set; } = null!;

        public bool? IsSend { get; set; }
        public int? ConsentId { get; set; }
    }

    public class High5ChocheFormResponse
    {
        public int Id { get; set; }
        public int ClinicId { get; set; }
        public int UserId { get; set; }
        public string FormName { get; set; } = null!;
        public string JsonData { get; set; } = null!;
        public bool? IsSend { get; set; }
        public long EpochTime { get; set; }
        public int? ConsentId { get; set; }
    }
}
