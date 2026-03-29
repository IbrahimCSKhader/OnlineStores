using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;// Controllers/AuthController.cs
using Microsoft.AspNetCore.Mvc;
using onlineStore.DTOs.Auth;
using onlineStore.DTOs.Order;
using onlineStore.Services.AuthServices;
using System.Security.Claims;
using onlineStore.Models.Identity;
namespace onlineStore.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.RegisterAsync(dto);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(result);
        }


        
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.LoginAsync(dto);

            if (!result.Success)
                return Unauthorized(new { message = result.Message });

            return Ok(result);
        }


        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (userId == null)
                return Unauthorized();

            await _authService.LogoutAsync(userId);

            return Ok(new { message = "Logged out successfully" });
        }
        [HttpGet("google")]
        public IActionResult GoogleLogin()
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = "/api/auth/google-callback"
            };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }
        [HttpGet("google-callback")]
        public async Task<IActionResult> GoogleCallback()
        {
            var result = await HttpContext.AuthenticateAsync(
                GoogleDefaults.AuthenticationScheme);

            if (!result.Succeeded || result.Principal == null)
                return Unauthorized(new { message = "فشل التحقق من Google" });

            var email = result.Principal.FindFirst(ClaimTypes.Email)?.Value;
            var firstName = result.Principal.FindFirst(ClaimTypes.GivenName)?.Value;
            var lastName = result.Principal.FindFirst(ClaimTypes.Surname)?.Value;

            if (string.IsNullOrEmpty(email))
                return BadRequest(new { message = "لم يتم الحصول على البريد الإلكتروني من Google" });

    
            var dto = new GoogleAuthDto
            {
                Email = email.Trim().ToLower(),
                FirstName = firstName ?? "",
                LastName = lastName ?? ""
            };

            var authResult = await _authService.GoogleLoginAsync(dto);

            if (!authResult.Success)
                return BadRequest(new { message = authResult.Message });

            return Ok(authResult);
        }
        [HttpPost("create-owner")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<ActionResult<OwnerResponseDto>> CreateOwner([FromBody] CreateOwnerDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var owner = await _authService.CreateOwnerAsync(dto);
            return Ok(owner);
        }
    }

}
