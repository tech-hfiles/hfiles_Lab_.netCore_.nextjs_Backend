using HFiles_Backend.API.Interfaces;
using HFiles_Backend.Application.DTOs.Clinics.ConsentForm;
using HFiles_Backend.Application.DTOs.Clinics.PatientRecord;

namespace HFiles_Backend.API.Services
{
    public class EmailTemplateService : IEmailTemplateService
    {
        // Helper method to replace terminology based on clinic ID
        private string ReplaceTerminology(string template, int clinicId)
        {
            if (clinicId == 36)
            {
                template = template.Replace("Clinic", "Gym")
                                   .Replace("clinic", "gym")
                                   .Replace("Patient", "Member")
                                   .Replace("patient", "member");
            }
            return template;
        }
        public string GenerateClinicOtpTemplate(string clinicName, string otp, int clinicId = 0)
        {
            var template = $"""
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
            return ReplaceTerminology(template, clinicId);
        }

        public string GenerateClinicWelcomeTemplate(string clinicName, int clinicId = 0)
        {
            var template = $"""
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
            return ReplaceTerminology(template, clinicId);
        }

        public string GenerateClinicAdminNotificationTemplate(string clinicName, string email, string phone, string pincode, int clinicId = 0)
        {
            var template = $"""
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
            return ReplaceTerminology(template, clinicId);
        }

        public string GenerateClinicLoginOtpTemplate(string otp, int validityMinutes, int clinicId = 0)
        {
            var template = $"""
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
            return ReplaceTerminology(template, clinicId);
        }

        public string GenerateClinicPasswordResetTemplate(string clinicName, string otp, int validityMinutes, string resetLink, int clinicId = 0)
        {
            var template = $"""
            <html>
            <body style='font-family:Arial,sans-serif;'>
                <p>Hello <strong>{clinicName}</strong>,</p>
                <p>Your OTP for Clinic Reset Password is:</p>
                <h2 style='color: #333;'>{otp}</h2>
                <p>This OTP is valid for <strong>{validityMinutes} minutes</strong>.</p>
                <p>You have requested to reset your password for your clinic account. Click the button below to proceed:</p>
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
            return ReplaceTerminology(template, clinicId);
        }

        public string GenerateClinicUserPasswordResetTemplate(string firstName, string clinicName, string otp, int validityMinutes, string resetLink, int clinicId = 0)
        {
            var template = $"""
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
            return ReplaceTerminology(template, clinicId);
        }


