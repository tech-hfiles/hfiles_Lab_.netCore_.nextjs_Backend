namespace HFiles_Backend.Application.DTOs.Clinics.Treatment
{
    public class TreatmentRecordPayload
    {
        public List<TreatmentDetail> Treatments { get; set; } = new();
    }

    public class TreatmentDetail
    {
        public string Name { get; set; } = string.Empty;
    }
}
