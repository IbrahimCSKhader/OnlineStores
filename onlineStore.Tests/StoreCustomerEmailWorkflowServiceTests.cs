using Microsoft.Extensions.Logging.Abstractions;
using onlineStore.Models;
using onlineStore.Services.Email;
using onlineStore.Services.StoreCustomerAuth;

namespace onlineStore.Tests;

public sealed class StoreCustomerEmailWorkflowServiceTests
{
    [Fact]
    public void PrepareEmailVerification_CreatesValidSixDigitCodeAndConfirmEmailClearsIt()
    {
        var customer = CreateCustomer(emailConfirmed: true);
        var service = CreateService();

        var code = service.PrepareEmailVerification(customer);

        Assert.Matches(@"^\d{6}$", code);
        Assert.False(customer.EmailConfirmed);
        Assert.NotNull(customer.EmailVerificationCodeHash);
        Assert.True(customer.EmailVerificationCodeExpiresAt > DateTime.UtcNow);
        Assert.True(service.IsValidEmailVerificationCode(customer, code));
        Assert.False(service.IsValidEmailVerificationCode(customer, "000000"));

        service.ConfirmEmail(customer);

        Assert.True(customer.EmailConfirmed);
        Assert.Null(customer.EmailVerificationCodeHash);
        Assert.Null(customer.EmailVerificationCodeExpiresAt);
        Assert.False(service.IsValidEmailVerificationCode(customer, code));
    }

    [Fact]
    public void PreparePasswordReset_CreatesValidCodeAndClearPasswordResetClearsIt()
    {
        var customer = CreateCustomer();
        var service = CreateService();

        var code = service.PreparePasswordReset(customer);

        Assert.Matches(@"^\d{6}$", code);
        Assert.NotNull(customer.PasswordResetCodeHash);
        Assert.True(customer.PasswordResetCodeExpiresAt > DateTime.UtcNow);
        Assert.True(service.IsValidPasswordResetCode(customer, code));
        Assert.False(service.IsValidPasswordResetCode(customer, "111111"));

        service.ClearPasswordReset(customer);

        Assert.Null(customer.PasswordResetCodeHash);
        Assert.Null(customer.PasswordResetCodeExpiresAt);
        Assert.False(service.IsValidPasswordResetCode(customer, code));
    }

    [Fact]
    public void Codes_AreInvalidAfterExpiration()
    {
        var customer = CreateCustomer();
        var service = CreateService();
        var verificationCode = service.PrepareEmailVerification(customer);
        var resetCode = service.PreparePasswordReset(customer);

        customer.EmailVerificationCodeExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        customer.PasswordResetCodeExpiresAt = DateTime.UtcNow.AddSeconds(-1);

        Assert.False(service.IsValidEmailVerificationCode(customer, verificationCode));
        Assert.False(service.IsValidPasswordResetCode(customer, resetCode));
    }

    [Fact]
    public async Task SendEmailVerificationCodeAsync_SendsVerificationTemplate()
    {
        var emailService = new RecordingEmailService();
        var service = CreateService(emailService);
        var customer = CreateCustomer();

        var sent = await service.SendEmailVerificationCodeAsync(customer, "123456", "test");

        Assert.True(sent);
        var message = Assert.Single(emailService.Messages);
        Assert.Equal(customer.Email, message.To);
        Assert.Contains("verification", message.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("123456", message.HtmlBody);
        Assert.Contains(customer.FirstName, message.HtmlBody);
    }

    [Fact]
    public async Task SendPasswordEmails_SendExpectedTemplates()
    {
        var emailService = new RecordingEmailService();
        var service = CreateService(emailService);
        var customer = CreateCustomer();

        var resetSent = await service.SendPasswordResetCodeAsync(customer, "654321", "test");
        var confirmationSent = await service.SendPasswordResetConfirmationAsync(customer, "test");

        Assert.True(resetSent);
        Assert.True(confirmationSent);
        Assert.Equal(2, emailService.Messages.Count);
        Assert.Contains("password reset", emailService.Messages[0].Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("654321", emailService.Messages[0].HtmlBody);
        Assert.Contains("password was changed", emailService.Messages[1].Subject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendWelcomeEmailAsync_SendsWelcomeTemplate()
    {
        var emailService = new RecordingEmailService();
        var service = CreateService(emailService);
        var customer = CreateCustomer();

        var sent = await service.SendWelcomeEmailAsync(customer, "test");

        Assert.True(sent);
        var message = Assert.Single(emailService.Messages);
        Assert.Equal(customer.Email, message.To);
        Assert.Contains("welcome", message.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(customer.FirstName, message.HtmlBody);
    }

    [Fact]
    public async Task SendEmailAsync_ReturnsFalseWhenRecipientMissingOrSmtpFails()
    {
        var failingEmailService = new RecordingEmailService { ThrowOnSend = true };
        var failingService = CreateService(failingEmailService);
        var customer = CreateCustomer();

        var failedSmtpSend = await failingService.SendEmailVerificationCodeAsync(customer, "123456", "test");

        Assert.False(failedSmtpSend);

        var missingEmailService = new RecordingEmailService();
        var missingEmailWorkflow = CreateService(missingEmailService);
        customer.Email = string.Empty;

        var missingRecipientSend = await missingEmailWorkflow.SendWelcomeEmailAsync(customer, "test");

        Assert.False(missingRecipientSend);
        Assert.Empty(missingEmailService.Messages);
    }

    private static StoreCustomerEmailWorkflowService CreateService(
        IEmailService? emailService = null)
    {
        return new StoreCustomerEmailWorkflowService(
            emailService ?? new RecordingEmailService(),
            NullLogger<StoreCustomerEmailWorkflowService>.Instance);
    }

    private static StoreCustomer CreateCustomer(bool emailConfirmed = false)
    {
        return new StoreCustomer
        {
            Id = Guid.NewGuid(),
            StoreId = Guid.NewGuid(),
            FirstName = "Ibrahim",
            LastName = "Khader",
            Email = "customer@example.com",
            EmailConfirmed = emailConfirmed,
            PasswordHash = "hash",
            IsActive = true
        };
    }

    private sealed class RecordingEmailService : IEmailService
    {
        public List<RecordedEmail> Messages { get; } = new();
        public bool ThrowOnSend { get; set; }

        public Task SendEmailAsync(string to, string subject, string htmlBody)
        {
            if (ThrowOnSend)
                throw new InvalidOperationException("SMTP failed.");

            Messages.Add(new RecordedEmail(to, subject, htmlBody));
            return Task.CompletedTask;
        }
    }

    private sealed record RecordedEmail(
        string To,
        string Subject,
        string HtmlBody);
}
