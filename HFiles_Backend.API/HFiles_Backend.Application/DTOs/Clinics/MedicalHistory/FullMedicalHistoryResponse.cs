namespace HFiles_Backend.Application.DTOs.Clinics.MedicalHistory
{
    public class FullMedicalHistoryResponse
    {
        /// <summary>
        /// The user whose medical records are represented.
        /// </summary>
        public int PatientId { get; set; }

        /// <summary>
        /// Summary of lifestyle and behavioral habits (smoking, caffeine, alcohol, exercise).
        /// </summary>
        public UserSocialHistoryDto? SocialHistory { get; set; }

        /// <summary>
        /// Key health attributes of the user for display.
        /// Includes height, weight, blood group, HFID, and profile photo.
        /// </summary>
        public UserProfileSummaryDto? UserProfileSummary { get; set; } // 🆕

        /// <summary>
        /// Summary of surgery history entries.
        /// </summary>
        public List<UserSurgeryDetailsDto> Surgeries { get; set; } = new();

        /// <summary>
        /// System-defined static allergy records with IsAllergic flag.
        /// All types always returned.
        /// </summary>
        public List<UserStaticAllergySummaryDto> StaticAllergies { get; set; } = new();

        /// <summary>
        /// Custom dynamic allergies defined by user.
        /// </summary>
        public List<DynamicAllergySummaryDto> DynamicAllergies { get; set; } = new();

        /// <summary>
        /// Medication-specific allergy entries.
        /// </summary>
        public List<UserMedicationAllergyDto> MedicationAllergies { get; set; } = new();

        /// <summary>
        /// System-defined disease flags (e.g., Diabetes) including inheritance indicators.
        /// </summary>
        public List<UserStaticDiseaseDto> StaticDiseases { get; set; } = new();

        /// <summary>
        /// Custom-defined disease records with inheritance flags.
        /// </summary>
        public List<DynamicDiseaseRecordDto> DynamicDiseases { get; set; } = new();
    }
}
