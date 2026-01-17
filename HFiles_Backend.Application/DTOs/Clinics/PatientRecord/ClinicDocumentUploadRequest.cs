using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace HFiles_Backend.Application.DTOs.Clinics.PatientRecord
{
    public class ClinicDocumentUploadRequest
    {
        [Required(ErrorMessage = "At least one file is required.")]
        public List<IFormFile> Files { get; set; } = new List<IFormFile>();

        // Manual filename for each file (optional - if not provided, use original filename)
        public List<string>? FileNames { get; set; }

        public bool SendToPatient { get; set; } = false;
    }
}