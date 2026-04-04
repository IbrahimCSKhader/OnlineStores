using onlineStore.DTOs.Auth;
using onlineStore.Models.Identity;

namespace onlineStore.Services.AuthServices
{
    public interface IAuthService
    {
        Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
        Task<AuthResponseDto> LoginAsync(LoginDto dto);
        Task<AuthResponseDto> VerifyEmailAsync(VerifyEmailDto dto);
        Task LogoutAsync(string userId);
        Task<(bool Success, string Message)> ResendVerificationCodeAsync(
            ResendVerificationCodeDto dto);
        Task<(bool Success, string Message)> ForgotPasswordAsync(ForgotPasswordDto dto);
        Task<(bool Success, string Message)> ResetPasswordAsync(ResetPasswordDto dto);
        Task<AuthResponseDto> GoogleLoginAsync(GoogleAuthDto dto);
        Task<OwnerResponseDto> CreateOwnerAsync(CreateOwnerDto dto);
        Task<(bool Success, string Message)> ChangeUserPasswordBySuperAdminAsync(
            Guid userId, string newPassword);
    }
}
