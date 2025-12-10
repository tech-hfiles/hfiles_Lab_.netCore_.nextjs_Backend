using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.API.DTOs.Labs
{
    public class ResendReport
    {
        [Required(ErrorMessage = "At least one report ID must be provided.")]
        [MinLength(1, ErrorMessage = "The list must contain at least one ID.")]
        public List<int> Ids { get; set; } = [];
    }
}
