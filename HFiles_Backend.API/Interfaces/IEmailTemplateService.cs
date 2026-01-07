using HFiles_Backend.Application.DTOs.Clinics.ConsentForm;
using HFiles_Backend.Application.DTOs.Clinics.PatientRecord;

namespace HFiles_Backend.API.Interfaces
{
     public interface IEmailTemplateService
    {
        string GenerateClinicOtpTemplate(string clinicName, string otp, int clinicId = 0);
        string GenerateClinicWelcomeTemplate(string clinicName, int clinicId = 0);
        string GenerateClinicAdminNotificationTemplate(string clinicName, string email, string phone, string pincode, int clinicId = 0);
        string GenerateClinicLoginOtpTemplate(string otp, int validityMinutes, int clinicId = 0);
        string GenerateClinicPasswordResetTemplate(string labName, string otp, int validityMinutes, string resetLink, int clinicId = 0);
        string GenerateClinicUserPasswordResetTemplate(string firstName, string clinicName, string otp, int validityMinutes, string resetLink, int clinicId = 0);
        string GenerateMultipleConsentFormsEmailTemplate(string patientFirstName, List<ConsentFormLinkInfo> consentFormLinks, string clinicName, int clinicId = 0);
        string GenerateFollowUpAppointmentEmailTemplate(string patientFirstName, List<ConsentFormLinkInfo> consentFormLinks, string clinicName, string appointmentDate, string appointmentTime, int clinicId = 0);
        string GenerateAppointmentConfirmationWithConsentFormsEmailTemplate(
       string patientFirstName,
       List<ConsentFormLinkInfo> consentFormLinks,
       string clinicName,
       string appointmentDate,
       string appointmentTime, int clinicId = 0);
        string GenerateEmailBodySymptomDiary(string? firstName, string clinicName, int clinicId = 0);
        string GeneratePatientDocumentsUploadedEmailTemplate(
           string patientFirstName,
           List<PatientDocumentInfo> uploadedDocuments,
           string clinicName,
           string appointmentDate,
           string appointmentTime, int clinicId = 0);


		string GenerateTrialAppointmentConfirmationEmailTemplate(
		string firstName,
		string clinicName,
		string appointmentDate,
		string appointmentTime,
		string fitnessGoal
	);


		string GenerateFirstSessionConfirmationEmailTemplate(
	string patientName,
	string coachName,
	string sessionDate,
	string sessionTime,
	string clinicName
);

	}
}

