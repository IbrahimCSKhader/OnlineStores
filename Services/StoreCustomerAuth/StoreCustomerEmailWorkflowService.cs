using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using onlineStore.Models;
using onlineStore.Services.Email;

namespace onlineStore.Services.StoreCustomerAuth
{
    public class StoreCustomerEmailWorkflowService : IStoreCustomerEmailWorkflowService
    {
        private const int VerificationCodeLifetimeMinutes = 15;
        private const int PasswordResetCodeLifetimeMinutes = 15;

        private readonly IEmailService _emailService;
        private readonly ILogger<StoreCustomerEmailWorkflowService> _logger;

        public StoreCustomerEmailWorkflowService(
            IEmailService emailService,
            ILogger<StoreCustomerEmailWorkflowService> logger)
        {
            _emailService = emailService;
            _logger = logger;
        }

        public string PrepareEmailVerification(StoreCustomer customer)
        {
            var code = GenerateCode();

            customer.EmailConfirmed = false;
            customer.EmailVerificationCodeHash = HashCode(code);
            customer.EmailVerificationCodeExpiresAt = DateTime.UtcNow.AddMinutes(VerificationCodeLifetimeMinutes);

            return code;
        }

        public string PreparePasswordReset(StoreCustomer customer)
        {
            var code = GenerateCode();

            customer.PasswordResetCodeHash = HashCode(code);
            customer.PasswordResetCodeExpiresAt = DateTime.UtcNow.AddMinutes(PasswordResetCodeLifetimeMinutes);

            return code;
        }

        public bool IsValidEmailVerificationCode(StoreCustomer customer, string code) =>
            IsValidCode(customer.EmailVerificationCodeHash, customer.EmailVerificationCodeExpiresAt, code);

        public bool IsValidPasswordResetCode(StoreCustomer customer, string code) =>
            IsValidCode(customer.PasswordResetCodeHash, customer.PasswordResetCodeExpiresAt, code);

        public void ConfirmEmail(StoreCustomer customer)
        {
            customer.EmailConfirmed = true;
            customer.EmailVerificationCodeHash = null;
            customer.EmailVerificationCodeExpiresAt = null;
        }

        public void ClearPasswordReset(StoreCustomer customer)
        {
            customer.PasswordResetCodeHash = null;
            customer.PasswordResetCodeExpiresAt = null;
        }

        public async Task<bool> SendEmailVerificationCodeAsync(StoreCustomer customer, string code, string source) =>
            await SendEmailAsync(
                customer,
                EmailTemplateBuilder.BuildEmailVerificationCodeEmail(customer.FirstName, code),
                "verification code",
                source);

        public async Task<bool> SendPasswordResetCodeAsync(StoreCustomer customer, string code, string source) =>
            await SendEmailAsync(
                customer,
                EmailTemplateBuilder.BuildPasswordResetCodeEmail(customer.FirstName, code),
                "password reset code",
                source);

        public async Task<bool> SendPasswordResetConfirmationAsync(StoreCustomer customer, string source) =>
            await SendEmailAsync(
                customer,
                EmailTemplateBuilder.BuildPasswordResetConfirmationEmail(customer.FirstName),
                "password reset confirmation",
                source);

        public async Task<bool> SendWelcomeEmailAsync(StoreCustomer customer, string source) =>
            await SendEmailAsync(
                customer,
                EmailTemplateBuilder.BuildWelcomeEmail(customer.FirstName),
                "welcome email",
                source);

        private async Task<bool> SendEmailAsync(
            StoreCustomer customer,
            EmailTemplateContent template,
            string emailType,
            string source)
        {
            if (string.IsNullOrWhiteSpace(customer.Email))
            {
                _logger.LogWarning(
                    "Store customer {EmailType} skipped after {Source} because email is missing. StoreCustomerId: {StoreCustomerId}",
                    emailType,
                    source,
                    customer.Id);
                return false;
            }

            try
            {
                await _emailService.SendEmailAsync(customer.Email, template.Subject, template.HtmlBody);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to send store customer {EmailType} to {Email} after {Source}. StoreCustomerId: {StoreCustomerId}",
                    emailType,
                    customer.Email,
                    source,
                    customer.Id);
                return false;
            }
        }

        private static bool IsValidCode(string? storedHash, DateTime? expiresAt, string code)
        {
            if (string.IsNullOrWhiteSpace(storedHash) || !expiresAt.HasValue)
                return false;

            if (expiresAt.Value < DateTime.UtcNow)
                return false;

            return string.Equals(storedHash, HashCode(code), StringComparison.Ordinal);
        }

        private static string GenerateCode() =>
            RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);

        private static string HashCode(string code)
        {
            var normalizedCode = code.Trim();
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedCode));
            return Convert.ToHexString(hash);
        }
    }
}
