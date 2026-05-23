using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using onlineStore.DTOs.Auth;
using onlineStore.Services.AuthServices;
using onlineStore.Services.Store;
using System.Security.Claims;

namespace onlineStore.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private const string GoogleStoreContextRequired = "store_context_required";
        private const string GoogleStoreContextMissing = "store_context_missing";
        private const string GoogleAuthFailed = "google_auth_failed";
        private const string GoogleEmailNotFound = "email_not_found";
        private const string GoogleRedirectBuildFailed = "redirect_build_failed";
        private const string GoogleUnexpectedError = "unexpected_error";

        private const string FrontendSuccessPathFallback = "/auth/google/success";
        private const string FrontendFailurePathFallback = "/auth/google/failure";

        private readonly IAuthService _authService;
        private readonly IStorefrontOriginService _storefrontOriginService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IAuthService authService,
            IStorefrontOriginService storefrontOriginService,
            IConfiguration configuration,
            ILogger<AuthController> logger)
        {
            _authService = authService;
            _storefrontOriginService = storefrontOriginService;
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
        public IActionResult GoogleLogin(
            [FromQuery] string? storeSlug = null,
            [FromQuery] Guid? storeId = null,
            [FromQuery] string? redirectTo = null,
            [FromQuery] string? frontendOrigin = null)
        {
            var normalizedFrontendOrigin = NormalizeFrontendOrigin(frontendOrigin);
            _logger.LogInformation(
                "[GoogleLoginEndpoint] Google login endpoint hit. HasStoreId: {HasStoreId}, StoreId: {StoreId}, HasStoreSlug: {HasStoreSlug}, StoreSlug: {StoreSlug}, HasRedirectTo: {HasRedirectTo}, RedirectTo: {RedirectTo}, HasFrontendOrigin: {HasFrontendOrigin}, FrontendOrigin: {FrontendOrigin}",
                storeId.HasValue,
                storeId,
                !string.IsNullOrWhiteSpace(storeSlug),
                storeSlug,
                !string.IsNullOrWhiteSpace(redirectTo),
                redirectTo,
                !string.IsNullOrWhiteSpace(normalizedFrontendOrigin),
                normalizedFrontendOrigin);

            if (string.IsNullOrWhiteSpace(storeSlug) && !storeId.HasValue)
            {
                _logger.LogWarning("[GoogleLoginEndpoint] Google login was called without store context.");
                return Redirect(BuildFrontendFailureRedirect(
                    GoogleStoreContextRequired,
                    "Store context is required for storefront Google auth.",
                    frontendOrigin: normalizedFrontendOrigin));
            }

            var redirectUri = Url.Action(nameof(GoogleCallback), "Auth");

            if (string.IsNullOrWhiteSpace(redirectUri))
            {
                _logger.LogError("[GoogleLoginEndpoint] Failed to build Google callback redirect URI.");
                return Redirect(BuildFrontendFailureRedirect(
                    GoogleRedirectBuildFailed,
                    "Could not build Google callback redirect URI.",
                    frontendOrigin: normalizedFrontendOrigin));
            }

            _logger.LogInformation("[GoogleLoginEndpoint] Callback redirect URI built: {RedirectUri}", redirectUri);

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

            if (!string.IsNullOrWhiteSpace(normalizedFrontendOrigin))
                properties.Items["frontendOrigin"] = normalizedFrontendOrigin;

            _logger.LogInformation(
                "[GoogleLoginEndpoint] Challenge ready with preserved context. HasStoreId: {HasStoreId}, StoreId: {StoreId}, HasStoreSlug: {HasStoreSlug}, StoreSlug: {StoreSlug}, HasRedirectTo: {HasRedirectTo}, RedirectTo: {RedirectTo}, HasFrontendOrigin: {HasFrontendOrigin}, FrontendOrigin: {FrontendOrigin}, Items: {Items}",
                storeId.HasValue,
                storeId,
                !string.IsNullOrWhiteSpace(storeSlug),
                storeSlug,
                !string.IsNullOrWhiteSpace(redirectTo),
                redirectTo,
                !string.IsNullOrWhiteSpace(normalizedFrontendOrigin),
                normalizedFrontendOrigin,
                properties.Items);

            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet("google-callback")]
        [AllowAnonymous]
        public async Task<IActionResult> GoogleCallback(
            [FromQuery] string? remoteError = null,
            [FromQuery] string? frontendOrigin = null)
        {
            _logger.LogInformation(
                "[GoogleCallback] Callback started. TraceIdentifier: {TraceIdentifier}, RemoteErrorExists: {HasRemoteError}, RemoteError: {RemoteError}",
                HttpContext.TraceIdentifier,
                !string.IsNullOrWhiteSpace(remoteError),
                remoteError);

            Guid? storeId = null;
            string? storeSlug = null;
            string? redirectTo = null;
            frontendOrigin = NormalizeFrontendOrigin(frontendOrigin);

            try
            {
                if (!string.IsNullOrWhiteSpace(remoteError))
                {
                    _logger.LogWarning("[GoogleCallback] Google remote error received: {RemoteError}", remoteError);
                    return Redirect(BuildFrontendFailureRedirect(
                        GoogleAuthFailed,
                        remoteError,
                        frontendOrigin: frontendOrigin));
                }

                var result = await HttpContext.AuthenticateAsync(
                    IdentityConstants.ExternalScheme);

                _logger.LogInformation(
                    "[GoogleCallback] External authentication completed. Succeeded: {Succeeded}, HasPrincipal: {HasPrincipal}",
                    result.Succeeded,
                    result.Principal != null);

                if (!result.Succeeded || result.Principal == null)
                {
                    _logger.LogWarning("[GoogleCallback] External authentication failed or principal is null.");
                    return Redirect(BuildFrontendFailureRedirect(GoogleAuthFailed, "Google authentication did not succeed."));
                }

                var email = FindClaimValue(result.Principal, ClaimTypes.Email, "email");
                var firstName = FindClaimValue(result.Principal, ClaimTypes.GivenName, "given_name");
                var lastName = FindClaimValue(result.Principal, ClaimTypes.Surname, "family_name");

                var storeIdValue = GetPropertyItem(result, "storeId");
                storeSlug = GetPropertyItem(result, "storeSlug");
                redirectTo = GetPropertyItem(result, "redirectTo");
                frontendOrigin =
                    NormalizeFrontendOrigin(GetPropertyItem(result, "frontendOrigin")) ??
                    frontendOrigin;

                _logger.LogInformation(
                    "[GoogleCallback] Raw Google auth properties. HasStoreIdItem: {HasStoreIdItem}, StoreIdItem: {StoreIdItem}, HasStoreSlugItem: {HasStoreSlugItem}, StoreSlugItem: {StoreSlugItem}, HasRedirectToItem: {HasRedirectToItem}, RedirectToItem: {RedirectToItem}, HasFrontendOriginItem: {HasFrontendOriginItem}, FrontendOriginItem: {FrontendOriginItem}, Items: {Items}",
                    !string.IsNullOrWhiteSpace(storeIdValue),
                    storeIdValue,
                    !string.IsNullOrWhiteSpace(storeSlug),
                    storeSlug,
                    !string.IsNullOrWhiteSpace(redirectTo),
                    redirectTo,
                    !string.IsNullOrWhiteSpace(frontendOrigin),
                    frontendOrigin,
                    result.Properties?.Items);

                if (!string.IsNullOrWhiteSpace(storeIdValue) && Guid.TryParse(storeIdValue, out var parsedStoreId))
                    storeId = parsedStoreId;

                _logger.LogInformation(
                    "[GoogleCallback] Preserved context restored. HasStoreId: {HasStoreId}, StoreId: {StoreId}, HasStoreSlug: {HasStoreSlug}, StoreSlug: {StoreSlug}, HasRedirectTo: {HasRedirectTo}, RedirectTo: {RedirectTo}, HasFrontendOrigin: {HasFrontendOrigin}, FrontendOrigin: {FrontendOrigin}",
                    storeId.HasValue,
                    storeId,
                    !string.IsNullOrWhiteSpace(storeSlug),
                    storeSlug,
                    !string.IsNullOrWhiteSpace(redirectTo),
                    redirectTo,
                    !string.IsNullOrWhiteSpace(frontendOrigin),
                    frontendOrigin);

                if (storeId == null && string.IsNullOrWhiteSpace(storeSlug))
                {
                    _logger.LogWarning("[GoogleCallback] Google callback completed without preserved store context.");
                    return Redirect(BuildFrontendFailureRedirect(
                        GoogleStoreContextMissing,
                        "Google callback lost store context.",
                        frontendOrigin: frontendOrigin));
                }

                if (string.IsNullOrWhiteSpace(email))
                {
                    _logger.LogWarning("[GoogleCallback] Google callback completed without an email claim.");
                    return Redirect(BuildFrontendFailureRedirect(
                        GoogleEmailNotFound,
                        "Google account email was not provided.",
                        storeId,
                        storeSlug,
                        redirectTo,
                        frontendOrigin));
                }

                var dto = new GoogleAuthDto
                {
                    Email = email.Trim().ToLowerInvariant(),
                    FirstName = firstName ?? "",
                    LastName = lastName ?? "",
                    StoreId = storeId,
                    StoreSlug = storeSlug,
                    RedirectTo = redirectTo
                };

                _logger.LogInformation(
                    "[GoogleCallback] Dispatching Google auth service call. Email: {Email}, StoreId: {StoreId}, StoreSlug: {StoreSlug}",
                    dto.Email,
                    dto.StoreId,
                    dto.StoreSlug);

                var authResult = await _authService.GoogleLoginAsync(dto);

                _logger.LogInformation(
                    "[GoogleCallback] Auth service returned. Success: {Success}, HasToken: {HasToken}, ErrorCode: {ErrorCode}, StoreId: {StoreId}, StoreSlug: {StoreSlug}",
                    authResult.Success,
                    !string.IsNullOrWhiteSpace(authResult.Token),
                    authResult.ErrorCode,
                    authResult.StoreId,
                    authResult.StoreSlug);

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
                        authResult.RedirectTo ?? redirectTo,
                        frontendOrigin));
                }

                var successRedirect = BuildFrontendSuccessRedirect(authResult, frontendOrigin);
                _logger.LogInformation("[GoogleCallback] Success redirect generated. RedirectUrl: {RedirectUrl}", successRedirect);

                return Redirect(successRedirect);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GoogleCallback] Unexpected error during Google callback flow");
                return Redirect(BuildFrontendFailureRedirect(
                    GoogleUnexpectedError,
                    "Unexpected error during Google callback flow.",
                    storeId,
                    storeSlug,
                    redirectTo,
                    frontendOrigin));
            }
            finally
            {
                await TrySignOutExternalSchemeAsync();
            }
        }

        private string BuildFrontendSuccessRedirect(
            AuthResponseDto authResult,
            string? frontendOrigin = null)
        {
            var successUrl = ResolveFrontendUrl(
                "FrontendSettings:GoogleAuthSuccessRedirectPath",
                FrontendSuccessPathFallback,
                "success",
                frontendOrigin);

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

            _logger.LogInformation(
                "[BuildFrontendSuccessRedirect] Success redirect built. HasToken: {HasToken}, StoreCustomerId: {StoreCustomerId}, AccountType: {AccountType}, StoreId: {StoreId}, StoreSlug: {StoreSlug}, AuthMode: {AuthMode}, SessionScope: {SessionScope}, Dashboard: {Dashboard}, RedirectToExists: {HasRedirectTo}",
                !string.IsNullOrWhiteSpace(authResult.Token),
                authResult.StoreCustomerId,
                authResult.AccountType,
                authResult.StoreId,
                authResult.StoreSlug,
                authResult.AuthMode,
                authResult.SessionScope,
                authResult.Dashboard,
                !string.IsNullOrWhiteSpace(authResult.RedirectTo));

            return $"{successUrl}#{fragment}";
        }

        private string BuildFrontendFailureRedirect(
            string errorCode,
            string? message = null,
            Guid? storeId = null,
            string? storeSlug = null,
            string? redirectTo = null,
            string? frontendOrigin = null)
        {
            var failureUrl = ResolveFrontendUrl(
                "FrontendSettings:GoogleAuthFailureRedirectPath",
                FrontendFailurePathFallback,
                "failure",
                frontendOrigin);

            var query = new Dictionary<string, string?>
            {
                ["errorCode"] = errorCode,
                ["message"] = message,
                ["storeId"] = storeId?.ToString(),
                ["storeSlug"] = storeSlug,
                ["redirectTo"] = redirectTo
            };

            _logger.LogWarning(
                "[BuildFrontendFailureRedirect] Failure redirect built. ErrorCode: {ErrorCode}, Message: {Message}, StoreId: {StoreId}, StoreSlug: {StoreSlug}, RedirectToExists: {HasRedirectTo}",
                errorCode,
                message,
                storeId,
                storeSlug,
                !string.IsNullOrWhiteSpace(redirectTo));

            return QueryHelpers.AddQueryString(failureUrl, query!);
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

        private string ResolveFrontendUrl(
            string pathSettingKey,
            string fallbackPath,
            string redirectKind,
            string? preferredOrigin = null)
        {
            var baseUrl = _configuration["FrontendSettings:BaseUrl"];
            var configuredPath = _configuration[pathSettingKey];
            var pathToUse = string.IsNullOrWhiteSpace(configuredPath) ? fallbackPath : configuredPath;
            var normalizedPreferredOrigin = NormalizeFrontendOrigin(preferredOrigin);

            if (!string.IsNullOrWhiteSpace(normalizedPreferredOrigin))
            {
                if (IsAllowedFrontendOrigin(normalizedPreferredOrigin))
                {
                    baseUrl = normalizedPreferredOrigin;
                    _logger.LogInformation(
                        "[ResolveFrontendUrl] Using preserved frontend origin for {RedirectKind} redirect. FrontendOrigin: {FrontendOrigin}",
                        redirectKind,
                        normalizedPreferredOrigin);
                }
                else
                {
                    _logger.LogWarning(
                        "[ResolveFrontendUrl] Ignoring untrusted frontend origin for {RedirectKind} redirect. FrontendOrigin: {FrontendOrigin}",
                        redirectKind,
                        normalizedPreferredOrigin);
                }
            }

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl = $"{Request.Scheme}://{Request.Host}";
                _logger.LogWarning(
                    "[ResolveFrontendUrl] Frontend BaseUrl missing while building {RedirectKind} redirect. Falling back to request host base: {FallbackBaseUrl}",
                    redirectKind,
                    baseUrl);
            }

            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                _logger.LogWarning(
                    "[ResolveFrontendUrl] Frontend path setting {SettingKey} missing while building {RedirectKind} redirect. Using fallback path: {FallbackPath}",
                    pathSettingKey,
                    redirectKind,
                    fallbackPath);
            }

            return CombineUrl(baseUrl, pathToUse);
        }

        private bool IsAllowedFrontendOrigin(string? origin)
        {
            return _storefrontOriginService.IsAllowedOrigin(origin);
        }

        private static string? NormalizeFrontendOrigin(string? origin)
        {
            if (string.IsNullOrWhiteSpace(origin))
                return null;

            if (!Uri.TryCreate(origin.Trim(), UriKind.Absolute, out var parsedOrigin))
                return null;

            if (!string.Equals(parsedOrigin.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(parsedOrigin.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return $"{parsedOrigin.Scheme}://{parsedOrigin.Authority}".TrimEnd('/');
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
                _logger.LogDebug("[GoogleCallback] External sign-out completed.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[GoogleCallback] External sign-out failed after callback completion.");
            }
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
