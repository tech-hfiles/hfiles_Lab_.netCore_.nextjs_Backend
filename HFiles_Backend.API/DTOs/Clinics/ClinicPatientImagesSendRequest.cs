namespace HFiles_Backend.API.DTOs.Clinics
{
    public class ClinicPatientImagesSendRequest
    {
        public int ClinicId { get; set; }
        public int PatientId { get; set; }
        public int ClinicVisitId { get; set; }

        public List<ImageSendItem> Images { get; set; } = new();
    }
    public class ImageSendItem
    {
        public string ImageUrl { get; set; } = string.Empty;   // required
        public bool SendToPatient { get; set; }                 // 0 = don't send, 1 = send to patient
                                                                // Optional: public string? Title { get; set; }       // if you want custom name
                                                                // Optional: public string? ReferenceId { get; set; }
    }

}
