using HFiles_Backend.Domain.Entities.Clinics;

namespace HFiles_Backend.Domain.Interfaces
{
    public interface IAppointmentRepository
    {
        Task SaveAppointmentAsync(ClinicAppointment appointment);
        Task<ClinicAppointment?> GetAppointmentByIdAsync(int id);
        Task<List<ClinicAppointment>> GetAppointmentsByClinicIdAsync(int clinicId);
    }
}
