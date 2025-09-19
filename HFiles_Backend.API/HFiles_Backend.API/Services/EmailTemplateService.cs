using HFiles_Backend.API.Interfaces;
using HFiles_Backend.Application.DTOs.Clinics.ConsentForm;

namespace HFiles_Backend.API.Services
{
    public class EmailTemplateService : IEmailTemplateService
    {
        public string GenerateClinicOtpTemplate(string clinicName, string otp)
        {
            return $"""
            <html>
            <body style='font-family: Arial, sans-serif; line-height: 1.6;'>
                <p>Hello <strong>{clinicName}</strong>,</p>
                <p>Welcome to <strong>Hfiles</strong>!</p>
                <p>Your One-Time Password (OTP) is:</p>
                <h2>{otp}</h2>
                <p>This OTP expires in 5 minutes.</p>
                <p>Need help? <a href='mailto:contact@hfiles.in'>contact@hfiles.in</a></p>
                <p>– The Hfiles Team</p>
            </body>
            </html>
            """;
        }

        public string GenerateClinicWelcomeTemplate(string clinicName)
        {
            return $"""
            <html>
            <body style='font-family: Arial, sans-serif; line-height: 1.6;'>
                <p>Hello <strong>{clinicName}</strong>,</p>
                <p>Welcome to <strong>Hfiles</strong>!</p>
                <p>Your registration has been successfully received.</p>
                <p>Contact us at <a href='mailto:contact@hfiles.in'>contact@hfiles.in</a></p>
                <p>– The Hfiles Team</p>
            </body>
            </html>
            """;
        }

        public string GenerateClinicAdminNotificationTemplate(string clinicName, string email, string phone, string pincode)
        {
            return $"""
            <html>
            <body style='font-family: Arial, sans-serif; line-height: 1.6;'>
                <h2>New Clinic Signup Received</h2>
                <p><strong>Clinic Name:</strong> {clinicName}<br/>
                <strong>Email:</strong> {email}<br/>
                <strong>Phone:</strong> {phone}<br/>
                <strong>Pincode:</strong> {pincode}</p>
                <p>Please follow up with the clinic for onboarding.</p>
            </body>
            </html>
            """;
        }

        public string GenerateClinicLoginOtpTemplate(string otp, int validityMinutes)
        {
            return $"""
            <html>
            <body style='font-family: Arial, sans-serif; line-height: 1.6;'>
                <p>Hello,</p>
                <p>Your OTP for <strong>Hfiles Clinic</strong> login is:</p>
                <h2 style='color: #333;'>{otp}</h2>
                <p>This OTP is valid for <strong>{validityMinutes} minutes</strong>.</p>
                <p>If you didn’t request this, you can ignore this email.</p>
                <br/>
                <p>Best regards,<br/>The Hfiles Team</p>
            </body>
            </html>
            """;
        }

        public string GenerateClinicPasswordResetTemplate(string clinicName, string otp, int validityMinutes, string resetLink)
        {
            return $"""
            <html>
            <body style='font-family:Arial,sans-serif;'>
                <p>Hello <strong>{clinicName}</strong>,</p>
                <p>Your OTP for Lab Reset Password is:</p>
                <h2 style='color: #333;'>{otp}</h2>
                <p>This OTP is valid for <strong>{validityMinutes} minutes</strong>.</p>
                <p>You have requested to reset your password for your lab account. Click the button below to proceed:</p>
                <p>
                    <a href='{resetLink}' 
                       style='background-color:#0331B5;color:white;padding:10px 20px;text-decoration:none;font-weight:bold;'>
                       Reset Password
                    </a>
                </p>
                <p>If you did not request this, please ignore this email.</p>
                <br />
                <p>Best regards,<br>The Hfiles Team</p>
            </body>
            </html>
            """;
        }

        public string GenerateClinicUserPasswordResetTemplate(string firstName, string clinicName, string otp, int validityMinutes, string resetLink)
        {
            return $"""
                <html>
                <body style='font-family:Arial,sans-serif;'>
                    <p>Hello <strong>{firstName}</strong>,</p>
                    <p>Your OTP for Reset Password is:</p>
                    <h2 style='color: #333;'>{otp}</h2>
                    <p>This OTP is valid for <strong>{validityMinutes} minutes</strong>.</p>
                    <p>You requested a reset for <strong>{clinicName}</strong>. Click below to proceed:</p>
                    <p>
                        <a href='{resetLink}' style='background-color:#0331B5;color:white;padding:10px 20px;text-decoration:none;font-weight:bold;'>
                            Reset Password
                        </a>
                    </p>
                    <p>If you didn't request this, just ignore it.</p>
                    <br />
                    <p>Regards,<br>The Hfiles Team</p>
                </body>
                </html>
                """;
        }


