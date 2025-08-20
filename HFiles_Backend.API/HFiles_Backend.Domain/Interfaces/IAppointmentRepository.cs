﻿using HFiles_Backend.Domain.Entities.Clinics;

namespace HFiles_Backend.Domain.Interfaces
{
    public interface IAppointmentRepository
    {
        Task SaveAppointmentAsync(ClinicAppointment appointment);
        Task<ClinicAppointment?> GetAppointmentByIdAsync(int appointmentId, int clinicId);
        Task<List<ClinicAppointment>> GetAppointmentsByClinicIdAsync(int clinicId);
        Task<ClinicAppointment?> GetByIdAsync(int appointmentId);
        Task DeleteAsync(ClinicAppointment appointment);
        Task<int> MarkOverdueAppointmentsAsAbsentAsync();
    }
}
