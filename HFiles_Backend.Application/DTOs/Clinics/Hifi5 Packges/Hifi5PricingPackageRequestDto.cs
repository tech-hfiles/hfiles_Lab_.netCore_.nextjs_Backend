using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HFiles_Backend.Application.DTOs.Clinics.Hifi5_Packges
{
    public class Hifi5PricingPackageRequestDto
    {
        [Required(ErrorMessage = "Clinic ID is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Clinic ID must be greater than zero.")]
        public int ClinicId { get; set; }

        [Required(ErrorMessage = "Program Category is required.")]
        [MaxLength(50, ErrorMessage = "Program Category must not exceed 50 characters.")]
        public string ProgramCategory { get; set; } = null!;

        [Required(ErrorMessage = "Program Name is required.")]
        [MaxLength(100, ErrorMessage = "Program Name must not exceed 100 characters.")]
        public string ProgramName { get; set; } = null!;

        [Required(ErrorMessage = "Duration in Months is required.")]
        [Range(0, int.MaxValue, ErrorMessage = "Duration Months must be zero or greater.")]
        public int DurationMonths { get; set; }

        [Required(ErrorMessage = "Frequency is required.")]
        [MaxLength(50, ErrorMessage = "Frequency must not exceed 50 characters.")]
        public string Frequency { get; set; } = null!;

        [Required(ErrorMessage = "Total Sessions is required.")]
        [Range(0, int.MaxValue, ErrorMessage = "Total Sessions must be greater than zero.")]
        public int TotalSessions { get; set; }

        [Required(ErrorMessage = "Price in INR is required.")]
        [Range(0.01, 9999999999.99, ErrorMessage = "Price must be between 0.01 and 9999999999.99.")]
        public decimal PriceInr { get; set; }

        [Required(ErrorMessage = "Includes Physio is required.")]
        [MaxLength(10, ErrorMessage = "Includes Physio must not exceed 10 characters.")]
        public string IncludesPhysio { get; set; } = null!;

        [Required(ErrorMessage = "Physio Sessions is required.")]
        [Range(0, int.MaxValue, ErrorMessage = "Physio Sessions must be zero or greater.")]
        public int PhysioSessions { get; set; }

        [Required(ErrorMessage = "Extension Allowed is required.")]
        [MaxLength(10, ErrorMessage = "Extension Allowed must not exceed 10 characters.")]
        public string ExtensionAllowed { get; set; } = null!;
        public string HSN { get; set; }
    }
}