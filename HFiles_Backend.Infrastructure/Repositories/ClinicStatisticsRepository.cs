using HFiles_Backend.Application.DTOs.Clinics.Statistics;
using HFiles_Backend.Domain.Enums;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Globalization;

namespace HFiles_Backend.Infrastructure.Repositories
{
    public class ClinicStatisticsRepository(AppDbContext context, ILogger<ClinicStatisticsRepository> logger) : IClinicStatisticsRepository
    {
        private readonly AppDbContext _context = context;
        private readonly ILogger<ClinicStatisticsRepository> _logger = logger;


		// Add this new method to ClinicStatisticsRepository
		public async Task<object> GetHigh5StatisticsAsync(
	int clinicId,
	DateTime? startDate = null,
	DateTime? endDate = null)
		{
			try
			{
				var registrations = await GetRegistrationStatsAsync(clinicId, startDate, endDate);
				var enquiries = await GetEnquiryStatsAsync(clinicId, startDate, endDate);

				return new
				{
					TotalRegistrations = registrations,
					TotalEnquiries = enquiries
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error fetching High5 statistics for Clinic ID {ClinicId}", clinicId);
				throw;
			}
		}



		public async Task<EnquiryStats> GetEnquiryStatsAsync(
	int clinicId,
	DateTime? startDate,
	DateTime? endDate)
		{
			try
			{
				// Get all enquiries with their epoch times
				var enquiriesWithDates = await _context.clinicEnquiry
					.Where(e => e.ClinicId == clinicId)
					.Select(e => new { e.Id, e.EpochTime })
					.ToListAsync();

				// Convert epoch to dates and filter
				var filteredEnquiries = enquiriesWithDates
					.Select(e => new
					{
						e.Id,
						Date = DateTimeOffset.FromUnixTimeSeconds(e.EpochTime).DateTime
					})
					.Where(e =>
					{
						return (!startDate.HasValue || e.Date.Date >= startDate.Value.Date) &&
							   (!endDate.HasValue || e.Date.Date <= endDate.Value.Date);
					})
					.ToList();

				// Group by month
				var monthlyData = filteredEnquiries
					.GroupBy(e => new { e.Date.Year, e.Date.Month })
					.Select(g => new MonthlyCount
					{
						Year = g.Key.Year,
						Month = g.Key.Month,
						MonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(g.Key.Month),
						Count = g.Count()
					})
					.OrderBy(m => m.Year)
					.ThenBy(m => m.Month)
					.ToList();

				return new EnquiryStats
				{
					TotalCount = filteredEnquiries.Count,
					MonthlyBreakdown = monthlyData
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error fetching enquiry statistics for Clinic ID {ClinicId}", clinicId);
				throw;
			}
		}








		public async Task<ClinicStatisticsResponse> GetClinicStatisticsAsync(
          int clinicId,
          DateTime? startDate = null,
          DateTime? endDate = null)
        {
            try
            {
                // Execute tasks SEQUENTIALLY to avoid DbContext concurrency issues
                var registrations = await GetRegistrationStatsAsync(clinicId, startDate, endDate);
                var income = await GetIncomeStatsAsync(clinicId, startDate, endDate);
                var invoices = await GetInvoiceStatsAsync(clinicId, startDate, endDate);
                var receipts = await GetReceiptStatsAsync(clinicId);
                var revenue = await GetRevenueByPackageAsync(clinicId);
                var appointments = await GetAppointmentStatsAsync(clinicId, startDate, endDate);
                var gender = await GetGenderDistributionAsync(clinicId);
                var ageGroup = await GetAgeGroupDistributionAsync(clinicId);

                return new ClinicStatisticsResponse
                {
                    TotalRegistrations = registrations,
                    TotalIncomeGenerated = income,
                    TotalInvoiceGenerated = invoices,
                    TotalReceiptStats = receipts,
                    RevenueByPackage = revenue,
                    TotalAppointments = appointments,
                    GenderDistribution = gender,
                    AgeGroupDistribution = ageGroup
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching clinic statistics for Clinic ID {ClinicId}", clinicId);
                throw;
            }
        }


        public async Task<TotalRegistrationsStats> GetRegistrationStatsAsync(
            int clinicId,
            DateTime? startDate,
            DateTime? endDate)
        {
            var query = _context.ClinicVisits
                .Where(v => v.ClinicId == clinicId);

            if (startDate.HasValue)
                query = query.Where(v => v.AppointmentDate >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(v => v.AppointmentDate <= endDate.Value);

            var visits = await query
                .Select(v => new { v.ClinicPatientId, v.AppointmentDate })
                .ToListAsync();

            // Get unique patients
            var uniquePatients = visits.Select(v => v.ClinicPatientId).Distinct().Count();

            // Monthly breakdown
            var monthlyData = visits
                .GroupBy(v => new { v.AppointmentDate.Year, v.AppointmentDate.Month })
                .Select(g => new MonthlyCount
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    MonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(g.Key.Month),
                    Count = g.Select(x => x.ClinicPatientId).Distinct().Count()
                })
                .OrderBy(m => m.Year)
                .ThenBy(m => m.Month)
                .ToList();

            return new TotalRegistrationsStats
            {
                TotalCount = uniquePatients,
                MonthlyBreakdown = monthlyData
            };
        }

        public async Task<IncomeStats> GetIncomeStatsAsync(
      int clinicId,
      DateTime? startDate,
      DateTime? endDate)
        {
            var receiptsQuery = _context.ClinicPatientRecords
                .Where(r => r.ClinicId == clinicId && r.Type == RecordType.Receipt)
                .Include(r => r.Visit);

            var receipts = await receiptsQuery.ToListAsync();

            // Filter by date if provided
            if (startDate.HasValue || endDate.HasValue)
            {
                receipts = receipts.Where(r =>
                {
                    var visitDate = r.Visit.AppointmentDate;
                    return (!startDate.HasValue || visitDate >= startDate.Value) &&
                           (!endDate.HasValue || visitDate <= endDate.Value);
                }).ToList();
            }

            decimal totalIncome = 0;
            var monthlyIncomes = new Dictionary<(int Year, int Month), decimal>();

            foreach (var receipt in receipts)
            {
                try
                {
                    // FIRST: Check if this receipt is paid
                    bool isPaid = receipt.Visit.PaymentMethod.HasValue &&
                                 receipt.Visit.PaymentMethod.Value != PaymentMethod.Pending;

                    // ONLY process paid receipts for income calculation
                    if (!isPaid)
                    {
                        continue; // Skip unpaid receipts
                    }

                    var jsonData = JObject.Parse(receipt.JsonData);
                    var amountPaid = jsonData["receipt"]?["amountPaid"]?.Value<decimal>() ?? 0;

                    // Add to total income
                    totalIncome += amountPaid;

                    // Add to monthly breakdown
                    var visitDate = receipt.Visit.AppointmentDate;
                    var key = (visitDate.Year, visitDate.Month);

                    if (!monthlyIncomes.ContainsKey(key))
                        monthlyIncomes[key] = 0;

                    monthlyIncomes[key] += amountPaid;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing receipt JSON for record ID {RecordId}", receipt.Id);
                }
            }

            var monthlyBreakdown = monthlyIncomes
                .Select(kvp => new MonthlyIncome
                {
                    Year = kvp.Key.Year,
                    Month = kvp.Key.Month,
                    MonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(kvp.Key.Month),
                    Income = kvp.Value
                })
                .OrderBy(m => m.Year)
                .ThenBy(m => m.Month)
                .ToList();

            return new IncomeStats
            {
                TotalIncome = totalIncome,
                MonthlyBreakdown = monthlyBreakdown
            };
        }


        public async Task<InvoiceStats> GetInvoiceStatsAsync(
            int clinicId,
            DateTime? startDate,
            DateTime? endDate)
        {
            var invoicesQuery = _context.ClinicPatientRecords
                .Where(r => r.ClinicId == clinicId && r.Type == RecordType.Invoice)
                .Include(r => r.Visit);

            var invoices = await invoicesQuery.ToListAsync();

            // Filter by date if provided
            if (startDate.HasValue || endDate.HasValue)
            {
                invoices = invoices.Where(r =>
                {
                    var visitDate = r.Visit.AppointmentDate;
                    return (!startDate.HasValue || visitDate >= startDate.Value) &&
                           (!endDate.HasValue || visitDate <= endDate.Value);
                }).ToList();
            }

            var monthlyData = invoices
                .GroupBy(i => new { i.Visit.AppointmentDate.Year, i.Visit.AppointmentDate.Month })
                .Select(g => new MonthlyCount
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    MonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(g.Key.Month),
                    Count = g.Count()
                })
                .OrderBy(m => m.Year)
                .ThenBy(m => m.Month)
                .ToList();

            return new InvoiceStats
            {
                TotalCount = invoices.Count,
                MonthlyBreakdown = monthlyData
            };
        }

        public async Task<ReceiptStats> GetReceiptStatsAsync(int clinicId)
        {
            var receipts = await _context.ClinicPatientRecords
                .Where(r => r.ClinicId == clinicId && r.Type == RecordType.Receipt)
                .Include(r => r.Visit)
                .ToListAsync();

            var totalReceipts = receipts.Count;
            decimal totalAmount = 0;
            decimal paidAmount = 0;
            decimal unpaidAmount = 0;
            int paidCount = 0;
            int unpaidCount = 0;

            foreach (var receipt in receipts)
            {
                try
                {
                    var jsonData = JObject.Parse(receipt.JsonData);
                    var amountPaid = jsonData["receipt"]?["amountPaid"]?.Value<decimal>() ?? 0;

                    // Add to total amount
                    totalAmount += amountPaid;

                    // Check if receipt is paid or unpaid based on visit's payment method
                    bool isPaid = receipt.Visit.PaymentMethod.HasValue &&
                                 receipt.Visit.PaymentMethod.Value != PaymentMethod.Pending;

                    if (isPaid)
                    {
                        paidCount++;
                        paidAmount += amountPaid;
                    }
                    else
                    {
                        unpaidCount++;
                        unpaidAmount += amountPaid;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing receipt JSON for record ID {RecordId}", receipt.Id);
                }
            }

            return new ReceiptStats
            {
                TotalReceipts = totalReceipts,
                TotalAmount = totalAmount,
                PaidReceipts = paidCount,
                PaidAmount = paidAmount,
                UnpaidReceipts = unpaidCount,
                UnpaidAmount = unpaidAmount
            };
        }

        public async Task<List<RevenueByPackage>> GetRevenueByPackageAsync(int clinicId)
        {
            // Get all treatments for this clinic
            var treatments = await _context.ClinicTreatments
                .Where(t => t.ClinicId == clinicId)
                .ToListAsync();

            // Get all treatment records
            var treatmentRecords = await _context.ClinicPatientRecords
                .Where(r => r.ClinicId == clinicId && r.Type == RecordType.Treatment)
                .ToListAsync();

            var revenueByPackage = new Dictionary<string, (decimal Revenue, int Sessions)>();

            foreach (var record in treatmentRecords)
            {
                try
                {
                    var jsonData = JObject.Parse(record.JsonData);
                    var treatmentsArray = jsonData["treatments"] as JArray;

                    if (treatmentsArray != null)
                    {
                        foreach (var treatment in treatmentsArray)
                        {
                            var name = treatment["name"]?.Value<string>() ?? "Unknown";
                            var total = treatment["total"]?.Value<decimal>() ?? 0;

                            if (!revenueByPackage.ContainsKey(name))
                                revenueByPackage[name] = (0, 0);

                            var current = revenueByPackage[name];
                            revenueByPackage[name] = (current.Revenue + total, current.Sessions + 1);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing treatment JSON for record ID {RecordId}", record.Id);
                }
            }

            return revenueByPackage
                .Select(kvp => new RevenueByPackage
                {
                    PackageName = kvp.Key,
                    TotalRevenue = kvp.Value.Revenue,
                    TotalSessions = kvp.Value.Sessions
                })
                .OrderByDescending(r => r.TotalRevenue)
                .ToList();
        }

        public async Task<AppointmentStats> GetAppointmentStatsAsync(
            int clinicId,
            DateTime? startDate,
            DateTime? endDate)
        {
            var query = _context.ClinicAppointments
                .Where(a => a.ClinicId == clinicId);

            if (startDate.HasValue)
                query = query.Where(a => a.AppointmentDate >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(a => a.AppointmentDate <= endDate.Value);

            var appointments = await query
                .Select(a => new { a.AppointmentDate })
                .ToListAsync();

            var monthlyData = appointments
                .GroupBy(a => new { a.AppointmentDate.Year, a.AppointmentDate.Month })
                .Select(g => new MonthlyCount
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    MonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(g.Key.Month),
                    Count = g.Count()
                })
                .OrderBy(m => m.Year)
                .ThenBy(m => m.Month)
                .ToList();

            return new AppointmentStats
            {
                TotalCount = appointments.Count,
                MonthlyBreakdown = monthlyData
            };
        }

        public async Task<GenderDistribution> GetGenderDistributionAsync(int clinicId)
        {
            // Get all unique patient HFIDs for this clinic
            var patientHfids = await _context.ClinicVisits
                .Where(v => v.ClinicId == clinicId)
                .Include(v => v.Patient)
                .Select(v => v.Patient.HFID)
                .Distinct()
                .ToListAsync();

            // Get user details for these HFIDs
            var users = await _context.Users
                .Where(u => patientHfids.Contains(u.HfId) && u.DeletedBy == 0)
                .Select(u => u.Gender)
                .ToListAsync();

            var distribution = new GenderDistribution
            {
                Male = users.Count(g => g != null && g.Equals("Male", StringComparison.OrdinalIgnoreCase)),
                Female = users.Count(g => g != null && g.Equals("Female", StringComparison.OrdinalIgnoreCase)),
                Others = users.Count(g => g != null &&
                    !g.Equals("Male", StringComparison.OrdinalIgnoreCase) &&
                    !g.Equals("Female", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(g)),
                NotSpecified = users.Count(g => string.IsNullOrWhiteSpace(g))
            };

            return distribution;
        }

        public async Task<AgeGroupDistribution> GetAgeGroupDistributionAsync(int clinicId)
        {
            // Get all unique patient HFIDs for this clinic
            var patientHfids = await _context.ClinicVisits
                .Where(v => v.ClinicId == clinicId)
                .Include(v => v.Patient)
                .Select(v => v.Patient.HFID)
                .Distinct()
                .ToListAsync();

            // Get user details for these HFIDs
            var users = await _context.Users
                .Where(u => patientHfids.Contains(u.HfId) && u.DeletedBy == 0)
                .Select(u => u.DOB)
                .ToListAsync();

            var distribution = new AgeGroupDistribution();

            foreach (var dob in users)
            {
                if (string.IsNullOrWhiteSpace(dob))
                {
                    distribution.AgeNotSpecified++;
                    continue;
                }

                try
                {
                    // Parse DOB in dd-MM-yyyy format
                    if (DateTime.TryParseExact(dob, "dd-MM-yyyy", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var birthDate))
                    {
                        var age = CalculateAge(birthDate);

                        if (age >= 0 && age <= 25)
                            distribution.Age0To25++;
                        else if (age >= 26 && age <= 50)
                            distribution.Age26To50++;
                        else if (age >= 51 && age <= 75)
                            distribution.Age51To75++;
                        else if (age >= 76 && age <= 100)
                            distribution.Age76To100++;
                        else
                            distribution.AgeNotSpecified++;
                    }
                    else
                    {
                        distribution.AgeNotSpecified++;
                    }
                }
                catch
                {
                    distribution.AgeNotSpecified++;
                }
            }

            return distribution;
        }

        private static int CalculateAge(DateTime birthDate)
        {
            var today = DateTime.Today;
            var age = today.Year - birthDate.Year;

            if (birthDate.Date > today.AddYears(-age))
                age--;

            return age;
        }
    }
}
