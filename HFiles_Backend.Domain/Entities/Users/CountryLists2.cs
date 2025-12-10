using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Users
{
    [Table("countrylist2")]
    public class CountryLists2
    {
        [Key]
        public int id { get; set; }
        public string? countryname { get; set; }
        public string? dialingcode { get; set; }
        public string? isocode { get; set; }
    }
}
