using System.Net;
using System.Net.Mail;

namespace HFiles_Backend.API.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _logger.LogInformation("EmailService initialized successfully.");
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            _logger.LogInformation("Preparing to send email to {Email}.", toEmail);
            await SendEmailWithAttachmentsAsync(toEmail, subject, body, new List<Attachment>());
        }

        public async Task SendEmailWithAttachmentsAsync(string toEmail, string subject, string body, List<Attachment> attachments)
        {
            try
            {
                var smtpHost = _configuration["Smtp:Host"]
                               ?? throw new InvalidOperationException("SMTP Host is not configured.");
                var smtpPortStr = _configuration["Smtp:Port"];
                var smtpUser = _configuration["Smtp:Username"]
                               ?? throw new InvalidOperationException("SMTP Username is not configured.");
                var smtpPass = _configuration["Smtp:Password"]
                               ?? throw new InvalidOperationException("SMTP Password is not configured.");
                var fromEmail = _configuration["Smtp:From"]
                                ?? throw new InvalidOperationException("SMTP From address is not configured.");

                if (string.IsNullOrWhiteSpace(smtpPortStr) || !int.TryParse(smtpPortStr, out int smtpPort))
                    throw new InvalidOperationException("SMTP Port is either missing or not a valid integer.");

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(toEmail);

                foreach (var attachment in attachments)
                {
                    mailMessage.Attachments.Add(attachment);
                }

                using var smtpClient = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUser, smtpPass),
                    EnableSsl = true,
                    Timeout = 60000
                };

                _logger.LogInformation("Sending email to {Email}. Attachments Count: {AttachmentCount}.", toEmail, attachments.Count);

                await smtpClient.SendMailAsync(mailMessage);

                _logger.LogInformation("Email successfully sent to {Email}.", toEmail);
            }
            catch (SmtpException smtpEx)
            {
                _logger.LogError(smtpEx, "SMTP error occurred while sending email to {Email}. StatusCode: {StatusCode}", toEmail, smtpEx.StatusCode);
                throw new Exception("SMTP failure. Please check credentials or server settings.", smtpEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while sending email to {Email}.", toEmail);
                throw new Exception("An unexpected error occurred while sending email.", ex);
            }
            finally
            {
                foreach (var attachment in attachments)
                {
                    attachment.Dispose();
                }

                _logger.LogInformation("Email process cleanup completed for {Email}.", toEmail);
            }
        }
    }
}
