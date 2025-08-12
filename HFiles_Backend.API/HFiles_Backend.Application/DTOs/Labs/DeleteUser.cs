using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.Application.DTOs.Labs
{
    public class DeleteUser
    {
        [Required(ErrorMessage = "User ID is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "User ID must be greater than zero.")]
        public int Id { get; set; }

        [Required(ErrorMessage = "Lab ID is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Lab ID must be greater than zero.")]
        public int LabId { get; set; }
    }

}
