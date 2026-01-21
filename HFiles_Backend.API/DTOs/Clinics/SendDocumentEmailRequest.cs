using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.API.DTOs.Clinics
{
    public class SendDocumentEmailRequest
    {
        [Required(ErrorMessage = "At least one document ID is required.")]
        public List<int> DocumentIds { get; set; } = new List<int>();

        public bool SaveToHfAccount { get; set; } = false;
        public string? CustomMessage { get; set; }
    }
}
