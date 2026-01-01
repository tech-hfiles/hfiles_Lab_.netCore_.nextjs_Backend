using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HFiles_Backend.Application.DTOs.Clinics.Hifi5_Packges
{
    public class Hifi5PricingPackageResponseDto
    {
        public int Id { get; set; }
        public int ClinicId { get; set; }
        public string ProgramCategory { get; set; } = null!;
        public string ProgramName { get; set; } = null!;
        public int DurationMonths { get; set; }
        public string Frequency { get; set; } = null!;
        public int TotalSessions { get; set; }
        public decimal PriceInr { get; set; }
        public string IncludesPhysio { get; set; } = null!;
        public int PhysioSessions { get; set; }
        public string ExtensionAllowed { get; set; } = null!;
        public string? HSN { get; set; }
        public long EpochTime { get; set; }
    }
}