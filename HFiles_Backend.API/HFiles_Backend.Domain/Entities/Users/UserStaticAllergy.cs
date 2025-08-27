using HFiles_Backend.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Users;

/// <summary>
/// Represents predefined static allergies mapped via enum.
/// Each allergy is stored row-wise with a flag per user.
/// If AllergyType is Medications and IsAllergic is true, medication names are attached via linked table.
/// </summary>
[Table("userstaticallergies")]
public class UserStaticAllergy
{
    [Key]
    public int Id { get; set; }

    [Required(ErrorMessage = "UserId is required.")]
    [ForeignKey(nameof(User))]
    public int UserId { get; set; }

    [Required(ErrorMessage = "AllergyType is required.")]
    [EnumDataType(typeof(StaticAllergyType), ErrorMessage = "Invalid allergy type.")]
    public StaticAllergyType AllergyType { get; set; }

    [Required]
    public bool IsAllergic { get; set; }

    /// <summary>
    /// Navigation to user.
    /// </summary>
    public User? User { get; set; }

    /// <summary>
    /// Navigation to medication allergies if applicable.
    /// Only populated when AllergyType == Medications and IsAllergic == true.
    /// </summary>
    public List<UserMedicationAllergy>? MedicationAllergies { get; set; }
}
