namespace HFiles_Backend.Application.DTOs.Clinics.MedicalHistory
{
    public class DynamicAllergySummaryDto
    {
        /// <summary>
        /// Unique identifier for this dynamic allergy record.
        /// Typically used to track or update specific allergy entries.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The name of the allergen as entered or selected by the user.
        /// This can include foods, medications, environmental triggers, etc.
        /// </summary>
        public string AllergyName { get; set; } = null!;

        /// <summary>
        /// Indicates whether the user is allergic to the specified allergen.
        /// A value of <c>true</c> means allergic; <c>false</c> indicates tolerance.
        /// </summary>
        public bool IsAllergic { get; set; }
    }
}
