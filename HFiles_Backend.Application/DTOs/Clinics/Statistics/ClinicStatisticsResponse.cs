namespace HFiles_Backend.Application.DTOs.Clinics.Statistics
{
	public class EnquiryStats
	{
		public int TotalCount { get; set; }
		public List<MonthlyCount> MonthlyBreakdown { get; set; } = new();
	}
	public class ClinicStatisticsResponse
    {
        public TotalRegistrationsStats TotalRegistrations { get; set; } = new();
        public IncomeStats TotalIncomeGenerated { get; set; } = new();
        public InvoiceStats TotalInvoiceGenerated { get; set; } = new();
        public ReceiptStats TotalReceiptStats { get; set; } = new();
        public List<RevenueByPackage> RevenueByPackage { get; set; } = new();
        public AppointmentStats TotalAppointments { get; set; } = new();
        public GenderDistribution GenderDistribution { get; set; } = new();
        public AgeGroupDistribution AgeGroupDistribution { get; set; } = new();
    }

    public class TotalRegistrationsStats
    {
        public int TotalCount { get; set; }
        public List<MonthlyCount> MonthlyBreakdown { get; set; } = new();
    }

    public class IncomeStats
    {
        public decimal TotalIncome { get; set; }
        public List<MonthlyIncome> MonthlyBreakdown { get; set; } = new();
    }

    public class InvoiceStats
    {
        public int TotalCount { get; set; }
        public List<MonthlyCount> MonthlyBreakdown { get; set; } = new();
    }

    public class ReceiptStats
    {
        public int TotalReceipts { get; set; }
        public decimal TotalAmount { get; set; }

        public int PaidReceipts { get; set; }
        public decimal PaidAmount { get; set; }

        public int UnpaidReceipts { get; set; }
        public decimal UnpaidAmount { get; set; }
    }

    public class RevenueByPackage
    {
        public string PackageName { get; set; } = string.Empty;
        public decimal TotalRevenue { get; set; }
        public int TotalSessions { get; set; }
    }

    public class AppointmentStats
    {
        public int TotalCount { get; set; }
        public List<MonthlyCount> MonthlyBreakdown { get; set; } = new();
    }

    public class GenderDistribution
    {
        public int Male { get; set; }
        public int Female { get; set; }
        public int Others { get; set; }
        public int NotSpecified { get; set; }
    }

    public class AgeGroupDistribution
    {
        public int Age0To25 { get; set; }
        public int Age26To50 { get; set; }
        public int Age51To75 { get; set; }
        public int Age76To100 { get; set; }
        public int AgeNotSpecified { get; set; }
    }

    public class MonthlyCount
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class MonthlyIncome
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; } = string.Empty;
        public decimal Income { get; set; }
    }
}
