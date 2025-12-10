using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HFiles_Backend.Application.DTOs.Clinics.Prescription
{
    public class PrescriptionNoteResponse
    {
        public int Id { get; set; }
        public string? Notes { get; set; }
    }
}
