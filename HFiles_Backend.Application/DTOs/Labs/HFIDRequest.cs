using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.Application.DTOs.Labs
{
    public class HFIDRequest
    {
        [Required(ErrorMessage = "HFID is required.")]
        [MaxLength(20, ErrorMessage = "HFID must not exceed 20 characters.")]
        public string HFID { get; set; } = null!;
    }
}
