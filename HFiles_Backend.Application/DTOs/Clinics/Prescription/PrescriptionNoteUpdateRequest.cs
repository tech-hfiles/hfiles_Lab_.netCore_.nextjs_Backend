using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HFiles_Backend.Application.DTOs.Clinics.Prescription
{
    public class PrescriptionNoteUpdateRequest
    {
        [Required(ErrorMessage = "Notes are required")]
        [MaxLength(10000, ErrorMessage = "Notes cannot exceed 10000 characters")]
        [MinLength(1, ErrorMessage = "Notes cannot be empty")]
        public string? Notes { get; set; }
    }
}
