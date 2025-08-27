using HFiles_Backend.Domain.Enums;

namespace HFiles_Backend.Application.DTOs.Clinics.MedicalHistory
{
    public class UserStaticDiseaseDto
    {
        /// <summary>
        /// Unique identifier for the static disease record.
        /// Typically used for updates, deletions, or reference linking.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The enumerated type of disease being tracked.
        /// Pulled from the <see cref="StaticDiseaseType"/> predefined disease set.
        /// Examples: Hypertension, Diabetes, Heart Disease, etc.
        /// </summary>
        public StaticDiseaseType DiseaseType { get; set; }

        /// <summary>
        /// Indicates whether the user personally has this disease.
        /// Used for diagnostic relevance and health risk tracking.
        /// </summary>
        public bool Myself { get; set; }

        /// <summary>
        /// Indicates whether the disease exists in the user's maternal lineage (mother's side).
        /// Useful for evaluating genetic predisposition from that side of the family.
        /// </summary>
        public bool MotherSide { get; set; }

        /// <summary>
        /// Indicates whether the disease exists in the user's paternal lineage (father's side).
        /// Helps assess family history patterns and inheritance risks.
        /// </summary>
        public bool FatherSide { get; set; }
    }
}
