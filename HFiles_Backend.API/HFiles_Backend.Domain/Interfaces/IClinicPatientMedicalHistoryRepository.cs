using HFiles_Backend.Domain.Entities.Clinics;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HFiles_Backend.Domain.Interfaces
{
    public interface IClinicPatientMedicalHistoryRepository
    {
        Task<ClinicPatientMedicalHistory?> GetByClinicPatientIdAsync(int patientId, int clinicId);
        Task<ClinicPatientMedicalHistory?> GetByIdAsync(int id);
        Task<ClinicPatientMedicalHistory> CreateAsync(ClinicPatientMedicalHistory history);
        Task<ClinicPatientMedicalHistory> UpdateAsync(ClinicPatientMedicalHistory history);
        Task<bool> ExistsAsync(int patientId, int clinicId);
        Task SaveChangesAsync();
        Task<ClinicPatientMedicalHistory?> GetByClinicPatientIdDirectAsync(int clinicPatientId, int clinicId);
    }
}
