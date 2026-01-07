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



		public string GenerateFirstSessionConfirmationEmailTemplate(
	string patientName,
	string coachName,
	string sessionDate,
	string sessionTime,
	string clinicName)
		{
			return $@"
    <!DOCTYPE html>
    <html>
    <head>
        <meta charset='UTF-8'>
        <style>
            body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 0; }}
            .container {{ max-width: 600px; margin: 20px auto; background: #fff; border-radius: 10px; overflow: hidden; box-shadow: 0 4px 6px rgba(0,0,0,0.1); }}
            .header {{ background: linear-gradient(135deg, #ff6b6b 0%, #ee5a6f 100%); color: white; padding: 40px 20px; text-align: center; }}
            .header h1 {{ margin: 0; font-size: 28px; font-weight: 700; }}
            .fire-emoji {{ font-size: 40px; margin-bottom: 10px; }}
            .content {{ padding: 40px 30px; }}
            .greeting {{ font-size: 18px; color: #333; margin-bottom: 20px; }}
            .main-message {{ font-size: 17px; color: #555; line-height: 1.8; margin-bottom: 25px; }}
            .session-box {{ background: linear-gradient(135deg, #ff6b6b15 0%, #ee5a6f15 100%); border-left: 4px solid #ff6b6b; padding: 25px; margin: 25px 0; border-radius: 8px; }}
            .session-detail {{ margin: 12px 0; font-size: 16px; }}
            .label {{ font-weight: 700; color: #ff6b6b; margin-right: 8px; }}
            .value {{ color: #333; }}
            .motivation {{ background: #f8f9fa; padding: 20px; border-radius: 8px; margin: 25px 0; text-align: center; }}
            .motivation-text {{ font-size: 17px; color: #333; font-weight: 600; margin: 0; }}
            .lightning {{ color: #ffd700; font-size: 20px; }}
            .footer {{ background: #f8f9fa; padding: 30px 25px; text-align: center; border-top: 1px solid #e0e0e0; }}
            .team-signature {{ font-weight: 700; color: #ff6b6b; font-size: 18px; margin-top: 10px; }}
        </style>
    </head>
    <body>
        <div class='container'>
            <div class='header'>
                <h1>Your Fitness Journey Begins Today!</h1>
            </div>
            
            <div class='content'>
                <p class='greeting'>Dear {patientName},</p>
                
                <p class='main-message'>
                    We are excited to see you for your first session with <strong>Coach {coachName}</strong>.
                </p>
                
                <div class='session-box'>
                    <div class='session-detail'>
                        <span class='label'>📅 Date:</span>
                        <span class='value'>{sessionDate}</span>
                    </div>
                    <div class='session-detail'>
                        <span class='label'>⏰ Time:</span>
                        <span class='value'>{sessionTime}</span>
                    </div>
                    <div class='session-detail'>
                        <span class='label'>👤 Coach:</span>
                        <span class='value'>{coachName}</span>
                    </div>
                    <div class='session-detail'>
                        <span class='label'>📍 Location:</span>
                        <span class='value'>{clinicName}</span>
                    </div>
                </div>
                
                <div class='motivation'>
                    <p class='motivation-text'>
                        Come ready to have a great time — moving with us and don't forget to bring your enthusiasm! <span class='lightning'>⚡</span>
                    </p>
                </div>
                
                <p style='font-size: 18px; color: #333; font-weight: 600; margin-top: 30px;'>
                    Welcome to {clinicName}!
                </p>
            </div>
            
            <div class='footer'>
                <p style='color: #666; margin: 5px 0;'>
                    Please arrive 10 minutes early and bring comfortable sportswear, water bottle, and towel.
                </p>
                <p class='team-signature'>— Team {clinicName}</p>
            </div>
        </div>
    </body>
    </html>";
		}


		public string GenerateTrialAppointmentConfirmationEmailTemplate(
		string firstName,
		string clinicName,
		string appointmentDate,
		string appointmentTime,
		string fitnessGoal)
		{
			return $@"
    <!DOCTYPE html>
    <html>
    <head>
        <meta charset='UTF-8'>
        <meta name='viewport' content='width=device-width, initial-scale=1.0'>
        <style>
            body {{
                font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
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
                border-radius: 10px;
                overflow: hidden;
                box-shadow: 0 4px 6px rgba(0,0,0,0.1);
            }}
            .header {{
                background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
                color: white;
                padding: 30px 20px;
                text-align: center;
            }}
            .header h1 {{
                margin: 0;
                font-size: 26px;
                font-weight: 600;
            }}
            .emoji {{
                font-size: 32px;
                margin-bottom: 10px;
            }}
            .content {{
                padding: 30px 25px;
            }}
            .greeting {{
                font-size: 18px;
                color: #333;
                margin-bottom: 15px;
            }}
            .intro {{
                font-size: 16px;
                color: #555;
                margin-bottom: 25px;
            }}
            .appointment-box {{
                background: linear-gradient(135deg, #667eea15 0%, #764ba215 100%);
                border-left: 4px solid #667eea;
                padding: 20px;
                margin: 25px 0;
                border-radius: 8px;
            }}
            .detail-row {{
                display: flex;
                align-items: center;
                margin: 12px 0;
                font-size: 16px;
            }}
            .detail-icon {{
                font-size: 20px;
                margin-right: 12px;
                min-width: 25px;
            }}
            .detail-label {{
                font-weight: 600;
                color: #667eea;
                margin-right: 8px;
            }}
            .detail-value {{
                color: #333;
            }}
            .bring-section {{
                background-color: #f8f9fa;
                padding: 20px;
                border-radius: 8px;
                margin: 25px 0;
            }}
            .bring-section h3 {{
                color: #667eea;
                margin-top: 0;
                font-size: 18px;
                margin-bottom: 15px;
            }}
            .bring-list {{
                list-style: none;
                padding: 0;
                margin: 0;
            }}
            .bring-list li {{
                padding: 10px 0;
                border-bottom: 1px solid #e0e0e0;
                font-size: 15px;
                display: flex;
                align-items: center;
            }}
            .bring-list li:last-child {{
                border-bottom: none;
            }}
            .list-icon {{
                font-size: 20px;
                margin-right: 12px;
            }}
            .cta-section {{
                text-align: center;
                margin: 30px 0 20px 0;
                padding: 20px;
                background: linear-gradient(135deg, #667eea10 0%, #764ba210 100%);
                border-radius: 8px;
            }}
            .cta-text {{
                font-size: 17px;
                color: #333;
                font-weight: 600;
                margin-bottom: 10px;
            }}
            .footer {{
                background-color: #f8f9fa;
                padding: 25px;
                text-align: center;
                color: #666;
                font-size: 14px;
                border-top: 1px solid #e0e0e0;
            }}
            .footer-text {{
                margin: 5px 0;
            }}
            .team-signature {{
                font-weight: 600;
                color: #667eea;
                font-size: 16px;
                margin-top: 15px;
            }}
            .sparkle {{
                color: #ffd700;
            }}
        </style>
    </head>
    <body>
        <div class='container'>
            <div class='header'>
                <div class='emoji'>🎉</div>
                <h1>Ready for Your {clinicName} Trial Session?</h1>
            </div>
            
            <div class='content'>
                <p class='greeting'>Hi {firstName},</p>
                <p class='intro'>We're excited to see you for your trial session!</p>
                
                <div class='appointment-box'>
                    <div class='detail-row'>
                        <span class='detail-icon'>📅</span>
                        <span class='detail-label'>Date:</span>
                        <span class='detail-value'>{appointmentDate}</span>
                    </div>
                    <div class='detail-row'>
                        <span class='detail-icon'>⏰</span>
                        <span class='detail-label'>Time:</span>
                        <span class='detail-value'>{appointmentTime}</span>
                    </div>
                    <div class='detail-row'>
                        <span class='detail-icon'>📍</span>
                        <span class='detail-label'>Location:</span>
                        <span class='detail-value'>{clinicName}</span>
                    </div>
                    {(!string.IsNullOrEmpty(fitnessGoal) ? $@"
                    <div class='detail-row'>
                        <span class='detail-icon'>🎯</span>
                        <span class='detail-label'>Your Goal:</span>
                        <span class='detail-value'>{fitnessGoal}</span>
                    </div>" : "")}
                </div>
                
                <div class='bring-section'>
                    <h3>What to Bring:</h3>
                    <p style='margin-top: 0; margin-bottom: 15px; color: #555;'>
                        To make the most of your session, kindly come dressed in comfortable sportswear and bring the following:
                    </p>
                    <ul class='bring-list'>
                        <li>
                            <span class='list-icon'>👟</span>
                            <span>Shoes to change into (indoor-friendly)</span>
                        </li>
                        <li>
                            <span class='list-icon'>💧</span>
                            <span>A water bottle</span>
                        </li>
                        <li>
                            <span class='list-icon'>🧻</span>
                            <span>A napkin/towel</span>
                        </li>
                    </ul>
                </div>
                
                <div class='cta-section'>
                    <p class='cta-text'>Get ready to move, have fun, and experience the {clinicName} energy!</p>
                </div>
                
                <p style='color: #555; font-size: 15px; margin-top: 25px;'>
                    If you have any questions, feel free to reach out.
                </p>
                
                <p style='color: #555; font-size: 16px; font-weight: 500; margin-top: 20px;'>
                    See you soon!
                </p>
            </div>
            
            <div class='footer'>
                
                <p class='team-signature'>Team {clinicName} <span class='sparkle'>✨</span></p>
            </div>
        </div>
    </body>
    </html>";
		}
	}





}
