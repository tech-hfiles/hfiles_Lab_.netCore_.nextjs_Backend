using HFiles_Backend.Domain.Entities.Clinics;

namespace HFiles_Backend.Domain.Interfaces
{
    public interface IClinicPrescriptionRepository
    {
        Task SavePrescriptionAsync(ClinicPrescription prescription);
        Task<List<ClinicPrescription>> GetPrescriptionsByClinicIdAsync(int clinicId);
        Task<ClinicPrescription?> GetByIdAsync(int prescriptionId);
        Task UpdatePrescriptionAsync(ClinicPrescription prescription);

        Task DeletePrescriptionAsync(int prescriptionId);


        Task<ClinicPrescriptionNotes> SavePrescriptionNoteAsync(ClinicPrescriptionNotes note);
        Task<List<ClinicPrescriptionNotes>> GetPrescriptionNotesByClinicIdAsync(int clinicId);
        Task<ClinicPrescriptionNotes?> GetByNotesIdAsync(int noteId);
        Task<ClinicPrescriptionNotes?> GetPrescriptionNoteByIdAsync(int noteId);
        Task UpdatePrescriptionNoteAsync(ClinicPrescriptionNotes note);
        Task DeletePrescriptionNoteAsync(int noteId);
    }
}
