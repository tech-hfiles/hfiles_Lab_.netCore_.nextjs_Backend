using HFiles_Backend.Domain.Entities.Clinics;

public interface IClinicEnquiryRepository
{
	// Get all enquiries for a clinic - CHANGED to IEnumerable for flexibility
	Task<IEnumerable<ClinicEnquiry>> GetAllAsync(int clinicId);

	// Get a specific enquiry by Id
	Task<ClinicEnquiry?> GetByIdAsync(int id);

	// Add a new enquiry
	Task AddAsync(ClinicEnquiry enquiry);

	// Update an existing enquiry
	Task UpdateAsync(ClinicEnquiry enquiry);

	// Get today's appointments for a clinic
	Task<List<ClinicEnquiry>> GetTodayAppointmentsAsync(int clinicId, DateTime today);

	// Assign a single coach to an enquiry
	Task AssignCoachAsync(int enquiryId, int coachId);

	// Get all coaches assigned to a specific enquiry
	Task<List<ClinicEnquiryCoach>> GetEnquiryCoachesAsync(int enquiryId);

	Task SyncCoachesAsync(int enquiryId, List<int> coachIds);

}