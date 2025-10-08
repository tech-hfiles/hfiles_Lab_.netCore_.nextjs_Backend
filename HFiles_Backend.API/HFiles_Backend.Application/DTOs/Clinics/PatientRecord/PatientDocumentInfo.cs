using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HFiles_Backend.Application.DTOs.Clinics.PatientRecord
{
    public class PatientDocumentInfo
    {
        public string DocumentType { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? DocumentUrl { get; set; }
        public string? TempFilePath { get; set; }
    }
}
