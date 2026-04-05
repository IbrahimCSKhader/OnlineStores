using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using onlineStore.DTOs.Auth;
using onlineStore.Services.AuthServices;
using System.Security.Claims;

namespace onlineStore.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IAuthService authService,
            IConfiguration configuration,
            ILogger<AuthController> logger)
        {
            _authService = authService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("register")]
        [AllowAnonymous]
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
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.LoginAsync(dto);

            if (!result.Success)
            {
                if (result.RequiresEmailVerification)
                    return Unauthorized(result);

                return Unauthorized(new { message = result.Message });
            }

            return Ok(result);
        }

        [HttpPost("verify-email")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.VerifyEmailAsync(dto);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(result);
        }

        [HttpPost("resend-verification-code")]
        [AllowAnonymous]
        public async Task<IActionResult> ResendVerificationCode([FromBody] ResendVerificationCodeDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.ResendVerificationCodeAsync(dto);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { message = result.Message });
        }

        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.ForgotPasswordAsync(dto);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { message = result.Message });
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.ResetPasswordAsync(dto);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { message = result.Message });
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { message = "المستخدم غير مصرح له" });

            await _authService.LogoutAsync(userId);

            return Ok(new { message = "تم تسجيل الخروج بنجاح" });
        }

        [HttpGet("google")]
        [AllowAnonymous]
        public IActionResult GoogleLogin([FromQuery] string? storeSlug = null, [FromQuery] Guid? storeId = null)
        {
            _logger.LogInformation("Google login endpoint hit. Building challenge...");

            if (string.IsNullOrWhiteSpace(storeSlug) && !storeId.HasValue)
            {
                _logger.LogWarning("Google login was called without store context.");
                return Redirect(BuildFrontendFailureRedirect("store_context_required"));
            }

            var redirectUri = Url.Action(nameof(GoogleCallback), "Auth");
            _logger.LogInformation("Redirect URI will be: {RedirectUri}", redirectUri);

            var properties = new AuthenticationProperties
            {
                RedirectUri = redirectUri
            };

            if (storeId.HasValue)
                properties.Items["storeId"] = storeId.Value.ToString();

            if (!string.IsNullOrWhiteSpace(storeSlug))
                properties.Items["storeSlug"] = storeSlug.Trim();

            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet("google-callback")]
        [AllowAnonymous]
        public async Task<IActionResult> GoogleCallback([FromQuery] string? remoteError = null)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(remoteError))
                {
                    _logger.LogWarning("Google remote error received: {RemoteError}", remoteError);
                    return Redirect(BuildFrontendFailureRedirect(remoteError));
                }

                var result = await HttpContext.AuthenticateAsync(
                    IdentityConstants.ExternalScheme);

                if (!result.Succeeded || result.Principal == null)
                {
                    await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
                    return Redirect(BuildFrontendFailureRedirect("google_auth_failed"));
                }

                var email = FindClaimValue(result.Principal, ClaimTypes.Email, "email");
                var firstName = FindClaimValue(result.Principal, ClaimTypes.GivenName, "given_name");
                var lastName = FindClaimValue(result.Principal, ClaimTypes.Surname, "family_name");

                var storeIdValue = GetPropertyItem(result, "storeId");
                var storeSlug = GetPropertyItem(result, "storeSlug");

                Guid? storeId = null;
                if (!string.IsNullOrWhiteSpace(storeIdValue) && Guid.TryParse(storeIdValue, out var parsedStoreId))
                    storeId = parsedStoreId;

                if (storeId == null && string.IsNullOrWhiteSpace(storeSlug))
                {
                    await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
                    _logger.LogWarning("Google callback completed without preserved store context.");
                    return Redirect(BuildFrontendFailureRedirect("store_context_missing"));
                }

                if (string.IsNullOrWhiteSpace(email))
                {
                    await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
                    return Redirect(BuildFrontendFailureRedirect("email_not_found"));
                }

                var dto = new GoogleAuthDto
                {
                    Email = email.Trim().ToLowerInvariant(),
                    FirstName = firstName ?? "",
                    LastName = lastName ?? "",
                    StoreId = storeId,
                    StoreSlug = storeSlug
                };

                var authResult = await _authService.GoogleLoginAsync(dto);

                await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

                if (!authResult.Success || string.IsNullOrWhiteSpace(authResult.Token))
                {
                    return Redirect(BuildFrontendFailureRedirect(authResult.Message ?? "login_failed"));
                }

                return Redirect(BuildFrontendSuccessRedirect(authResult));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during Google callback flow");
                return Redirect(BuildFrontendFailureRedirect("unexpected_error"));
            }
        }

        private string BuildFrontendSuccessRedirect(AuthResponseDto authResult)
        {
            var baseUrl = _configuration["FrontendSettings:BaseUrl"];
            var successPath = _configuration["FrontendSettings:GoogleAuthSuccessRedirectPath"];

            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(successPath))
            {
                _logger.LogError(
                    "Frontend settings for Google success redirect are missing. BaseUrl: {BaseUrlExists}, SuccessPath: {PathExists}",
                    !string.IsNullOrWhiteSpace(baseUrl),
                    !string.IsNullOrWhiteSpace(successPath));

                throw new InvalidOperationException("Google frontend redirect settings are not configured.");
            }

            var successUrl = CombineUrl(baseUrl, successPath);

            var fragment = string.Join("&", new[]
            {
                $"token={Uri.EscapeDataString(authResult.Token ?? string.Empty)}",
                $"email={Uri.EscapeDataString(authResult.Email ?? string.Empty)}",
                $"firstName={Uri.EscapeDataString(authResult.FirstName ?? string.Empty)}",
                $"lastName={Uri.EscapeDataString(authResult.LastName ?? string.Empty)}"
            });

            return $"{successUrl}#{fragment}";
        }

        private string BuildFrontendFailureRedirect(string message)
        {
            var baseUrl = _configuration["FrontendSettings:BaseUrl"];
            var failurePath = _configuration["FrontendSettings:GoogleAuthFailureRedirectPath"];

            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(failurePath))
            {
                _logger.LogError(
                    "Frontend settings for Google failure redirect are missing. BaseUrl: {BaseUrlExists}, FailurePath: {PathExists}",
                    !string.IsNullOrWhiteSpace(baseUrl),
                    !string.IsNullOrWhiteSpace(failurePath));

                throw new InvalidOperationException("Google frontend redirect settings are not configured.");
            }

            var failureUrl = CombineUrl(baseUrl, failurePath);
            return QueryHelpers.AddQueryString(failureUrl, "message", message);
        }

        private static string? FindClaimValue(ClaimsPrincipal principal, params string[] claimTypes)
        {
            foreach (var claimType in claimTypes)
            {
                var value = principal.FindFirst(claimType)?.Value;
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return null;
        }

        private static string CombineUrl(string baseUrl, string path)
        {
            var normalizedBase = baseUrl.TrimEnd('/');
            var normalizedPath = path.Trim();

            if (!normalizedPath.StartsWith('/'))
                normalizedPath = "/" + normalizedPath;

            return normalizedBase + normalizedPath;
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

        [HttpPut("admin/change-password")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> ChangeUserPasswordBySuperAdmin([FromBody] AdminChangeUserPasswordDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.ChangeUserPasswordBySuperAdminAsync(dto.UserId, dto.NewPassword);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { message = result.Message });
        }

        private static string? GetPropertyItem(AuthenticateResult result, string key)
        {
            if (result.Properties?.Items == null)
                return null;

            return result.Properties.Items.TryGetValue(key, out var value)
                ? value
                : null;
        }
    }
}