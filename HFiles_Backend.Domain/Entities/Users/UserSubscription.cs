using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HFiles_Backend.Domain.Entities.Users
{
    [Table("usersubscriptions")]
    public class UserSubscription
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User Users { get; set; } = null!;

        [Required]
        [StringLength(50, ErrorMessage = "Subscription plan name cannot exceed 50 characters.")]
        public string? SubscriptionPlan { get; set; }

        [Required]
        public long StartEpoch { get; set; }

        [Required]
        public long EndEpoch { get; set; }
    }
}
