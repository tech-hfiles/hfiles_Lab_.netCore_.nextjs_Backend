using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Enums;

namespace HFiles_Backend.Domain.Interfaces
{
    public interface IClinicPatientRecordRepository
    {
        Task SaveAsync(ClinicPatientRecord record);
        Task<List<ClinicPatientRecord>> GetByClinicAndPatientAsync(int clinicId, int patientId);
        Task<List<ClinicPatientRecord>> GetByClinicPatientVisitAsync(int clinicId, int patientId, int clinicVisitId);
        Task<ClinicPatientRecord?> GetReportImageRecordAsync(int clinicId, int patientId, int visitId);
        Task UpdateAsync(ClinicPatientRecord record);
        Task<ClinicPatientRecord?> GetByCompositeKeyAsync(int clinicId, int patientId, int visitId, RecordType type);
        Task<ClinicPatient?> GetByIdAsync(int patientId);
        Task<List<ClinicPatientRecord>> GetTreatmentRecordsByClinicIdAsync(int clinicId);
        Task<List<ClinicPatientRecord>> GetPrescriptionRecordsByClinicIdAsync(int clinicId);
        Task<bool> PrescriptionExistsForVisitAsync(int clinicId, int patientId, int visitId);
        Task<List<ClinicPatientRecord>> GetUnsentTreatmentRecordsAsync(int clinicId);
        Task<List<ClinicPatientRecord>> GetInvoiceRecordsByClinicIdAsync(int clinicId);
        Task<List<ClinicPatientRecord>> GetUnsentInvoiceRecordsAsync(int clinicId);
        Task<List<ClinicPatientRecord>> GetReceiptRecordsByClinicIdAsync(int clinicId);
        Task<List<ClinicPatientRecord>> GetUnsentReceiptRecordsAsync(int clinicId);

        Task<ClinicPatientRecord?> GetByUniqueRecordIdAsync(int uniqueRecordId);

        Task<ClinicPatientRecord?> GetByUniqueRecordIdAsync(int clinicId, string uniqueRecordId);


        Task<bool> DeleteAsync(int uniqueRecordId);

        // ✅ ADD THIS - Returns ClinicPatientRecord (Record entity)

        Task<ClinicPatientRecord?> GetRecordByIdAsync(int recordId);
        Task<ClinicPatientRecord> GetRecordByUniqueRecordIdAsync(string uniqueRecordId);
        // Task<decimal> GetLastReceiptAmountDueByHfIdAsync(string hfId);
        Task<decimal> GetTotalAmountDueByHfIdAsync(string hfId);
        Task<string?> GetLatestPackageNameByPatientIdAsync(int patientId);
        // Get receipt documents by receipt number
        Task<ClinicPatientRecord?> GetReceiptDocumentByReceiptNumberAsync(int clinicId, string receiptNumber);

        // Get all receipt documents for a visit
        Task<List<ClinicPatientRecord>> GetReceiptDocumentsByVisitAsync(int clinicId, int patientId, int visitId);

    
        Task<bool> DeleteDocumentsAsync(int recordId);

    }
}
