using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Users;

/// <summary>
/// Stores medication names linked to a user's "Medications" allergy entry.
/// One-to-many relationship from UserStaticAllergy.
/// </summary>
[Table("usermedicationallergies")]
public class UserMedicationAllergy
{
    [Key]
    public int Id { get; set; }

    [Required]
    [ForeignKey(nameof(User))]
    public int UserId { get; set; }

    [Required]
    [ForeignKey(nameof(UserStaticAllergy))]
    public int StaticAllergyId { get; set; }

    /// <summary>
    /// Medication name flagged as allergenic for the user.
    /// </summary>
    [Required(ErrorMessage = "Medication name is required.")]
    [MaxLength(200, ErrorMessage = "Medication name must be 200 characters or less.")]
    public string MedicationName { get; set; } = null!;

    /// <summary>
    /// Navigation back to user.
    /// </summary>
    public User? User { get; set; }

    /// <summary>
    /// Navigation to parent static allergy (must be Medications type).
    /// </summary>
    public UserStaticAllergy? UserStaticAllergy { get; set; }
}
