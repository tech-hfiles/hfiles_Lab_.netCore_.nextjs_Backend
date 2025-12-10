namespace HFiles_Backend.Application.DTOs.Clinics.MedicalHistory
{
    public class UserSurgeryDetailsDto
    {
        /// <summary>
        /// Unique identifier for the surgical entry.
        /// Typically used for referencing or updating specific surgery records.
        /// </summary>
        public int SurgeryId { get; set; }

        /// <summary>
        /// Description or label of the surgical procedure performed.
        /// Example: "Appendectomy", "Knee Replacement", "Gallbladder Removal".
        /// </summary>
        public string? Details { get; set; }

        /// <summary>
        /// Year in which the surgery was performed.
        /// Often used for chronological tracking and audit history.
        /// Stored as string to support partial or flexible formats (e.g., "2020", "Fall 2018").
        /// </summary>
        public string? Year { get; set; }

        /// <summary>
        /// Name of the hospital or medical facility where the surgery took place.
        /// Useful for institutional tracking, referrals, or contextual records.
        /// </summary>
        public string? Hospital { get; set; }

        /// <summary>
        /// Full name of the doctor who performed or supervised the surgery.
        /// Useful for personal reference, legal tracing, or clinician coordination.
        /// </summary>
        public string? DoctorName { get; set; }
    }
}
