using onlineStore.DTOs.Auth;

namespace onlineStore.Services.AuthServices
{
    public interface IAuthService
    {
        Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
        Task<AuthResponseDto> LoginAsync(LoginDto dto);
        Task LogoutAsync(string userId);
        Task<AuthResponseDto> GoogleLoginAsync(GoogleAuthDto dto);
    }
}
