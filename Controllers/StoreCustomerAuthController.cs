using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using onlineStore.DTOs.Auth;
using onlineStore.DTOs.StoreCustomerAuth;
using onlineStore.Security;
using onlineStore.Services.AuthServices;
using onlineStore.Services.StoreCustomerAuth;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace onlineStore.Controllers
{
    [ApiController]
    [Route("api/store-customer-auth")]
    public class StoreCustomerAuthController : ControllerBase
    {
        private readonly IStoreCustomerAuthService _storeCustomerAuthService;
        private readonly IAuthService _authService;
        private readonly IStoreAuthorizationService _storeAuthorizationService;

        public StoreCustomerAuthController(
            IStoreCustomerAuthService storeCustomerAuthService,
            IAuthService authService,
            IStoreAuthorizationService storeAuthorizationService)
        {
            _storeCustomerAuthService = storeCustomerAuthService;
            _authService = authService;
            _storeAuthorizationService = storeAuthorizationService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] StoreCustomerRegisterDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _storeCustomerAuthService.RegisterAsync(dto);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(result);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] StoreCustomerLoginDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _storeCustomerAuthService.LoginAsync(dto);

            if (!result.Success)
            {
                if (result.RequiresEmailVerification)
                    return Unauthorized(result);

                return Unauthorized(new { message = result.Message });
            }

            return Ok(result);
        }

        [HttpPost("store/{storeId}/login")]
        public async Task<IActionResult> LoginToStore(Guid storeId, [FromBody] StoreScopedCustomerLoginDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var loginDto = new StoreCustomerLoginDto
            {
                StoreId = storeId,
                Email = dto.Email,
                Password = dto.Password
            };

            var result = await _storeCustomerAuthService.LoginAsync(loginDto);

            if (result.Success)
                return Ok(result);

            if (result.RequiresEmailVerification)
                return Unauthorized(result);

            var ownerLoginResult = await TryStoreOwnerLoginAsync(storeId, dto);
            if (ownerLoginResult.Success && ownerLoginResult.Response != null)
                return Ok(ownerLoginResult.Response);

            if (ownerLoginResult.Forbidden)
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ownerLoginResult.Message });

            return Unauthorized(new { message = ownerLoginResult.Message ?? result.Message });
        }

        private async Task<(bool Success, bool Forbidden, string? Message, AuthResponseDto? Response)> TryStoreOwnerLoginAsync(
            Guid storeId,
            StoreScopedCustomerLoginDto dto)
        {
            var ownerAuth = await _authService.LoginAsync(new LoginDto
            {
                Email = dto.Email,
                Password = dto.Password
            });

            if (!ownerAuth.Success || string.IsNullOrWhiteSpace(ownerAuth.Token))
            {
                return (
                    false,
                    false,
                    ownerAuth.Message ?? "البريد الإلكتروني أو كلمة المرور غير صحيحة",
                    null);
            }

            if (ownerAuth.Roles == null || !ownerAuth.Roles.Contains("StoreOwner"))
            {
                return (
                    false,
                    true,
                    "هذا الدخول مخصص لصاحب المتجر فقط.",
                    null);
            }

            var userIdClaim = new JwtSecurityTokenHandler()
                .ReadJwtToken(ownerAuth.Token)
                .Claims
                .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)
                ?.Value;

            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return (
                    false,
                    false,
                    "تعذر التحقق من هوية صاحب المتجر.",
                    null);
            }

            var canManageStore = await _storeAuthorizationService.CanManageStoreAsync(userId, storeId);
            if (!canManageStore)
            {
                return (
                    false,
                    true,
                    "هذا الحساب ليس صاحب هذا المتجر.",
                    null);
            }

            return (true, false, null, ownerAuth);
        }

        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromBody] StoreCustomerVerifyEmailDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _storeCustomerAuthService.VerifyEmailAsync(dto);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(result);
        }

        [HttpPost("resend-verification-code")]
        public async Task<IActionResult> ResendVerificationCode([FromBody] StoreCustomerResendVerificationCodeDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _storeCustomerAuthService.ResendVerificationCodeAsync(dto);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { message = result.Message });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] StoreCustomerForgotPasswordDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _storeCustomerAuthService.ForgotPasswordAsync(dto);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { message = result.Message });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] StoreCustomerResetPasswordDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _storeCustomerAuthService.ResetPasswordAsync(dto);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { message = result.Message });
        }

        [HttpPost("set-password")]
        [Authorize(Policy = "StoreCustomerOnly")]
        public async Task<IActionResult> SetPassword([FromBody] StoreCustomerSetPasswordDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var storeCustomerId = User.GetStoreCustomerId();
            var storeId = User.GetStoreCustomerStoreId();

            if (!storeCustomerId.HasValue || !storeId.HasValue)
                return Unauthorized(new { message = "Store customer context is missing." });

            var result = await _storeCustomerAuthService.SetPasswordAsync(
                storeCustomerId.Value,
                storeId.Value,
                dto);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { message = result.Message });
        }

        [HttpPost("store/{storeId}/set-password-from-auth-user")]
        [Authorize]
        public async Task<IActionResult> SetPasswordFromAuthenticatedUser(
            Guid storeId,
            [FromBody] StoreCustomerSetPasswordDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(email))
                return Unauthorized(new { message = "Authenticated user email is missing." });

            var result = await _storeCustomerAuthService.SetPasswordByEmailAsync(
                email,
                storeId,
                dto);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { message = result.Message });
        }
    }
}
