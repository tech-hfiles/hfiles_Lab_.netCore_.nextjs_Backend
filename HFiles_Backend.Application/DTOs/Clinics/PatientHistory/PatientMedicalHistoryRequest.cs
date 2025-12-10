using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Application.DTOs.Clinics.PatientHistory
{
    /// <summary>
    /// Request DTO for creating or updating patient medical history
    /// </summary>
    public class PatientMedicalHistoryRequest
    {
        [Required(ErrorMessage = "HFID is required.")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "HFID must be between 3 and 50 characters.")]
        public string HFID { get; set; } = string.Empty;

        [Required(ErrorMessage = "Clinic ID is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Clinic ID must be a positive integer.")]
        public int ClinicId { get; set; }

        [StringLength(4000, ErrorMessage = "Medical history cannot exceed 4000 characters.")]
        public string? Medical { get; set; }

        [StringLength(4000, ErrorMessage = "Surgical history cannot exceed 4000 characters.")]
        public string? Surgical { get; set; }

        [StringLength(4000, ErrorMessage = "Drugs information cannot exceed 4000 characters.")]
        public string? Drugs { get; set; }

        [StringLength(4000, ErrorMessage = "Allergies information cannot exceed 4000 characters.")]
        public string? Allergies { get; set; }

        [StringLength(4000, ErrorMessage = "General examination cannot exceed 4000 characters.")]
        public string? GeneralExamination { get; set; }

        [StringLength(4000, ErrorMessage = "Investigations cannot exceed 4000 characters.")]
        public string? Investigations { get; set; }

        [StringLength(4000, ErrorMessage = "Diagnoses cannot exceed 4000 characters.")]
        public string? Diagnoses { get; set; }

        [StringLength(4000, ErrorMessage = "Provisional diagnosis cannot exceed 4000 characters.")]
        public string? ProvisionalDiagnosis { get; set; }

        [StringLength(4000, ErrorMessage = "Notes cannot exceed 4000 characters.")]
        public string? Notes { get; set; }

        [StringLength(4000, ErrorMessage = "Present complaints cannot exceed 4000 characters.")]
        public string? PresentComplaints { get; set; }

        [StringLength(4000, ErrorMessage = "Past history cannot exceed 4000 characters.")]
        public string? PastHistory { get; set; }

        [StringLength(4000, ErrorMessage = "Intensity cannot exceed 4000 characters.")]
        public string? Intensity { get; set; }

        [StringLength(4000, ErrorMessage = "Frequency cannot exceed 4000 characters.")]
        public string? Frequency { get; set; }

        [StringLength(4000, ErrorMessage = "Duration cannot exceed 4000 characters.")]
        public string? Duration { get; set; }

        [StringLength(4000, ErrorMessage = "Nature of Pain cannot exceed 4000 characters.")]
        public string? NatureofPain { get; set; }

        [StringLength(4000, ErrorMessage = "Aggravating Factors cannot exceed 4000 characters.")]
        public string? AggravatingFactors { get; set; }

        [StringLength(4000, ErrorMessage = "Relieving Facors cannot exceed 4000 characters.")]
        public string? RelievingFacors { get; set; }
    }
}
