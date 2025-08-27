using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Users
{
    [Table("user_surgery_details")]
    public class UserSurgeryDetails
    {
        [Key]
        public int user_surgery_id { get; set; }
        public int user_id { get; set; }
        public string? user_surgery_details { get; set; }
        public string? user_surgery_year { get; set; }
        public string? hostname { get; set; }
        public string? drname { get; set; }
    }
}
