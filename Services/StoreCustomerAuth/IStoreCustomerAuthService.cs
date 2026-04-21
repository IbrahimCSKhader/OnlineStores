using onlineStore.DTOs.StoreCustomerAuth;

namespace onlineStore.Services.StoreCustomerAuth
{
    public interface IStoreCustomerAuthService
    {
        Task<StoreCustomerAuthResponseDto> RegisterAsync(StoreCustomerRegisterDto dto);
        Task<StoreCustomerAuthResponseDto> LoginAsync(StoreCustomerLoginDto dto);
        Task<StorefrontLoginResponseDto> LoginToStoreAsync(Guid storeId, string email, string password);
        Task<StoreCustomerAuthResponseDto> VerifyEmailAsync(StoreCustomerVerifyEmailDto dto);
        Task<(bool Success, string Message)> ResendVerificationCodeAsync(StoreCustomerResendVerificationCodeDto dto);
        Task<(bool Success, string Message)> ForgotPasswordAsync(StoreCustomerForgotPasswordDto dto);
        Task<(bool Success, string Message)> ResetPasswordAsync(StoreCustomerResetPasswordDto dto);
        Task<(bool Success, string Message)> SetPasswordAsync(
            Guid storeCustomerId,
            Guid storeId,
            StoreCustomerSetPasswordDto dto);
        Task<(bool Success, string Message)> SetPasswordByEmailAsync(
            string email,
            Guid storeId,
            StoreCustomerSetPasswordDto dto);
    }
}
