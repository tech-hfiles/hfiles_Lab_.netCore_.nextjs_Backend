namespace HFiles_Backend.Application.DTOs.Clinics.MedicalHistory
{
    public class UserMedicationAllergyDto
    {
        /// <summary>
        /// Unique identifier for this medication allergy record.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Identifier of the static allergy category this medication maps to.
        /// Used to classify the medication under general allergy types (e.g. Penicillin-based).
        /// </summary>
        public int StaticAllergyId { get; set; }

        /// <summary>
        /// The name of the medication that triggered or is associated with the allergic reaction.
        /// This value is required and typically user-defined or system-sourced.
        /// </summary>
        public string MedicationName { get; set; } = null!;
    }
}
