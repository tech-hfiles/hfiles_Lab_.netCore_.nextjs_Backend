using HFiles_Backend.Domain.Enums;

namespace HFiles_Backend.Application.DTOs.Clinics.MedicalHistory
{
    public class UserSocialHistoryDto
    {
        /// <summary>
        /// Unique identifier for the user's social history record.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Frequency of smoking behavior.
        /// Used for assessing respiratory risk or chronic illness factors.
        /// </summary>
        public HabitFrequency? SmokingFrequency { get; set; }

        /// <summary>
        /// Frequency of alcohol consumption.
        /// Supports evaluation of liver health, lifestyle risk factors, and addiction screening.
        /// </summary>
        public HabitFrequency? AlcoholFrequency { get; set; }

        /// <summary>
        /// Frequency of caffeine intake (e.g., coffee, energy drinks).
        /// May be relevant for cardiovascular screening and sleep disorder diagnostics.
        /// </summary>
        public HabitFrequency? CaffeineFrequency { get; set; }

        /// <summary>
        /// Frequency of physical exercise or activity.
        /// Contributes to evaluations of cardiovascular fitness and metabolic health.
        /// </summary>
        public HabitFrequency? ExerciseFrequency { get; set; }
    }
}
