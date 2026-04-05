using onlineStore.DTOs.StoreCustomerAuth;

namespace onlineStore.Services.StoreCustomerAuth
{
    public interface IStoreCustomerAuthService
    {
        Task<StoreCustomerAuthResponseDto> CreateGuestSessionAsync(StoreCustomerGuestSessionDto dto);
        Task<StoreCustomerAuthResponseDto> RegisterAsync(StoreCustomerRegisterDto dto);
        Task<StoreCustomerAuthResponseDto> LoginAsync(StoreCustomerLoginDto dto);
        Task<StoreCustomerAuthResponseDto> VerifyEmailAsync(StoreCustomerVerifyEmailDto dto);
        Task<(bool Success, string Message)> ResendVerificationCodeAsync(StoreCustomerResendVerificationCodeDto dto);
        Task<(bool Success, string Message)> ForgotPasswordAsync(StoreCustomerForgotPasswordDto dto);
        Task<(bool Success, string Message)> ResetPasswordAsync(StoreCustomerResetPasswordDto dto);
    }
}
