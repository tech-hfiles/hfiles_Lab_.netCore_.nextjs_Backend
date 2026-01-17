using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HFiles_Backend.Application.DTOs.Clinics.PatientRecord
{
    public class UpdateFileNameRequest
    {
        [Required(ErrorMessage = "File name is required")]
        [StringLength(255, ErrorMessage = "File name cannot exceed 255 characters")]
        public string FileName { get; set; } = string.Empty;
    }
}
