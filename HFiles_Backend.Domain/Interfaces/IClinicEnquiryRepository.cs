using HFiles_Backend.Domain.Entities.Clinics;

public interface IClinicEnquiryRepository
{
	Task<List<ClinicEnquiry>> GetAllAsync(int clinicId);
	Task<ClinicEnquiry?> GetByIdAsync(int id);
	Task AddAsync(ClinicEnquiry enquiry);
	Task UpdateAsync(ClinicEnquiry enquiry);
	Task<List<ClinicEnquiry>> GetTodayAppointmentsAsync(int clinicId, DateTime today);

}
