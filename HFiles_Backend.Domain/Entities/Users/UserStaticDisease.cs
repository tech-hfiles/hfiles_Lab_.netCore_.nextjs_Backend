using HFiles_Backend.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Users
{
    [Table("userstaticdiseases")]
    public class UserStaticDisease
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey(nameof(User))]
        public int UserId { get; set; }

        [Required]
        public StaticDiseaseType DiseaseType { get; set; }

        public bool Myself { get; set; }
        public bool MotherSide { get; set; }
        public bool FatherSide { get; set; }

        public User? User { get; set; }
    }

}

