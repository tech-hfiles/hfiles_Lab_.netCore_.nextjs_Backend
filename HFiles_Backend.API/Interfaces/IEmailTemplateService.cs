using HFiles_Backend.Application.DTOs.Clinics.ConsentForm;
using HFiles_Backend.Application.DTOs.Clinics.PatientRecord;

namespace HFiles_Backend.API.Interfaces
{
     public interface IEmailTemplateService
    {
        string GenerateClinicOtpTemplate(string clinicName, string otp);
        string GenerateClinicWelcomeTemplate(string clinicName);
        string GenerateClinicAdminNotificationTemplate(string clinicName, string email, string phone, string pincode);
        string GenerateClinicLoginOtpTemplate(string otp, int validityMinutes);
        string GenerateClinicPasswordResetTemplate(string labName, string otp, int validityMinutes, string resetLink);
        string GenerateClinicUserPasswordResetTemplate(string firstName, string clinicName, string otp, int validityMinutes, string resetLink);
        string GenerateMultipleConsentFormsEmailTemplate(string patientFirstName, List<ConsentFormLinkInfo> consentFormLinks, string clinicName);
        string GenerateFollowUpAppointmentEmailTemplate(string patientFirstName, List<ConsentFormLinkInfo> consentFormLinks, string clinicName, string appointmentDate, string appointmentTime);
        string GenerateAppointmentConfirmationWithConsentFormsEmailTemplate(
       string patientFirstName,
       List<ConsentFormLinkInfo> consentFormLinks,
       string clinicName,
       string appointmentDate,
       string appointmentTime);
        string GenerateEmailBodySymptomDiary(string? firstName, string clinicName);
        string GeneratePatientDocumentsUploadedEmailTemplate(
           string patientFirstName,
           List<PatientDocumentInfo> uploadedDocuments,
           string clinicName,
           string appointmentDate,
           string appointmentTime);
    }
}
