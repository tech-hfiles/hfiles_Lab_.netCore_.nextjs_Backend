using System.Collections.Generic;
using System.Threading.Tasks;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Entities.Users;
using HFiles_Backend.Domain.Enums;

namespace HFiles_Backend.Domain.Interfaces.Clinics
{
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





	}
}

