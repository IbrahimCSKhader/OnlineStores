using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using onlineStore.DTOs.Auth;
using onlineStore.DTOs.StoreCustomerAuth;
using onlineStore.Security;
using onlineStore.Services.AuthServices;
using onlineStore.Services.StoreCustomerAuth;
using System.Security.Claims;

namespace onlineStore.Controllers
{
    [ApiController]
    [Route("api/store-customer-auth")]
    public class StoreCustomerAuthController : ControllerBase
    {
        private const string GoogleStoreContextRequired = "store_context_required";
        private const string GoogleStoreContextMissing = "store_context_missing";
        private const string GoogleAuthFailed = "google_auth_failed";
        private const string GoogleEmailNotFound = "email_not_found";
        private const string GoogleRedirectBuildFailed = "redirect_build_failed";
        private const string GoogleUnexpectedError = "unexpected_error";

        private const string FrontendSuccessPathFallback = "/auth/google/success";
        private const string FrontendFailurePathFallback = "/auth/google/failure";

        private readonly IStoreCustomerAuthService _storeCustomerAuthService;
        private readonly IAuthService _authService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<StoreCustomerAuthController> _logger;

        public StoreCustomerAuthController(
            IStoreCustomerAuthService storeCustomerAuthService,
            IAuthService authService,
            IConfiguration configuration,
            ILogger<StoreCustomerAuthController> logger)
        {
            _storeCustomerAuthService = storeCustomerAuthService;
            _authService = authService;
            _configuration = configuration;
            _logger = logger;
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

            var result = await _storeCustomerAuthService.LoginToStoreAsync(dto.StoreId, dto.Email, dto.Password);

            if (result.Success)
                return Ok(result);

            if (result.RequiresEmailVerification)
                return Unauthorized(result);

            if (result.IsForbidden)
                return StatusCode(StatusCodes.Status403Forbidden, new { message = result.Message });

            return Unauthorized(new { message = result.Message });
        }

