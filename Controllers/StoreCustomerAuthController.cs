using Microsoft.AspNetCore.Mvc;
using onlineStore.DTOs.StoreCustomerAuth;
using onlineStore.Services.StoreCustomerAuth;

namespace onlineStore.Controllers
{
    [ApiController]
    [Route("api/store-customer-auth")]
    public class StoreCustomerAuthController : ControllerBase
    {
        private readonly IStoreCustomerAuthService _storeCustomerAuthService;

        public StoreCustomerAuthController(IStoreCustomerAuthService storeCustomerAuthService)
        {
            _storeCustomerAuthService = storeCustomerAuthService;
        }

        [HttpPost("guest")]
        public async Task<IActionResult> CreateGuestSession([FromBody] StoreCustomerGuestSessionDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _storeCustomerAuthService.CreateGuestSessionAsync(dto);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(result);
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
    }
}
