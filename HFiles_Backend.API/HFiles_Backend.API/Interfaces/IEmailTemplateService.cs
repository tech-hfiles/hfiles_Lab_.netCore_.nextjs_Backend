namespace HFiles_Backend.API.Interfaces
{
     public interface IEmailTemplateService
    {
        string GenerateClinicOtpTemplate(string clinicName, string otp);
        string GenerateClinicWelcomeTemplate(string clinicName);
        string GenerateClinicAdminNotificationTemplate(string clinicName, string email, string phone, string pincode);
        string GenerateClinicLoginOtpTemplate(string otp, int validityMinutes);
    }
}
