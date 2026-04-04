namespace onlineStore.Services.Email
{
    public interface IEmailService
    {
        Task SendEmailAsync(
            string to,
            string subject,
            string htmlBody);
    }
}