        public string GenerateMultipleConsentFormsEmailTemplate(string patientFirstName, List<ConsentFormLinkInfo> consentFormLinks, string clinicName, int clinicId = 0)
        {
            var consentFormsList = string.Join("", consentFormLinks.Select(link =>
                $@"<li style='margin: 10px 0;'>
                      <a href='{link.ConsentFormLink}' 
                         style='color: #0331B5; text-decoration: none; font-weight: bold;'>
                         {link.ConsentFormName}
                      </a>
                   </li>"));

            var template =  $"""
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
            return ReplaceTerminology(template, clinicId);
        }





        public string GenerateAppointmentConfirmationWithConsentFormsEmailTemplate(
        string patientFirstName,
        List<ConsentFormLinkInfo> consentFormLinks,
        string clinicName,
        string appointmentDate,
        string appointmentTime, int clinicId = 0)
        {
            var consentFormsList = string.Join("", consentFormLinks.Select(link =>
                $@"<li style='margin: 10px 0;'>
                  <a href='{link.ConsentFormLink}' 
                     style='color: #0331B5; text-decoration: none; font-weight: bold;'>
                     {link.ConsentFormName}
                  </a>
               </li>"));

            var template = $"""
        <html>
        <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
            <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                <h2 style='color: #0331B5; text-align: center;'>HFiles - Appointment Confirmation & Consent Forms</h2>
                
                <p>Dear <strong>{patientFirstName}</strong>,</p>
                
                <p>Your appointment has been successfully scheduled with <strong>{clinicName}</strong>.</p>
                
                <div style='background-color: #e8f5e8; padding: 15px; border-radius: 8px; margin: 20px 0; border-left: 4px solid #28a745;'>
                    <h3 style='margin-top: 0; color: #155724;'>Appointment Details:</h3>
                    <p style='margin: 5px 0;'><strong>Clinic:</strong> {clinicName}</p>
                    <p style='margin: 5px 0;'><strong>Date:</strong> {appointmentDate}</p>
                    <p style='margin: 5px 0;'><strong>Time:</strong> {appointmentTime}</p>
                </div>
                
                <div style='background-color: #f8f9fa; padding: 15px; border-radius: 8px; margin: 20px 0;'>
                    <h3 style='margin-top: 0; color: #0331B5;'>Required Consent Forms:</h3>
                    <p>Before your appointment, please complete the following consent forms:</p>
                    <ul style='margin: 10px 0; padding-left: 20px;'>
                        {consentFormsList}
                    </ul>
                </div>
                
                <div style='background-color: #fff3cd; padding: 15px; border-radius: 8px; margin: 20px 0; border-left: 4px solid #ffc107;'>
                    <p style='margin: 0;'><strong>Important:</strong> Please complete all consent forms before your appointment to ensure a smooth visit and avoid any delays.</p>
                </div>
                
                <p>If you have any questions or need assistance, please contact the clinic directly.</p>
                
                <p>You can also access your consent forms by:</p>
                <ol>
                    <li>Logging into your HFiles account</li>
                    <li>Navigating to the notification section</li>
                    <li>Completing your pending consent forms</li>
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
            return ReplaceTerminology(template, clinicId);
        }




        public string GenerateFollowUpAppointmentEmailTemplate(string patientFirstName, List<ConsentFormLinkInfo> consentFormLinks, string clinicName, string appointmentDate, string appointmentTime, int clinicId = 0)
        {
            var consentFormsList = string.Join("", consentFormLinks.Select(link =>
                $@"<li style='margin: 10px 0;'>
                      <a href='{link.ConsentFormLink}' 
                         style='color: #0331B5; text-decoration: none; font-weight: bold;'>
                         {link.ConsentFormName}
                      </a>
                   </li>"));

            var template = $"""
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
            return ReplaceTerminology(template, clinicId);
        }





        public string GenerateEmailBodySymptomDiary(string? firstName, string clinicName, int clinicId = 0)
        {
            var patientName = string.IsNullOrWhiteSpace(firstName) ? "Patient" : firstName;

           var template = $@"
            <!DOCTYPE html>
            <html lang=""en"">
            <head>
                <meta charset=""UTF-8"">
                <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                <style>
                    body {{
                        font-family: Arial, Helvetica, sans-serif;
                        line-height: 1.6;
                        color: #333;
                        background-color: #f4f4f4;
                        margin: 0;
                        padding: 0;
                    }}
                    .container {{
                        max-width: 600px;
                        margin: 20px auto;
                        background-color: #ffffff;
                        border-radius: 8px;
                        overflow: hidden;
                        box-shadow: 0 2px 10px rgba(0,0,0,0.1);
                    }}
                    .header {{
                        background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
                        color: white;
                        padding: 30px;
                        text-align: center;
                    }}
                    .header h1 {{
                        margin: 0;
                        font-size: 24px;
                    }}
                    .content {{
                        padding: 30px;
                    }}
                    .content p {{
                        margin-bottom: 15px;
                    }}
                    .highlight {{
                        background-color: #f0f0f0;
                        border-left: 4px solid #667eea;
                        padding: 15px;
                        margin: 20px 0;
                    }}
                    .footer {{
                        background-color: #f8f9fa;
                        padding: 20px;
                        text-align: center;
                        font-size: 12px;
                        color: #6c757d;
                    }}
                    .button {{
                        display: inline-block;
                        background-color: #667eea;
                        color: white;
                        padding: 12px 30px;
                        text-decoration: none;
                        border-radius: 5px;
                        margin-top: 15px;
                    }}
                </style>
            </head>
            <body>
                <div class=""container"">
                    <div class=""header"">
                        <h1>📋 Symptom Diary</h1>
                    </div>
                    <div class=""content"">
                        <p>Dear {patientName},</p>
            
                        <p>We hope this email finds you well.</p>
            
                        <div class=""highlight"">
                            <p><strong>{clinicName}</strong> has sent you a symptom diary to help track your health progress.</p>
                        </div>
            
                        <p>Please find the symptom diary attached to this email. We kindly request you to:</p>
            
                        <ul>
                            <li>Download and review the symptom diary</li>
                            <li>Fill it out regularly as instructed</li>
                            <li>Keep track of your symptoms accurately</li>
                            <li>Share it with your healthcare provider during your next visit</li>
                        </ul>
            
                        <p>If you have any questions or concerns about the symptom diary, please don't hesitate to contact us.</p>
            
                        <p>Thank you for your cooperation in managing your health.</p>
            
                        <p>Best regards,<br>
                        <strong>{clinicName}</strong></p>
                    </div>
                    <div class=""footer"">
                        <p>This is an automated email from {clinicName}. Please do not reply to this email.</p>
                        <p>© {DateTime.UtcNow.Year} HFiles Health Management System. All rights reserved.</p>
                    </div>
                </div>
            </body>
            </html>";
            return ReplaceTerminology(template, clinicId);
        }



        public string GeneratePatientDocumentsUploadedEmailTemplate(
      string patientFirstName,
      List<PatientDocumentInfo> uploadedDocuments,
      string clinicName,
      string appointmentDate,
      string appointmentTime, int clinicId = 0)
        {
            var documentsList = string.Join("", uploadedDocuments.Select(doc =>
                $@"<li style='margin: 15px 0; background-color: white; padding: 12px; border-radius: 6px; border: 1px solid #e0e0e0;'>
              <div style='display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap;'>
                  <div style='flex: 1; min-width: 200px; margin-bottom: 8px;'>
                      <strong style='color: #0331B5; font-size: 16px;'>{doc.DocumentType}</strong>
                      <br/>
                      <span style='color: #666; font-size: 14px;'>📁 {doc.Category}</span>
                  </div>
                  <div>
                      <a href='{doc.DocumentUrl}' 
                         target='_blank'
                         style='background-color: #0331B5; 
                                color: white; 
                                padding: 10px 24px; 
                                text-decoration: none; 
                                border-radius: 6px; 
                                font-weight: bold; 
                                display: inline-block;
                                font-size: 14px;'>
                         📄 View Document
                      </a>
                  </div>
              </div>
           </li>"));

            var template = $"""
    <html>
    <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
        <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
            <h2 style='color: #0331B5; text-align: center;'>HFiles - Documents Uploaded</h2>
            
            <p>Dear <strong>{patientFirstName}</strong>,</p>
            
            <p><strong>{clinicName}</strong> has uploaded {uploadedDocuments.Count} document(s) to your HFiles account.</p>
            
            <div style='background-color: #e8f5e8; padding: 15px; border-radius: 8px; margin: 20px 0; border-left: 4px solid #28a745;'>
                <h3 style='margin-top: 0; color: #155724;'>Appointment Details:</h3>
                <p style='margin: 5px 0;'><strong>Clinic:</strong> {clinicName}</p>
                <p style='margin: 5px 0;'><strong>Date:</strong> {appointmentDate}</p>
                <p style='margin: 5px 0;'><strong>Time:</strong> {appointmentTime}</p>
            </div>
            
            <div style='background-color: #f8f9fa; padding: 15px; border-radius: 8px; margin: 20px 0;'>
                <h3 style='margin-top: 0; color: #0331B5;'>Documents Uploaded:</h3>
                <ul style='margin: 10px 0; padding-left: 0; list-style: none;'>
                    {documentsList}
                </ul>
            </div>
            
            <div style='background-color: #e7f3ff; padding: 15px; border-radius: 8px; margin: 20px 0; border-left: 4px solid #0331B5;'>
                <p style='margin: 0;'><strong>💡 Alternative Access:</strong></p>
                <p style='margin: 10px 0;'>You can also access your documents anytime by:</p>
                <ol style='margin: 10px 0; padding-left: 20px;'>
                    <li>Logging in to your HFiles account</li>
                    <li>Going to "All Reports" section</li>
                    <li>Finding your documents under their respective categories</li>
                </ol>
            </div>
            
            <p>If you have any questions about these documents or need assistance accessing them, please contact the clinic directly.</p>
            
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
            return ReplaceTerminology(template, clinicId);
        }
    }
}
