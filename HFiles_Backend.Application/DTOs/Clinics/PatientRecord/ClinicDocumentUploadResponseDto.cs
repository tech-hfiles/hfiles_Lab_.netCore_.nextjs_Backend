using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HFiles_Backend.Application.DTOs.Clinics.PatientRecord
{
    public class ClinicDocumentUploadResponseDto
    {
        public int ClinicId { get; set; }
        public int PatientId { get; set; }
        public int TotalFilesUploaded { get; set; }
        public List<ClinicDocumentResponseDto> UploadedFiles { get; set; } = new List<ClinicDocumentResponseDto>();
        public string Message { get; set; } = string.Empty;
    }
}
