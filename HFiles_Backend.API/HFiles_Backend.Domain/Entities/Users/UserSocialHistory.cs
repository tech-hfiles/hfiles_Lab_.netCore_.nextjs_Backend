using HFiles_Backend.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Users;

/// <summary>
/// Represents the lifestyle habit profile for a user.
/// Allows partial updates of individual habit fields.
/// </summary>
[Table("usersocialhistories")]
public class UserSocialHistory
{
    /// <summary>
    /// Primary key for social history entry.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key referencing the owning user.
    /// </summary>
    [Required(ErrorMessage = "UserId is required.")]
    [ForeignKey(nameof(User))]
    public int UserId { get; set; }

    /// <summary>
    /// Frequency of smoking habit — nullable to support partial updates.
    /// </summary>
    [EnumDataType(typeof(HabitFrequency), ErrorMessage = "Invalid SmokingFrequency value.")]
    public HabitFrequency? SmokingFrequency { get; set; }

    /// <summary>
    /// Frequency of alcohol consumption — nullable to support partial updates.
    /// </summary>
    [EnumDataType(typeof(HabitFrequency), ErrorMessage = "Invalid AlcoholFrequency value.")]
    public HabitFrequency? AlcoholFrequency { get; set; }

    /// <summary>
    /// Frequency of exercise — nullable to support partial updates.
    /// </summary>
    [EnumDataType(typeof(HabitFrequency), ErrorMessage = "Invalid ExerciseFrequency value.")]
    public HabitFrequency? ExerciseFrequency { get; set; }

    /// <summary>
    /// Frequency of caffeine intake — nullable to support partial updates.
    /// </summary>
    [EnumDataType(typeof(HabitFrequency), ErrorMessage = "Invalid CaffeineFrequency value.")]
    public HabitFrequency? CaffeineFrequency { get; set; }

    /// <summary>
    /// Navigation property for accessing related user entity.
    /// </summary>
    public User? User { get; set; }
}