        public string GenerateMultipleConsentFormsEmailTemplate(string patientFirstName, List<ConsentFormLinkInfo> consentFormLinks, string clinicName)
        {
            var consentFormsList = string.Join("", consentFormLinks.Select(link =>
                $@"<li style='margin: 10px 0;'>
                      <a href='{link.ConsentFormLink}' 
                         style='color: #0331B5; text-decoration: none; font-weight: bold;'>
                         {link.ConsentFormName}
                      </a>
                   </li>"));

            return $"""
            <html>
            <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                    <h2 style='color: #0331B5; text-align: center;'>HFiles - Consent Forms Required</h2>
                    
                    <p>Dear <strong>{patientFirstName}</strong>,</p>
                    
                    <p>You have received multiple consent forms from <strong>{clinicName}</strong> that require your attention.</p>
                    
                    <div style='background-color: #f8f9fa; padding: 15px; border-radius: 8px; margin: 20px 0;'>
                        <h3 style='margin-top: 0; color: #0331B5;'>Consent Forms to Complete:</h3>
                        <ul style='margin: 10px 0; padding-left: 20px;'>
                            {consentFormsList}
                        </ul>
                        <p><strong>Clinic:</strong> {clinicName}</p>
                    </div>
                    
                    <p>Please click on each consent form link above to access and complete them individually.</p>
                    
                    <div style='background-color: #fff3cd; padding: 15px; border-radius: 8px; margin: 20px 0; border-left: 4px solid #ffc107;'>
                        <p style='margin: 0;'><strong>Important:</strong> Please complete all consent forms before your appointment to ensure a smooth visit.</p>
                    </div>
                    
                    <p>If you have any questions or need assistance, please contact the clinic directly.</p>
                    
                    <p>Alternatively, you can:</p>
                    <ol>
                        <li>Login to your HFiles account</li>
                        <li>Navigate to the notification section</li>
                        <li>Find and complete your consent forms</li>
                    </ol>
                    
                    <hr style='margin: 30px 0; border: none; border-top: 1px solid #eee;' />
                    
                    <p style='font-size: 14px; color: #666;'>
                        Best regards,<br/>
                        The HFiles Team<br/>
                        <a href='mailto:contact@hfiles.in' style='color: #0331B5;'>contact@hfiles.in</a>
                    </p>
                    
                    <p style='font-size: 12px; color: #999; text-align: center; margin-top: 20px;'>
                        This is an automated message. Please do not reply to this email.
                    </p>
                </div>
            </body>
            </html>
            """;
        }





        public string GenerateFollowUpAppointmentEmailTemplate(string patientFirstName, List<ConsentFormLinkInfo> consentFormLinks, string clinicName, string appointmentDate, string appointmentTime)
        {
            var consentFormsList = string.Join("", consentFormLinks.Select(link =>
                $@"<li style='margin: 10px 0;'>
                      <a href='{link.ConsentFormLink}' 
                         style='color: #0331B5; text-decoration: none; font-weight: bold;'>
                         {link.ConsentFormName}
                      </a>
                   </li>"));

            return $"""
            <html>
            <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                    <h2 style='color: #0331B5; text-align: center;'>HFiles - Appointment Confirmation</h2>
                    
                    <p>Dear <strong>{patientFirstName}</strong>,</p>
                    
                    <p>Your follow-up appointment has been successfully scheduled with <strong>{clinicName}</strong>.</p>
                    
                    <div style='background-color: #e8f4fd; padding: 15px; border-radius: 8px; margin: 20px 0; border-left: 4px solid #0331B5;'>
                        <h3 style='margin-top: 0; color: #0331B5;'>Appointment Details:</h3>
                        <p style='margin: 5px 0;'><strong>Date:</strong> {appointmentDate}</p>
                        <p style='margin: 5px 0;'><strong>Time:</strong> {appointmentTime}</p>
                        <p style='margin: 5px 0;'><strong>Clinic:</strong> {clinicName}</p>
                    </div>
                    
                    {(consentFormLinks.Any() ? $@"
                    <div style='background-color: #f8f9fa; padding: 15px; border-radius: 8px; margin: 20px 0;'>
                        <h3 style='margin-top: 0; color: #0331B5;'>Required Consent Forms:</h3>
                        <p>Please complete the following consent forms before your appointment:</p>
                        <ul style='margin: 10px 0; padding-left: 20px;'>
                            {consentFormsList}
                        </ul>
                    </div>
                    
                    <div style='background-color: #fff3cd; padding: 15px; border-radius: 8px; margin: 20px 0; border-left: 4px solid #ffc107;'>
                        <p style='margin: 0;'><strong>Important:</strong> Please complete all consent forms before your appointment to ensure a smooth visit.</p>
                    </div>" : "")}
                    
                    <p>If you have any questions or need to reschedule, please contact the clinic directly.</p>
                    
                    <p>Alternatively, you can:</p>
                    <ol>
                        <li>Login to your HFiles account</li>
                        <li>View your appointment details</li>
                        <li>Access consent forms from the notification section</li>
                    </ol>
                    
                    <hr style='margin: 30px 0; border: none; border-top: 1px solid #eee;' />
                    
                    <p style='font-size: 14px; color: #666;'>
                        Best regards,<br/>
                        The HFiles Team<br/>
                        <a href='mailto:contact@hfiles.in' style='color: #0331B5;'>contact@hfiles.in</a>
                    </p>
                    
                    <p style='font-size: 12px; color: #999; text-align: center; margin-top: 20px;'>
                        This is an automated message. Please do not reply to this email.
                    </p>
                </div>
            </body>
            </html>
            """;
        }
    }
}
