using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Labs
{
    [Table("labmembers")]
    public class LabMember
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int LabId { get; set; }
        public string Role { get; set; } = "Member";
        public string? PasswordHash { get; set; }
        public int CreatedBy { get; set; }
        public int PromotedBy { get; set; } = 0;
        public int DeletedBy { get; set; } = 0;
        public long EpochTime { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
