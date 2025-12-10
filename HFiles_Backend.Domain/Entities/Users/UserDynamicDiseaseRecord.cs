using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Users
{
    [Table("userdynamicdiseaserecords")]
    public class UserDynamicDiseaseRecord
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey(nameof(User))]
        public int UserId { get; set; }

        [Required]
        [ForeignKey(nameof(UserDynamicDiseaseType))]
        public int DiseaseTypeId { get; set; }

        public bool Myself { get; set; }
        public bool MotherSide { get; set; }
        public bool FatherSide { get; set; }

        public User? User { get; set; }
        public UserDynamicDiseaseType? DiseaseType { get; set; }
    }

}
