using onlineStore.Models;

namespace onlineStore.Services.StoreCustomerAuth
{
    public interface IStoreCustomerEmailWorkflowService
    {
        string PrepareEmailVerification(StoreCustomer customer);
        string PreparePasswordReset(StoreCustomer customer);
        bool IsValidEmailVerificationCode(StoreCustomer customer, string code);
        bool IsValidPasswordResetCode(StoreCustomer customer, string code);
        void ConfirmEmail(StoreCustomer customer);
        void ClearPasswordReset(StoreCustomer customer);
        Task<bool> SendEmailVerificationCodeAsync(StoreCustomer customer, string code, string source);
        Task<bool> SendPasswordResetCodeAsync(StoreCustomer customer, string code, string source);
        Task<bool> SendPasswordResetConfirmationAsync(StoreCustomer customer, string source);
        Task<bool> SendWelcomeEmailAsync(StoreCustomer customer, string source);
    }
}
