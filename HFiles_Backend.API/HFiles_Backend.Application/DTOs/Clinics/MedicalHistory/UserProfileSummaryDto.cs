namespace HFiles_Backend.Application.DTOs.Clinics.MedicalHistory
{
    public class UserProfileSummaryDto
    {
        /// <summary>
        /// Optional URI or file path pointing to the user's profile photo.
        /// Used for visual identification in medical reports or user interfaces.
        /// </summary>
        public string? ProfilePhoto { get; set; }

        /// <summary>
        /// Full legal name of the user.
        /// Displayed prominently in documentation and patient records.
        /// </summary>
        public string FullName { get; set; } = null!;

        /// <summary>
        /// Full legal gender of the user.
        /// Displayed prominently in documentation and patient records.
        /// </summary>
        public string Gender { get; set; } = null!;

        /// <summary>
        /// User’s blood group (e.g., A+, O−, B−).
        /// Important for emergency care and transfusion decisions.
        /// </summary>
        public string? BloodGroup { get; set; }

        /// <summary>
        /// User's height in feet, if available.
        /// Stored separately from inches for flexibility in formatting.
        /// </summary>
        public int? HeightFeet { get; set; }

        /// <summary>
        /// User's height inches component.
        /// Complements <see cref="HeightFeet"/> to form full height measurement.
        /// </summary>
        public int? HeightInches { get; set; }

        /// <summary>
        /// Weight of the user in kilograms.
        /// Used for medical calculations and diagnostics.
        /// </summary>
        public float? WeightKg { get; set; }

        /// <summary>
        /// Health file identifier (HF ID) used to uniquely represent the user's record in the system.
        /// Can be displayed on printed documents or referenced in API calls.
        /// </summary>
        public string? HfId { get; set; }
    }
}
