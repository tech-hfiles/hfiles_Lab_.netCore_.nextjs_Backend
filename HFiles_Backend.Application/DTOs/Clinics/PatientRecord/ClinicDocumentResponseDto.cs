using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HFiles_Backend.Application.DTOs.Clinics.PatientRecord
{
    public class ClinicDocumentResponseDto
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public long FileSizeInBytes { get; set; }
        public bool SendToPatient { get; set; }
        public long EpochTime { get; set; }
    }
}
