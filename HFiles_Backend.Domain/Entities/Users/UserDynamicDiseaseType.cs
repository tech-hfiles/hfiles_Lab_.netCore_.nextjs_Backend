using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Users
{
    [Table("userdynamicdiseasetypes")]
    public class UserDynamicDiseaseType
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string DiseaseName { get; set; } = null!;
    }
}
