using System.Collections.Generic;
using System.Threading.Tasks;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Entities.Users;
using HFiles_Backend.Domain.Enums;
using HFiles_Backend.Domain.Entities; // ✅ ADD THIS

namespace HFiles_Backend.Domain.Interfaces.Clinics
{


	public class PatientPackageHistoryDto
	{
		public string UniqueRecordId { get; set; }
		public string PackageName { get; set; }
		public string CoachName { get; set; } // Added this
		public string PatientName { get; set; }
		public string HFID { get; set; }
		public string StartDate { get; set; }
		public string EndDate { get; set; }
		public List<SessionDetail> Sessions { get; set; } = new List<SessionDetail>();
	}

	public class SessionDetail
	{
		public string Date { get; set; }
		public string Time { get; set; }
		public int Status { get; set; }
		public int AppointmentId { get; set; }
	}

	public class PackageAppointmentDetailDto
	{
		public int AppointmentId { get; set; }
		public string PackageName { get; set; }
		public DateTime PackageDate { get; set; }
		public TimeSpan PackageTime { get; set; }
		public int Status { get; set; }
		public string UniqueRecordId { get; set; }
		public string PatientName { get; set; }
		// Add any other fields from the JSON if needed
	}
	public interface IClinicHigh5AppointmentService
	{
		// ================= Create =================
		Task<High5Appointment> CreateAppointmentAsync(High5Appointment appointment);

		// ================= Read =================
		Task<High5Appointment?> GetAppointmentByIdAsync(int appointmentId);

		Task<IEnumerable<High5Appointment>> GetAppointmentsByClinicIdAsync(int clinicId);

		Task<IEnumerable<High5Appointment>> GetAppointmentsByUserIdAsync(int userId);

		Task<IEnumerable<High5Appointment>> GetAllAppointmentsAsync();

		// ================= Update =================
		Task<bool> UpdateAppointmentAsync(High5Appointment appointment);

		Task<List<High5Appointment>> GetAppointmentsByClinicIdWithUserAsync(int clinicId);
		Task<List<DailyCountDto>> GetDailyAppointmentCountsAsync(
	int clinicId,
	DateTime startDate,
	DateTime endDate);
		Task<bool> IsDuplicateAppointmentAsync(int excludeId, int userId, int? packageId, DateTime date);

		Task<List<PackageAppointmentDetailDto>> GetAppointmentsByRecordIdAsync(string uniqueRecordId);

		Task<List<PatientPackageHistoryDto>> GetMEPPackagesByPatientAsync(int patientId);
	}
}

