using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HFiles_Backend.Domain.Entities.Clinics;
using Microsoft.EntityFrameworkCore.Storage;

namespace HFiles_Backend.Domain.Interfaces
{
    public interface IClinicDocumentRepository  // Changed from 'class' to 'interface'
    {
        Task<ClinicSignup?> GetClinicByIdAsync(int clinicId);
        Task<ClinicPatient?> GetPatientByIdAsync(int patientId);
        Task<bool> IsPatientBelongsToClinicAsync(int patientId, int clinicId);
        Task<ClinicDocument_Storage> SaveDocumentAsync(ClinicDocument_Storage document);
        Task<List<ClinicDocument_Storage>> GetPatientDocumentsAsync(int patientId, int clinicId);
        Task<bool> SoftDeleteDocumentAsync(int documentId, int clinicId, int patientId);
        Task<IDbContextTransaction> BeginTransactionAsync();
        Task<ClinicDocument_Storage?> GetDocumentByIdAsync(int documentId, int clinicId, int patientId);
        Task<bool> UpdateDocumentFileNameAsync(int documentId, int clinicId, int patientId, string newFileName);
        Task SaveChangesAsync();
        Task<ClinicVisit?> GetOrCreateVisitForTodayAsync(int clinicId, int patientId);
        Task SaveClinicPatientRecordAsync(ClinicPatientRecord record);
    }
}