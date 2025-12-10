using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Users;

/// <summary>
/// Stores user-defined allergy types that are not represented in StaticAllergyType enum.
/// Supports flexible allergy tracking without schema changes.
/// </summary>
[Table("userdynamicallergies")]
public class UserDynamicAllergy
{
    [Key]
    public int Id { get; set; }

    [Required(ErrorMessage = "UserId is required.")]
    [ForeignKey(nameof(User))]
    public int UserId { get; set; }

    [Required(ErrorMessage = "Allergy name is required.")]
    [MaxLength(100)]
    public string AllergyName { get; set; } = null!;

    [Required]
    public bool IsAllergic { get; set; }

    public User? User { get; set; }
}