        [HttpPost("store/{storeId}/login")]
        public async Task<IActionResult> LoginToStore(Guid storeId, [FromBody] StoreScopedCustomerLoginDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _storeCustomerAuthService.LoginToStoreAsync(storeId, dto.Email, dto.Password);

            if (result.Success)
                return Ok(result);

            if (result.RequiresEmailVerification)
                return Unauthorized(result);

            if (result.IsForbidden)
                return StatusCode(StatusCodes.Status403Forbidden, new { message = result.Message });

            return Unauthorized(new { message = result.Message });
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

        [HttpGet("google")]
        [AllowAnonymous]
        public IActionResult GoogleLogin(
            [FromQuery] string? storeSlug = null,
            [FromQuery] Guid? storeId = null,
            [FromQuery] string? redirectTo = null)
        {
            _logger.LogInformation(
                "[StoreCustomerGoogleLogin] Google login endpoint hit. HasStoreId: {HasStoreId}, StoreId: {StoreId}, HasStoreSlug: {HasStoreSlug}, StoreSlug: {StoreSlug}, HasRedirectTo: {HasRedirectTo}, RedirectTo: {RedirectTo}",
                storeId.HasValue,
                storeId,
                !string.IsNullOrWhiteSpace(storeSlug),
                storeSlug,
                !string.IsNullOrWhiteSpace(redirectTo),
                redirectTo);

            if (string.IsNullOrWhiteSpace(storeSlug) && !storeId.HasValue)
            {
                _logger.LogWarning("[StoreCustomerGoogleLogin] Google login was called without store context.");
                return Redirect(BuildFrontendFailureRedirect(GoogleStoreContextRequired, "Store context is required for storefront Google auth."));
            }

            var redirectUri = Url.Action(nameof(GoogleCallback), "StoreCustomerAuth");
            if (string.IsNullOrWhiteSpace(redirectUri))
            {
                _logger.LogError("[StoreCustomerGoogleLogin] Failed to build Google callback redirect URI.");
                return Redirect(BuildFrontendFailureRedirect(GoogleRedirectBuildFailed, "Could not build Google callback redirect URI."));
            }

            var properties = new AuthenticationProperties
            {
                RedirectUri = redirectUri
            };

            if (storeId.HasValue)
                properties.Items["storeId"] = storeId.Value.ToString();

            if (!string.IsNullOrWhiteSpace(storeSlug))
                properties.Items["storeSlug"] = storeSlug.Trim();

            if (!string.IsNullOrWhiteSpace(redirectTo))
                properties.Items["redirectTo"] = redirectTo.Trim();

            _logger.LogInformation(
                "[StoreCustomerGoogleLogin] Challenge ready with preserved context. HasStoreId: {HasStoreId}, StoreId: {StoreId}, HasStoreSlug: {HasStoreSlug}, StoreSlug: {StoreSlug}, HasRedirectTo: {HasRedirectTo}, RedirectTo: {RedirectTo}, Items: {Items}",
                storeId.HasValue,
                storeId,
                !string.IsNullOrWhiteSpace(storeSlug),
                storeSlug,
                !string.IsNullOrWhiteSpace(redirectTo),
                redirectTo,
                properties.Items);

            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet("google-callback")]
        [AllowAnonymous]
        public async Task<IActionResult> GoogleCallback([FromQuery] string? remoteError = null)
        {
            _logger.LogInformation(
                "[StoreCustomerGoogleCallback] Callback started. TraceIdentifier: {TraceIdentifier}, RemoteErrorExists: {HasRemoteError}, RemoteError: {RemoteError}",
                HttpContext.TraceIdentifier,
                !string.IsNullOrWhiteSpace(remoteError),
                remoteError);

            Guid? storeId = null;
            string? storeSlug = null;
            string? redirectTo = null;

            try
            {
                if (!string.IsNullOrWhiteSpace(remoteError))
                {
                    _logger.LogWarning("[StoreCustomerGoogleCallback] Google remote error received: {RemoteError}", remoteError);
                    return Redirect(BuildFrontendFailureRedirect(GoogleAuthFailed, remoteError));
                }

                var result = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);

                _logger.LogInformation(
                    "[StoreCustomerGoogleCallback] External authentication completed. Succeeded: {Succeeded}, HasPrincipal: {HasPrincipal}",
                    result.Succeeded,
                    result.Principal != null);

                if (!result.Succeeded || result.Principal == null)
                {
                    _logger.LogWarning("[StoreCustomerGoogleCallback] External authentication failed or principal is null.");
                    return Redirect(BuildFrontendFailureRedirect(GoogleAuthFailed, "Google authentication did not succeed."));
                }

                var email = FindClaimValue(result.Principal, ClaimTypes.Email, "email");
                var firstName = FindClaimValue(result.Principal, ClaimTypes.GivenName, "given_name");
                var lastName = FindClaimValue(result.Principal, ClaimTypes.Surname, "family_name");

                var storeIdValue = GetPropertyItem(result, "storeId");
                storeSlug = GetPropertyItem(result, "storeSlug");
                redirectTo = GetPropertyItem(result, "redirectTo");

                _logger.LogInformation(
                    "[StoreCustomerGoogleCallback] Raw auth properties. HasStoreIdItem: {HasStoreIdItem}, StoreIdItem: {StoreIdItem}, HasStoreSlugItem: {HasStoreSlugItem}, StoreSlugItem: {StoreSlugItem}, HasRedirectToItem: {HasRedirectToItem}, RedirectToItem: {RedirectToItem}, Items: {Items}",
                    !string.IsNullOrWhiteSpace(storeIdValue),
                    storeIdValue,
                    !string.IsNullOrWhiteSpace(storeSlug),
                    storeSlug,
                    !string.IsNullOrWhiteSpace(redirectTo),
                    redirectTo,
                    result.Properties?.Items);

                if (!string.IsNullOrWhiteSpace(storeIdValue) && Guid.TryParse(storeIdValue, out var parsedStoreId))
                    storeId = parsedStoreId;

                _logger.LogInformation(
                    "[StoreCustomerGoogleCallback] Preserved context restored. HasStoreId: {HasStoreId}, StoreId: {StoreId}, HasStoreSlug: {HasStoreSlug}, StoreSlug: {StoreSlug}, HasRedirectTo: {HasRedirectTo}, RedirectTo: {RedirectTo}",
                    storeId.HasValue,
                    storeId,
                    !string.IsNullOrWhiteSpace(storeSlug),
                    storeSlug,
                    !string.IsNullOrWhiteSpace(redirectTo),
                    redirectTo);

                if (storeId == null && string.IsNullOrWhiteSpace(storeSlug))
                {
                    _logger.LogWarning("[StoreCustomerGoogleCallback] Google callback completed without preserved store context.");
                    return Redirect(BuildFrontendFailureRedirect(GoogleStoreContextMissing, "Google callback lost store context."));
                }

                if (string.IsNullOrWhiteSpace(email))
                {
                    _logger.LogWarning("[StoreCustomerGoogleCallback] Google callback completed without an email claim.");
                    return Redirect(BuildFrontendFailureRedirect(GoogleEmailNotFound, "Google account email was not provided.", storeId, storeSlug, redirectTo));
                }

                var authResult = await _authService.GoogleLoginAsync(new GoogleAuthDto
                {
                    Email = email.Trim().ToLowerInvariant(),
                    FirstName = firstName ?? string.Empty,
                    LastName = lastName ?? string.Empty,
                    StoreId = storeId,
                    StoreSlug = storeSlug,
                    RedirectTo = redirectTo
                });

                _logger.LogInformation(
                    "[StoreCustomerGoogleCallback] Auth service returned. Success: {Success}, HasToken: {HasToken}, ErrorCode: {ErrorCode}, StoreId: {StoreId}, StoreSlug: {StoreSlug}, AccountType: {AccountType}, SessionScope: {SessionScope}, Dashboard: {Dashboard}",
                    authResult.Success,
                    !string.IsNullOrWhiteSpace(authResult.Token),
                    authResult.ErrorCode,
                    authResult.StoreId,
                    authResult.StoreSlug,
                    authResult.AccountType,
                    authResult.SessionScope,
                    authResult.Dashboard);

                if (!authResult.Success || string.IsNullOrWhiteSpace(authResult.Token))
                {
                    var errorCode = string.IsNullOrWhiteSpace(authResult.ErrorCode)
                        ? GoogleAuthFailed
                        : authResult.ErrorCode;

                    return Redirect(BuildFrontendFailureRedirect(
                        errorCode,
                        authResult.Message ?? "Google login failed.",
                        authResult.StoreId ?? storeId,
                        authResult.StoreSlug ?? storeSlug,
                        authResult.RedirectTo ?? redirectTo));
                }

                var successRedirect = BuildFrontendSuccessRedirect(authResult);
                _logger.LogInformation("[StoreCustomerGoogleCallback] Success redirect generated. RedirectUrl: {RedirectUrl}", successRedirect);

                return Redirect(successRedirect);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[StoreCustomerGoogleCallback] Unexpected error during Google callback flow");
                return Redirect(BuildFrontendFailureRedirect(
                    GoogleUnexpectedError,
                    "Unexpected error during Google callback flow.",
                    storeId,
                    storeSlug,
                    redirectTo));
            }
            finally
            {
                await TrySignOutExternalSchemeAsync();
            }
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

        private string BuildFrontendSuccessRedirect(onlineStore.DTOs.Auth.AuthResponseDto authResult)
        {
            var successUrl = ResolveFrontendUrl(
                "FrontendSettings:GoogleAuthSuccessRedirectPath",
                FrontendSuccessPathFallback,
                "success");

            var fragmentParts = new List<string>
            {
                BuildFragmentPair("token", authResult.Token),
                BuildFragmentPair("email", authResult.Email),
                BuildFragmentPair("firstName", authResult.FirstName),
                BuildFragmentPair("lastName", authResult.LastName),
                BuildFragmentPair("storeCustomerId", authResult.StoreCustomerId?.ToString()),
                BuildFragmentPair("accountType", authResult.AccountType),
                BuildFragmentPair("storeId", authResult.StoreId?.ToString()),
                BuildFragmentPair("storeSlug", authResult.StoreSlug),
                BuildFragmentPair("redirectTo", authResult.RedirectTo),
                BuildFragmentPair("authMode", authResult.AuthMode ?? "storefront"),
                BuildFragmentPair("sessionScope", authResult.SessionScope ?? "storefront"),
                BuildFragmentPair("dashboard", authResult.Dashboard ?? "customer")
            };

            var fragment = string.Join("&", fragmentParts.Where(x => !string.IsNullOrWhiteSpace(x)));

            return $"{successUrl}#{fragment}";
        }

        private string BuildFrontendFailureRedirect(
            string errorCode,
            string? message = null,
            Guid? storeId = null,
            string? storeSlug = null,
            string? redirectTo = null)
        {
            var failureUrl = ResolveFrontendUrl(
                "FrontendSettings:GoogleAuthFailureRedirectPath",
                FrontendFailurePathFallback,
                "failure");

            return QueryHelpers.AddQueryString(failureUrl, new Dictionary<string, string?>
            {
                ["errorCode"] = errorCode,
                ["message"] = message,
                ["storeId"] = storeId?.ToString(),
                ["storeSlug"] = storeSlug,
                ["redirectTo"] = redirectTo
            });
        }

        private string ResolveFrontendUrl(string pathSettingKey, string fallbackPath, string redirectKind)
        {
            var baseUrl = _configuration["FrontendSettings:BaseUrl"];
            var configuredPath = _configuration[pathSettingKey];
            var pathToUse = string.IsNullOrWhiteSpace(configuredPath) ? fallbackPath : configuredPath;

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl = $"{Request.Scheme}://{Request.Host}";
                _logger.LogWarning(
                    "[StoreCustomerGoogleCallback] Frontend BaseUrl missing while building {RedirectKind} redirect. Falling back to request host base: {FallbackBaseUrl}",
                    redirectKind,
                    baseUrl);
            }

            return CombineUrl(baseUrl, pathToUse);
        }

        private static string BuildFragmentPair(string key, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return $"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}";
        }

        private async Task TrySignOutExternalSchemeAsync()
        {
            try
            {
                await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[StoreCustomerGoogleCallback] External sign-out failed after callback completion.");
            }
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
