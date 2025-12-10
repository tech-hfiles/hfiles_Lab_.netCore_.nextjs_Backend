using HFiles_Backend.Domain.Enums;

namespace HFiles_Backend.Application.DTOs.Clinics.MedicalHistory
{
    public class UserStaticAllergySummaryDto
    {
        /// <summary>
        /// Unique identifier for this specific allergy response record.
        /// Can be used for querying, updating, or mapping to user profiles.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The static allergy type being evaluated (e.g., Dairy, Nuts, Pollen).
        /// This value is drawn from the <see cref="StaticAllergyType"/> enumeration.
        /// </summary>
        public StaticAllergyType AllergyType { get; set; }

        /// <summary>
        /// Indicates whether the user is allergic to the specified allergy type.
        /// <c>true</c> means the user has a confirmed allergy; <c>false</c> means tolerant or unresponsive.
        /// </summary>
        public bool IsAllergic { get; set; }
    }
}
