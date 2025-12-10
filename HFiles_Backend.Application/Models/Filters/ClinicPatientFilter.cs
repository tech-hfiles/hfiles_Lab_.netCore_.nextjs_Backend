namespace HFiles_Backend.Application.Models.Filters
{
    public class ClinicPatientFilter
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public PaymentStatusFilter? PaymentStatus { get; set; }
    }

    public enum PaymentStatusFilter
    {
        All,
        Paid,
        Unpaid
    }
}
