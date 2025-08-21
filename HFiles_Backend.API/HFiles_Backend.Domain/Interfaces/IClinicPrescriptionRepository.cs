﻿using HFiles_Backend.Domain.Entities.Clinics;

namespace HFiles_Backend.Domain.Interfaces
{
    public interface IClinicPrescriptionRepository
    {
        Task SavePrescriptionAsync(ClinicPrescription prescription);
        Task<List<ClinicPrescription>> GetPrescriptionsByClinicIdAsync(int clinicId);
        Task<ClinicPrescription?> GetByIdAsync(int prescriptionId);
        Task UpdatePrescriptionAsync(ClinicPrescription prescription);
    }
}
