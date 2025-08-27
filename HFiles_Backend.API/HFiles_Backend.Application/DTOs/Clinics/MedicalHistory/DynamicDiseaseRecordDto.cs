namespace HFiles_Backend.Application.DTOs.Clinics.MedicalHistory
{
    public class DynamicDiseaseRecordDto
    {
        /// <summary>
        /// Unique identifier for this specific dynamic disease record.
        /// Used for querying, updating, or deleting disease history entries.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Identifier for the dynamic disease type.
        /// This links to a custom disease defined in the system.
        /// </summary>
        public int DiseaseTypeId { get; set; }

        /// <summary>
        /// Display name of the dynamic disease.
        /// This can include user-defined or organization-defined terminology.
        /// </summary>
        public string DiseaseName { get; set; } = null!;

        /// <summary>
        /// Indicates whether the user personally suffers from this disease.
        /// Useful for flagging conditions directly impacting treatment plans.
        /// </summary>
        public bool Myself { get; set; }

        /// <summary>
        /// Indicates whether the disease is present in the user's maternal lineage.
        /// Useful for identifying inherited or genetic risk factors.
        /// </summary>
        public bool MotherSide { get; set; }

        /// <summary>
        /// Indicates whether the disease is present in the user's paternal lineage.
        /// Helps trace disease patterns across generations.
        /// </summary>
        public bool FatherSide { get; set; }
    }
}
