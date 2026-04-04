using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;
using onlineStore.Settings;

namespace onlineStore.Services.Email
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(
            IOptions<EmailSettings> emailOptions,
            ILogger<EmailService> logger)
        {
            _emailSettings = emailOptions?.Value ?? new EmailSettings();
            _logger = logger;
        }

        public async Task SendEmailAsync(
            string to,
            string subject,
            string htmlBody)
        {
            var normalizedTo = to?.Trim();
            var normalizedSubject = subject?.Trim();
            var normalizedHtmlBody = htmlBody?.Trim();

            if (string.IsNullOrWhiteSpace(normalizedTo))
            {
                _logger.LogWarning("Email sending skipped because recipient email is missing.");
                throw new ArgumentException("Recipient email is required.", nameof(to));
            }

            if (string.IsNullOrWhiteSpace(normalizedSubject))
            {
                _logger.LogWarning(
                    "Email sending skipped for {Recipient} because subject is missing.",
                    normalizedTo);
                throw new ArgumentException("Email subject is required.", nameof(subject));
            }

            if (string.IsNullOrWhiteSpace(normalizedHtmlBody))
            {
                _logger.LogWarning(
                    "Email sending skipped for {Recipient} because HTML body is missing.",
                    normalizedTo);
                throw new ArgumentException("HTML body is required.", nameof(htmlBody));
            }

            ValidateEmailSettings();

            try
            {
                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(
                        _emailSettings.FromEmail.Trim(),
                        _emailSettings.FromName.Trim(),
                        Encoding.UTF8),
                    Subject = normalizedSubject,
                    SubjectEncoding = Encoding.UTF8,
                    Body = normalizedHtmlBody,
                    BodyEncoding = Encoding.UTF8,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(new MailAddress(normalizedTo));

                using var smtpClient = new SmtpClient(_emailSettings.SmtpHost.Trim(), _emailSettings.Port)
                {
                    EnableSsl = _emailSettings.EnableSsl,
                    UseDefaultCredentials = false,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Credentials = new NetworkCredential(
                        _emailSettings.Username.Trim(),
                        _emailSettings.AppPassword)
                };

                await smtpClient.SendMailAsync(mailMessage);

                _logger.LogInformation(
                    "Email sent successfully via Gmail SMTP to {Recipient} with subject {Subject}.",
                    normalizedTo,
                    normalizedSubject);
            }
            catch (Exception ex) when (
                ex is SmtpException ||
                ex is InvalidOperationException ||
                ex is FormatException)
            {
                _logger.LogError(
                    ex,
                    "Failed to send email to {Recipient} using Gmail SMTP.",
                    normalizedTo);

                throw new InvalidOperationException(
                    "Failed to send email using Gmail SMTP.",
                    ex);
            }
        }

        private void ValidateEmailSettings()
        {
            if (string.IsNullOrWhiteSpace(_emailSettings.FromEmail))
            {
                _logger.LogWarning("Email sending failed because FromEmail is not configured.");
                throw new InvalidOperationException("EmailSettings:FromEmail is not configured.");
            }

            if (string.IsNullOrWhiteSpace(_emailSettings.FromName))
            {
                _logger.LogWarning("Email sending failed because FromName is not configured.");
                throw new InvalidOperationException("EmailSettings:FromName is not configured.");
            }

            if (string.IsNullOrWhiteSpace(_emailSettings.SmtpHost))
            {
                _logger.LogWarning("Email sending failed because SmtpHost is not configured.");
                throw new InvalidOperationException("EmailSettings:SmtpHost is not configured.");
            }

            if (_emailSettings.Port <= 0)
            {
                _logger.LogWarning("Email sending failed because Port is invalid.");
                throw new InvalidOperationException("EmailSettings:Port must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(_emailSettings.Username))
            {
                _logger.LogWarning("Email sending failed because Username is not configured.");
                throw new InvalidOperationException("EmailSettings:Username is not configured.");
            }

            if (string.IsNullOrWhiteSpace(_emailSettings.AppPassword))
            {
                _logger.LogWarning("Email sending failed because AppPassword is not configured.");
                throw new InvalidOperationException("EmailSettings:AppPassword is not configured.");
            }
        }
    }
}
