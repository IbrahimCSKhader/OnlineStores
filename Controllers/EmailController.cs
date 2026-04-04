using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using onlineStore.DTOs.Email;
using onlineStore.Services.Email;
using System.Net;

namespace onlineStore.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmailController : ControllerBase
    {
        private const string TestRecipientEmail = "ik2907951@gmail.com";
        private const string DefaultTestFirstName = "Test User";
        private const string DefaultTestCode = "123456";

        private readonly IEmailService _emailService;
        private readonly ILogger<EmailController> _logger;
        private readonly IWebHostEnvironment _environment;

        public EmailController(
            IEmailService emailService,
            ILogger<EmailController> logger,
            IWebHostEnvironment environment)
        {
            _emailService = emailService;
            _logger = logger;
            _environment = environment;
        }

        [HttpPost("test-send")]
        public async Task<IActionResult> SendTestEmail(CancellationToken cancellationToken)
        {
            var sentAtUtc = DateTime.UtcNow;
            cancellationToken.ThrowIfCancellationRequested();

            var htmlBody = $"""
                <!DOCTYPE html>
                <html lang="en">
                <head>
                    <meta charset="utf-8">
                    <meta name="viewport" content="width=device-width, initial-scale=1.0">
                    <title>Test Email</title>
                </head>
                <body style="margin:0;padding:24px;background-color:#f4f6f8;font-family:Arial,Helvetica,sans-serif;color:#1f2937;">
                    <div style="max-width:600px;margin:0 auto;background:#ffffff;border:1px solid #e5e7eb;border-radius:12px;padding:32px;">
                        <h1 style="margin-top:0;">Email Test</h1>
                        <p>This is a test email from the onlineStore backend.</p>
                        <p>Sent at UTC: <strong>{sentAtUtc:yyyy-MM-dd HH:mm:ss}</strong></p>
                    </div>
                </body>
                </html>
                """;

            return await SendEmailInternalAsync(
                TestRecipientEmail,
                "onlineStore test email",
                htmlBody,
                "Test email sent successfully.",
                cancellationToken,
                new
                {
                    recipient = TestRecipientEmail,
                    sentAtUtc
                });
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendEmail(
            [FromBody] SendEmailDto dto,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            cancellationToken.ThrowIfCancellationRequested();

            return await SendEmailInternalAsync(
                dto.To,
                dto.Subject,
                dto.HtmlBody,
                "Email sent successfully.",
                cancellationToken);
        }

        [AllowAnonymous]
        [HttpPost("public-send")]
        public async Task<IActionResult> SendPublicEmail(
            [FromBody] PublicSendEmailDto dto,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            cancellationToken.ThrowIfCancellationRequested();

            var normalizedSubject = string.IsNullOrWhiteSpace(dto.Subject)
                ? "Message from onlineStore"
                : dto.Subject.Trim();

            var safeMessage = WebUtility.HtmlEncode(dto.Message.Trim())
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace("\n", "<br />", StringComparison.Ordinal);

            var htmlBody = $"""
                <!DOCTYPE html>
                <html lang="en">
                <head>
                    <meta charset="utf-8">
                    <meta name="viewport" content="width=device-width, initial-scale=1.0">
                    <title>{WebUtility.HtmlEncode(normalizedSubject)}</title>
                </head>
                <body style="margin:0;padding:24px;background-color:#f4f6f8;font-family:Arial,Helvetica,sans-serif;color:#1f2937;">
                    <div style="max-width:600px;margin:0 auto;background:#ffffff;border:1px solid #e5e7eb;border-radius:12px;padding:32px;">
                        <h1 style="margin-top:0;font-size:24px;">{WebUtility.HtmlEncode(normalizedSubject)}</h1>
                        <p style="margin:0;font-size:16px;line-height:1.8;">{safeMessage}</p>
                    </div>
                </body>
                </html>
                """;

            _logger.LogWarning(
                "Anonymous public email endpoint used for recipient {Recipient}. Subject: {Subject}",
                dto.To,
                normalizedSubject);

            return await SendEmailInternalAsync(
                dto.To,
                normalizedSubject,
                htmlBody,
                "Public email sent successfully.",
                cancellationToken);
        }

        [HttpPost("send-welcome")]
        public async Task<IActionResult> SendWelcomeEmail(
            [FromBody] SendWelcomeEmailDto dto,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            cancellationToken.ThrowIfCancellationRequested();

            var template = EmailTemplateBuilder.BuildWelcomeEmail(dto.FirstName);

            return await SendEmailInternalAsync(
                dto.To,
                template.Subject,
                template.HtmlBody,
                "Welcome email sent successfully.",
                cancellationToken);
        }

        [HttpPost("send-verification-code")]
        public async Task<IActionResult> SendVerificationCodeEmail(
            [FromBody] SendCodeEmailDto dto,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            cancellationToken.ThrowIfCancellationRequested();

            var template = EmailTemplateBuilder.BuildEmailVerificationCodeEmail(
                dto.FirstName ?? DefaultTestFirstName,
                dto.Code);

            return await SendEmailInternalAsync(
                dto.To,
                template.Subject,
                template.HtmlBody,
                "Verification code email sent successfully.",
                cancellationToken);
        }

        [HttpPost("send-password-reset-code")]
        public async Task<IActionResult> SendPasswordResetCodeEmail(
            [FromBody] SendCodeEmailDto dto,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            cancellationToken.ThrowIfCancellationRequested();

            var template = EmailTemplateBuilder.BuildPasswordResetCodeEmail(
                dto.FirstName ?? DefaultTestFirstName,
                dto.Code);

            return await SendEmailInternalAsync(
                dto.To,
                template.Subject,
                template.HtmlBody,
                "Password reset code email sent successfully.",
                cancellationToken);
        }

        [HttpPost("test-send/welcome")]
        public async Task<IActionResult> SendWelcomeTestEmail(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var template = EmailTemplateBuilder.BuildWelcomeEmail(DefaultTestFirstName);

            return await SendEmailInternalAsync(
                TestRecipientEmail,
                template.Subject,
                template.HtmlBody,
                "Welcome test email sent successfully.",
                cancellationToken,
                new
                {
                    recipient = TestRecipientEmail,
                    firstName = DefaultTestFirstName
                });
        }

        [HttpPost("test-send/verification-code")]
        public async Task<IActionResult> SendVerificationCodeTestEmail(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var template = EmailTemplateBuilder.BuildEmailVerificationCodeEmail(
                DefaultTestFirstName,
                DefaultTestCode);

            return await SendEmailInternalAsync(
                TestRecipientEmail,
                template.Subject,
                template.HtmlBody,
                "Verification code test email sent successfully.",
                cancellationToken,
                new
                {
                    recipient = TestRecipientEmail,
                    firstName = DefaultTestFirstName,
                    code = DefaultTestCode
                });
        }

        [HttpPost("test-send/password-reset-code")]
        public async Task<IActionResult> SendPasswordResetCodeTestEmail(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var template = EmailTemplateBuilder.BuildPasswordResetCodeEmail(
                DefaultTestFirstName,
                DefaultTestCode);

            return await SendEmailInternalAsync(
                TestRecipientEmail,
                template.Subject,
                template.HtmlBody,
                "Password reset code test email sent successfully.",
                cancellationToken,
                new
                {
                    recipient = TestRecipientEmail,
                    firstName = DefaultTestFirstName,
                    code = DefaultTestCode
                });
        }

        private async Task<IActionResult> SendEmailInternalAsync(
            string to,
            string subject,
            string htmlBody,
            string successMessage,
            CancellationToken cancellationToken,
            object? additionalData = null)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                await _emailService.SendEmailAsync(
                    to,
                    subject,
                    htmlBody);

                _logger.LogInformation(
                    "Email endpoint sent email successfully to {Recipient}. Subject: {Subject}",
                    to,
                    subject);

                return Ok(BuildSuccessResponse(
                    successMessage,
                    to,
                    subject,
                    additionalData));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Email endpoint request was canceled for recipient {Recipient}.",
                    to);

                return StatusCode(StatusCodes.Status499ClientClosedRequest, new
                {
                    message = "Email request was canceled.",
                    recipient = to
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Email endpoint failed to send email to {Recipient}. Subject: {Subject}",
                    to,
                    subject);

                return StatusCode(500, new
                {
                    message = "Failed to send email.",
                    recipient = to,
                    subject,
                    detail = _environment.IsDevelopment()
                        ? GetExceptionDetails(ex)
                        : null
                });
            }
        }

        private static object BuildSuccessResponse(
            string message,
            string recipient,
            string subject,
            object? additionalData = null)
        {
            if (additionalData == null)
            {
                return new
                {
                    message,
                    recipient,
                    subject
                };
            }

            return new
            {
                message,
                recipient,
                subject,
                data = additionalData
            };
        }

        private static string GetExceptionDetails(Exception exception)
        {
            var current = exception;

            while (current.InnerException != null)
            {
                current = current.InnerException;
            }

            return current.Message;
        }
    }
}
