using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.Application.DTOs.Labs
{
    public class ProfileUpdate
    {
        [Range(1, int.MaxValue, ErrorMessage = "Lab ID must be greater than zero.")]
        public int Id { get; set; }

        [MaxLength(255, ErrorMessage = "Address must not exceed 255 characters.")]
        public string? Address { get; set; }
    }
}
