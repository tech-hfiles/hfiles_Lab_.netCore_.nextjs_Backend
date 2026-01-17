using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HFiles_Backend.Domain.Entities.Clinics
{
    public class Hifi5PricingPackage
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Clinic ID is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Clinic ID must be greater than zero.")]
        public int ClinicId { get; set; }

        [ForeignKey(nameof(ClinicId))]
        public ClinicSignup Clinic { get; set; } = null!;

        [Required(ErrorMessage = "Program Category is required.")]
        [MaxLength(50, ErrorMessage = "Program Category must not exceed 50 characters.")]
        [Column("Program_Category")]
        public string ProgramCategory { get; set; } = null!;

        [Required(ErrorMessage = "Program Name is required.")]
        [MaxLength(100, ErrorMessage = "Program Name must not exceed 100 characters.")]
        [Column("Program_Name")]
        public string ProgramName { get; set; } = null!;

        [Required(ErrorMessage = "Duration in Months is required.")]
        [Range(0, int.MaxValue, ErrorMessage = "Duration Months must be zero or greater.")]
        [Column("Duration_Months")]
        public int DurationMonths { get; set; } = 0;

        [Required(ErrorMessage = "Frequency is required.")]
        [MaxLength(50, ErrorMessage = "Frequency must not exceed 50 characters.")]
        [Column("Frequency")]
        public string Frequency { get; set; } = null!;

        [Required(ErrorMessage = "Total Sessions is required.")]
        [Range(0, int.MaxValue, ErrorMessage = "Total Sessions must be zero or greater.")]
        [Column("Total_Sessions")]
        public int? TotalSessions { get; set; }

        [Required(ErrorMessage = "Price in INR is required.")]
        [Range(0.01, 9999999999.99, ErrorMessage = "Price must be between 0.01 and 9999999999.99.")]
        [Column("Price_INR", TypeName = "decimal(10,2)")]
        public decimal PriceInr { get; set; }

        [Required(ErrorMessage = "Includes Physio is required.")]
        [MaxLength(10, ErrorMessage = "Includes Physio must not exceed 10 characters.")]
        [Column("Includes_Physio")]
        public string IncludesPhysio { get; set; } = null!;

        [Required(ErrorMessage = "Physio Sessions is required.")]
        [Range(0, int.MaxValue, ErrorMessage = "Physio Sessions must be zero or greater.")]
        [Column("Physio_Sessions")]
        public int PhysioSessions { get; set; } = 0;

        [Required(ErrorMessage = "Extension Allowed is required.")]
        [MaxLength(10, ErrorMessage = "Extension Allowed must not exceed 10 characters.")]
        [Column("Extension_Allowed")]
        public string ExtensionAllowed { get; set; } = null!;

        [Required(ErrorMessage = "HSN is required.")]
        [StringLength(8, MinimumLength = 4, ErrorMessage = "HSN must be 4 to 8 digits.")]
        [Column("HSN")]
        public string? HsnNumber { get; set; } = null!;


        [Required(ErrorMessage = "EpochTime is required.")]
        [Range(1, long.MaxValue, ErrorMessage = "EpochTime must be a valid timestamp.")]
        public long EpochTime { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
