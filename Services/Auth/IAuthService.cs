using onlineStore.DTOs.Auth;
using onlineStore.Models.Identity;

namespace onlineStore.Services.AuthServices
{
    public interface IAuthService
    {
        Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
        Task<AuthResponseDto> LoginAsync(LoginDto dto);
        Task LogoutAsync(string userId);
        Task<AuthResponseDto> GoogleLoginAsync(GoogleAuthDto dto);
        Task<OwnerResponseDto> CreateOwnerAsync(CreateOwnerDto dto);
    }
}
