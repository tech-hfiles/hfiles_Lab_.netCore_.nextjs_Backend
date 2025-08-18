using HFiles_Backend.API.Interfaces;

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
    }
}
