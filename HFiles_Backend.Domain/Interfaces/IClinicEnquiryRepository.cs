using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Enums;
public class DailyCountDto
{
	public DateTime Date { get; set; }
	public int Count { get; set; }
}

public interface IClinicEnquiryRepository
{
	// Get all enquiries for a clinic - CHANGED to IEnumerable for flexibility
	Task<IEnumerable<ClinicEnquiry>> GetAllAsync(int clinicId);

	Task<List<DailyCountDto>> GetDailyEnquiryCountsAsync(
		int clinicId,
		DateTime startDate,
		DateTime endDate);
	// Get a specific enquiry by Id
	Task<ClinicEnquiry?> GetByIdAsync(int id);

	// Add a new enquiry
	Task AddAsync(ClinicEnquiry enquiry);

	// Update an existing enquiry
	Task UpdateAsync(ClinicEnquiry enquiry);

	// Get today's appointments for a clinic
	Task<List<ClinicEnquiry>> GetTodayAppointmentsAsync(int clinicId, DateTime today);
	// ✅ NEW - Get filtered and paginated enquiries (optimized)
	Task<(List<ClinicEnquiry> enquiries, int totalCount)> GetFilteredEnquiriesAsync(
		int clinicId,
		List<EnquiryStatus>? status,
		PaymentStatus? paymentStatus,
		int? coachId,
		string? search,
		DateTime? startDate,
		DateTime? endDate,
		int page,
		int pageSize);

	// Assign a single coach to an enquiry
	Task AssignCoachAsync(int enquiryId, int coachId);

	// Get all coaches assigned to a specific enquiry
	Task<List<ClinicEnquiryCoach>> GetEnquiryCoachesAsync(int enquiryId);

	Task SyncCoachesAsync(int enquiryId, List<int> coachIds);

}