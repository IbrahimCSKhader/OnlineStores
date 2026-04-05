using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using onlineStore.Data;
using onlineStore.DTOs.Auth;
using onlineStore.Models;
using onlineStore.Models.Identity;
using onlineStore.Services.Email;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace onlineStore.Services.AuthServices
{
    public class AuthService : IAuthService
    {
        private const string GenericForgotPasswordMessage =
            "إذا كان البريد الإلكتروني مسجلاً، فسيتم إرسال كود إعادة تعيين كلمة المرور.";

        private const string StoreCustomerRegisterMessage =
            "تسجيل العملاء أصبح عبر /api/store-customer-auth/register";

        private const string StoreCustomerLoginMessage =
            "هذا الحساب مخصص لعملاء المتاجر. استخدم /api/store-customer-auth/login";

        private const string DefaultCustomerRole = "Customer";

        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _context;
        private readonly IPasswordHasher<StoreCustomer> _storeCustomerPasswordHasher;
        private readonly IEmailService _emailService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            IConfiguration configuration,
            AppDbContext context,
            IPasswordHasher<StoreCustomer> storeCustomerPasswordHasher,
            IEmailService emailService,
            ILogger<AuthService> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _context = context;
            _storeCustomerPasswordHasher = storeCustomerPasswordHasher;
            _emailService = emailService;
            _logger = logger;
        }

        public Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
        {
            _logger.LogInformation(
                "Platform register endpoint called for {Email}, but customer registration moved to store-customer auth.",
                dto.Email);

            return Task.FromResult(Fail(StoreCustomerRegisterMessage));
        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {
            try
            {
                var normalizedEmail = NormalizeEmail(dto.Email);

                var user = await FindPlatformUserByEmailAsync(normalizedEmail);
                if (user == null)
                {
                    if (await EmailBelongsToStoreCustomerAsync(normalizedEmail))
                        return Fail(StoreCustomerLoginMessage);

                    return Fail("البريد الإلكتروني أو كلمة المرور غير صحيحة");
                }

                var roles = await _userManager.GetRolesAsync(user);

                if (!IsPlatformAccount(roles))
                    return Fail(StoreCustomerLoginMessage);

                if (!user.IsActive)
                    return Fail("الحساب موقوف، تواصل مع الدعم");

                if (await _userManager.IsLockedOutAsync(user))
                    return Fail("الحساب مقفل مؤقتاً بسبب محاولات خاطئة، حاول بعد 5 دقائق");

                var passwordValid = await _userManager.CheckPasswordAsync(user, dto.Password);
                if (!passwordValid)
                {
                    await _userManager.AccessFailedAsync(user);

                    if (await _userManager.IsLockedOutAsync(user))
                        return Fail("الحساب مقفل مؤقتاً بسبب محاولات خاطئة، حاول بعد 5 دقائق");

                    return Fail("البريد الإلكتروني أو كلمة المرور غير صحيحة");
                }

                if (user.AccessFailedCount > 0)
                    await _userManager.ResetAccessFailedCountAsync(user);

                if (!user.EmailConfirmed)
                    return FailRequiresEmailVerification(
                        "Your email is not verified yet. Please verify it before logging in.");

                var token = await GenerateJwtToken(user);

                _logger.LogInformation("Platform user logged in: {Email}", user.Email);

                return CreateAuthenticatedResponse(
                    user,
                    token.Token,
                    token.ExpiresAt,
                    roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for {Email}", dto.Email);
                return Fail("حدث خطأ أثناء تسجيل الدخول");
            }
        }

        public async Task<AuthResponseDto> VerifyEmailAsync(VerifyEmailDto dto)
        {
            try
            {
                var normalizedEmail = NormalizeEmail(dto.Email);
                var verificationCode = NormalizeValue(dto.Code);

                var user = await FindPlatformUserByEmailAsync(normalizedEmail);
                if (user == null)
                    return Fail("البريد الإلكتروني أو كود التحقق غير صحيح");

                var roles = await _userManager.GetRolesAsync(user);
                if (!IsPlatformAccount(roles))
                    return Fail(StoreCustomerLoginMessage);

                if (!user.IsActive)
                    return Fail("الحساب موقوف، تواصل مع الدعم");

                if (user.EmailConfirmed)
                    return Fail("البريد الإلكتروني مفعل مسبقاً، يمكنك تسجيل الدخول مباشرة.");

                var confirmResult = await _userManager.ConfirmEmailAsync(user, verificationCode);

                if (!confirmResult.Succeeded)
                    return Fail(GetIdentityErrors(confirmResult, "كود التحقق غير صحيح أو منتهي الصلاحية"));

                var token = await GenerateJwtToken(user);

                _logger.LogInformation("Email confirmed successfully for {Email}", user.Email);

                await TrySendWelcomeEmailAsync(user.Email, user.FirstName, "email verification");

                return CreateAuthenticatedResponse(
                    user,
                    token.Token,
                    token.ExpiresAt,
                    roles,
                    "تم تفعيل البريد الإلكتروني بنجاح.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during email verification for {Email}", dto.Email);
                return Fail("حدث خطأ أثناء تفعيل البريد الإلكتروني");
            }
        }

        public async Task<(bool Success, string Message)> ResendVerificationCodeAsync(ResendVerificationCodeDto dto)
        {
            try
            {
                var normalizedEmail = NormalizeEmail(dto.Email);
                var user = await FindPlatformUserByEmailAsync(normalizedEmail);

                if (user == null || !user.IsActive)
                    return (true, "إذا كان الحساب موجوداً وغير مفعل، فسيتم إرسال كود تحقق جديد.");

                var roles = await _userManager.GetRolesAsync(user);
                if (!IsPlatformAccount(roles))
                    return (false, StoreCustomerLoginMessage);

                if (user.EmailConfirmed)
                    return (true, "البريد الإلكتروني مفعل مسبقاً.");

                var emailSent = await TrySendEmailVerificationCodeAsync(user, "verification resend");

                return (
                    true,
                    emailSent
                        ? "تم إرسال كود تحقق جديد إلى بريدك الإلكتروني."
                        : "تعذر إرسال كود التحقق حالياً، حاول مرة أخرى بعد قليل."
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while resending verification code for {Email}", dto.Email);
                return (false, "حدث خطأ أثناء إعادة إرسال كود التحقق");
            }
        }

        public async Task<(bool Success, string Message)> ForgotPasswordAsync(ForgotPasswordDto dto)
        {
            try
            {
                var normalizedEmail = NormalizeEmail(dto.Email);
                var user = await _userManager.FindByEmailAsync(normalizedEmail);

                if (user == null || !user.IsActive)
                    return (true, GenericForgotPasswordMessage);

                var roles = await _userManager.GetRolesAsync(user);
                if (!IsPlatformAccount(roles))
                    return (true, GenericForgotPasswordMessage);

                await TrySendPasswordResetCodeAsync(user, "forgot password");

                return (true, GenericForgotPasswordMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during forgot password request for {Email}", dto.Email);
                return (false, "حدث خطأ أثناء طلب إعادة تعيين كلمة المرور");
            }
        }

        public async Task<(bool Success, string Message)> ResetPasswordAsync(ResetPasswordDto dto)
        {
            try
            {
                var normalizedEmail = NormalizeEmail(dto.Email);
                var resetCode = NormalizeValue(dto.Code);

                var user = await _userManager.FindByEmailAsync(normalizedEmail);
                if (user == null || !user.IsActive)
                    return (false, "بيانات إعادة تعيين كلمة المرور غير صحيحة");

                var roles = await _userManager.GetRolesAsync(user);
                if (!IsPlatformAccount(roles))
                    return (false, "بيانات إعادة تعيين كلمة المرور غير صحيحة");

                var resetResult = await _userManager.ResetPasswordAsync(user, resetCode, dto.NewPassword);

                if (!resetResult.Succeeded)
                    return (false, GetIdentityErrors(resetResult, "كود إعادة تعيين كلمة المرور غير صحيح أو منتهي الصلاحية"));

                await _userManager.UpdateSecurityStampAsync(user);
                await TrySendPasswordResetConfirmationAsync(user, "password reset");

                _logger.LogInformation("Password reset completed for {Email}", user.Email);

                return (true, "تمت إعادة تعيين كلمة المرور بنجاح.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during password reset for {Email}", dto.Email);
                return (false, "حدث خطأ أثناء إعادة تعيين كلمة المرور");
            }
        }

        public async Task LogoutAsync(string userId)
        {
            try
            {
                await _signInManager.SignOutAsync();
                _logger.LogInformation("User logged out: {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout for {UserId}", userId);
            }
        }

        public async Task<AuthResponseDto> GoogleLoginAsync(GoogleAuthDto dto)
        {
            _logger.LogInformation("[GoogleLogin] Starting Google login process for email: {Email}", dto.Email);

            try
            {
                var normalizedEmail = NormalizeEmail(dto.Email);
                var normalizedFirstName = NormalizeValue(dto.FirstName, allowEmpty: true);
                var normalizedLastName = NormalizeValue(dto.LastName, allowEmpty: true);

                _logger.LogDebug("[GoogleLogin] Normalized data - Email: {Email}, FirstName: {FirstNameExists}, LastName: {LastNameExists}",
                    normalizedEmail, !string.IsNullOrWhiteSpace(normalizedFirstName), !string.IsNullOrWhiteSpace(normalizedLastName));

                var user = await FindPlatformUserByEmailAsync(normalizedEmail);

                if (user == null)
                {
                    _logger.LogInformation("[GoogleLogin] User not found, creating new account for: {Email}", normalizedEmail);

                    user = new AppUser
                    {
                        Email = normalizedEmail,
                        UserName = normalizedEmail,
                        FirstName = normalizedFirstName,
                        LastName = normalizedLastName,
                        EmailConfirmed = true,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    _logger.LogDebug("[GoogleLogin] Calling CreateAsync for new user: {Email}", normalizedEmail);
                    var createResult = await _userManager.CreateAsync(user);

                    if (!createResult.Succeeded)
                    {
                        var errors = GetIdentityErrors(createResult);
                        _logger.LogWarning("[GoogleLogin] Failed to create user {Email}. Errors: {Errors}",
                            normalizedEmail, errors);
                        return Fail("تعذر إكمال تسجيل الدخول عبر Google - فشل في إنشاء الحساب");
                    }

                    _logger.LogInformation("[GoogleLogin] User {Email} created successfully. Now assigning role: {Role}",
                        normalizedEmail, DefaultCustomerRole);

                    var addRoleResult = await _userManager.AddToRoleAsync(user, DefaultCustomerRole);

                    if (!addRoleResult.Succeeded)
                    {
                        var errors = GetIdentityErrors(addRoleResult);
                        _logger.LogError("[GoogleLogin] CRITICAL: Failed to assign role {Role} to user {Email}. Errors: {Errors}. User exists but has no role!",
                            DefaultCustomerRole, normalizedEmail, errors);

                        // Attempt to delete the orphaned user
                        try
                        {
                            await _userManager.DeleteAsync(user);
                            _logger.LogInformation("[GoogleLogin] Rolled back user creation for {Email} due to role assignment failure", normalizedEmail);
                        }
                        catch (Exception deleteEx)
                        {
                            _logger.LogError(deleteEx, "[GoogleLogin] Failed to roll back user creation for {Email}. Manual cleanup required.", normalizedEmail);
                        }

                        return Fail("تعذر إكمال تسجيل الدخول عبر Google - خطأ في إعدادات النظام");
                    }

                    _logger.LogInformation("[GoogleLogin] Successfully created new customer account via Google for {Email}", normalizedEmail);
                }
                else
                {
                    _logger.LogInformation("[GoogleLogin] Existing user found: {Email}, UserId: {UserId}", normalizedEmail, user.Id);
                }

                _logger.LogDebug("[GoogleLogin] Retrieving roles for user: {Email}", normalizedEmail);
                var roles = await _userManager.GetRolesAsync(user);
                _logger.LogDebug("[GoogleLogin] User {Email} has roles: {@Roles}", normalizedEmail, roles);

                if (!roles.Contains(DefaultCustomerRole))
                {
                    _logger.LogInformation("[GoogleLogin] Ensuring role {Role} exists for user {Email}", DefaultCustomerRole, normalizedEmail);

                    var ensureRoleResult = await _userManager.AddToRoleAsync(user, DefaultCustomerRole);
                    if (!ensureRoleResult.Succeeded)
                    {
                        _logger.LogError("[GoogleLogin] Failed to ensure role {Role} for user {Email}. Errors: {Errors}",
                            DefaultCustomerRole,
                            normalizedEmail,
                            GetIdentityErrors(ensureRoleResult));
                        return Fail("تعذر إكمال تسجيل الدخول عبر Google - خطأ في صلاحيات المستخدم");
                    }

                    roles = await _userManager.GetRolesAsync(user);
                }

                if (!user.IsActive)
                {
                    _logger.LogWarning("[GoogleLogin] Login attempt for inactive account: {Email}", normalizedEmail);
                    return Fail("الحساب موقوف، تواصل مع الدعم");
                }

                if (!user.EmailConfirmed)
                {
                    _logger.LogInformation("[GoogleLogin] Confirming email for user: {Email}", normalizedEmail);
                    user.EmailConfirmed = true;
                    var updateResult = await _userManager.UpdateAsync(user);

                    if (!updateResult.Succeeded)
                    {
                        _logger.LogWarning("[GoogleLogin] Failed to confirm email for {Email}. Errors: {Errors}",
                            normalizedEmail, GetIdentityErrors(updateResult));
                        // Continue anyway, not critical
                    }
                }

                var shouldUpdateUser = false;

                if (string.IsNullOrWhiteSpace(user.FirstName) && !string.IsNullOrWhiteSpace(normalizedFirstName))
                {
                    user.FirstName = normalizedFirstName;
                    shouldUpdateUser = true;
                }

                if (string.IsNullOrWhiteSpace(user.LastName) && !string.IsNullOrWhiteSpace(normalizedLastName))
                {
                    user.LastName = normalizedLastName;
                    shouldUpdateUser = true;
                }

                if (shouldUpdateUser)
                {
                    _logger.LogDebug("[GoogleLogin] Updating user profile with name info: {Email}", normalizedEmail);
                    var updateResult = await _userManager.UpdateAsync(user);

                    if (!updateResult.Succeeded)
                    {
                        _logger.LogWarning("[GoogleLogin] Failed to update user name for {Email}. Errors: {Errors}",
                            normalizedEmail, GetIdentityErrors(updateResult));
                        // Continue anyway, not critical
                    }
                }

                if (!roles.Any())
                {
                    _logger.LogWarning("[GoogleLogin] User {Email} has no roles. Attempting to assign fallback role: {Role}",
                        normalizedEmail, DefaultCustomerRole);

                    var fallbackRoleResult = await _userManager.AddToRoleAsync(user, DefaultCustomerRole);

                    if (!fallbackRoleResult.Succeeded)
                    {
                        _logger.LogError("[GoogleLogin] Failed to assign fallback role {Role} to user {Email}. Errors: {Errors}",
                            DefaultCustomerRole, normalizedEmail, GetIdentityErrors(fallbackRoleResult));
                        return Fail("تعذر إكمال تسجيل الدخول عبر Google - خطأ في صلاحيات المستخدم");
                    }

                    roles = await _userManager.GetRolesAsync(user);
                    _logger.LogInformation("[GoogleLogin] Fallback role assigned successfully. User {Email} now has roles: {@Roles}",
                        normalizedEmail, roles);
                }

                var store = await ResolveGoogleStoreAsync(dto.StoreId, dto.StoreSlug);
                if (store == null)
                {
                    _logger.LogWarning(
                        "[GoogleLogin] Google login aborted for {Email}: invalid or missing store context. StoreId: {StoreId}, StoreSlug: {StoreSlug}",
                        normalizedEmail,
                        dto.StoreId,
                        dto.StoreSlug);
                    return Fail("store_invalid_or_missing");
                }

                await EnsureCustomerStoreLinkAsync(store, normalizedEmail, normalizedFirstName, normalizedLastName);

                _logger.LogDebug("[GoogleLogin] Generating JWT token for user: {Email}", normalizedEmail);
                var token = await GenerateJwtToken(user);
                _logger.LogDebug("[GoogleLogin] JWT token generated successfully for: {Email}, Expires: {ExpiresAt}",
                    normalizedEmail, token.ExpiresAt);

                _logger.LogInformation("[GoogleLogin] User logged in successfully via Google: {Email}, Roles: {@Roles}",
                    normalizedEmail, roles);

                return CreateAuthenticatedResponse(
                    user,
                    token.Token,
                    token.ExpiresAt,
                    roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GoogleLogin] Unhandled exception during Google login for {Email}. Error: {Message}",
                    dto.Email, ex.Message);
                return Fail("حدث خطأ أثناء تسجيل الدخول عبر Google");
            }
        }

        public async Task<OwnerResponseDto> CreateOwnerAsync(CreateOwnerDto dto)
        {
            var normalizedEmail = NormalizeEmail(dto.Email);

            var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);
            if (existingUser != null)
                throw new Exception("البريد الإلكتروني مستخدم مسبقاً");

            var owner = new AppUser
            {
                FirstName = NormalizeValue(dto.FirstName),
                LastName = NormalizeValue(dto.LastName),
                Email = normalizedEmail,
                UserName = normalizedEmail,
                EmailConfirmed = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(owner, dto.Password);

            if (!result.Succeeded)
                throw new Exception(GetIdentityErrors(result));

            await _userManager.AddToRoleAsync(owner, "StoreOwner");
            await TrySendEmailVerificationCodeAsync(owner, "owner creation");

            return new OwnerResponseDto
            {
                Id = owner.Id,
                Email = owner.Email!,
                FirstName = owner.FirstName,
                LastName = owner.LastName,
                IsActive = owner.IsActive
            };
        }

        public async Task<(bool Success, string Message)> ChangeUserPasswordBySuperAdminAsync(Guid userId, string newPassword)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());

                if (user == null || !user.IsActive)
                    return (false, "المستخدم غير موجود");

                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                var result = await _userManager.ResetPasswordAsync(user, resetToken, newPassword);

                if (!result.Succeeded)
                    return (false, GetIdentityErrors(result));

                await _userManager.UpdateSecurityStampAsync(user);
                await TrySendPasswordResetConfirmationAsync(user, "super admin password change");

                _logger.LogInformation("Password changed by SuperAdmin for user: {UserId}", userId);

                return (true, "تم تغيير كلمة المرور بنجاح");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while SuperAdmin changing password for user {UserId}", userId);
                return (false, "حدث خطأ أثناء تغيير كلمة المرور");
            }
        }

        private async Task<(string Token, DateTime ExpiresAt)> GenerateJwtToken(AppUser user)
        {
            try
            {
                _logger.LogDebug("[GenerateJwtToken] Starting JWT generation for user: {UserId}, Email: {Email}", user.Id, user.Email);

                var jwtSettings = _configuration.GetSection("JwtSettings");
                var secretKey = jwtSettings["SecretKey"];

                if (string.IsNullOrWhiteSpace(secretKey))
                {
                    _logger.LogError("[GenerateJwtToken] JWT SecretKey is not configured");
                    throw new InvalidOperationException("JWT SecretKey is not configured");
                }

                _logger.LogDebug("[GenerateJwtToken] Building security key...");
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
                var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                _logger.LogDebug("[GenerateJwtToken] Retrieving roles for user: {UserId}", user.Id);
                var roles = await _userManager.GetRolesAsync(user);
                _logger.LogDebug("[GenerateJwtToken] User {UserId} has {RoleCount} roles", user.Id, roles.Count);

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Email, user.Email!),
                    new Claim(ClaimTypes.GivenName, user.FirstName ?? string.Empty),
                    new Claim(ClaimTypes.Surname, user.LastName ?? string.Empty),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                    new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
                };

                claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
                _logger.LogDebug("[GenerateJwtToken] Total claims count: {ClaimCount}", claims.Count);

                var expiryDaysValue = jwtSettings["ExpiryInDays"] ?? "7";
                if (!int.TryParse(expiryDaysValue, out var expiryDays) || expiryDays <= 0)
                {
                    _logger.LogWarning("[GenerateJwtToken] Invalid ExpiryInDays value: {Value}, using default 7", expiryDaysValue);
                    expiryDays = 7;
                }

                var expiresAt = DateTime.UtcNow.AddDays(expiryDays);
                _logger.LogDebug("[GenerateJwtToken] Token will expire at: {ExpiresAt}", expiresAt);

                var token = new JwtSecurityToken(
                    issuer: jwtSettings["Issuer"],
                    audience: jwtSettings["Audience"],
                    claims: claims,
                    notBefore: DateTime.UtcNow,
                    expires: expiresAt,
                    signingCredentials: credentials);

                var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
                _logger.LogDebug("[GenerateJwtToken] JWT generated successfully for user: {UserId}", user.Id);

                return (tokenString, expiresAt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GenerateJwtToken] Failed to generate JWT for user {UserId}. Error: {Message}",
                    user.Id, ex.Message);
                throw;
            }
        }

        private async Task<bool> TrySendEmailVerificationCodeAsync(AppUser user, string source)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                _logger.LogWarning("Email verification code skipped after {Source} because email is missing.", source);
                return false;
            }

            try
            {
                var verificationCode = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var template = EmailTemplateBuilder.BuildEmailVerificationCodeEmail(user.FirstName, verificationCode);

                await _emailService.SendEmailAsync(user.Email, template.Subject, template.HtmlBody);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while sending verification code to {Email} after {Source}.", user.Email, source);
                return false;
            }
        }

        private async Task<bool> TrySendPasswordResetCodeAsync(AppUser user, string source)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                _logger.LogWarning("Password reset code skipped after {Source} because email is missing.", source);
                return false;
            }

            try
            {
                var resetCode = await _userManager.GeneratePasswordResetTokenAsync(user);
                var template = EmailTemplateBuilder.BuildPasswordResetCodeEmail(user.FirstName, resetCode);

                await _emailService.SendEmailAsync(user.Email, template.Subject, template.HtmlBody);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while sending password reset code to {Email} after {Source}.", user.Email, source);
                return false;
            }
        }

        private async Task<bool> TrySendPasswordResetConfirmationAsync(AppUser user, string source)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                _logger.LogWarning("Password reset confirmation skipped after {Source} because email is missing.", source);
                return false;
            }

            try
            {
                var template = EmailTemplateBuilder.BuildPasswordResetConfirmationEmail(user.FirstName);
                await _emailService.SendEmailAsync(user.Email, template.Subject, template.HtmlBody);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while sending password reset confirmation to {Email} after {Source}.", user.Email, source);
                return false;
            }
        }

        private async Task TrySendWelcomeEmailAsync(string? toEmail, string? firstName, string source)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                _logger.LogWarning("Welcome email skipped after {Source} because email is missing.", source);
                return;
            }

            try
            {
                var template = EmailTemplateBuilder.BuildWelcomeEmail(firstName);
                await _emailService.SendEmailAsync(toEmail, template.Subject, template.HtmlBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while sending welcome email to {Email} after {Source}.", toEmail, source);
            }
        }

        private static AuthResponseDto CreateAuthenticatedResponse(
            AppUser user,
            string token,
            DateTime expiresAt,
            IList<string> roles,
            string? message = null) => new()
            {
                Success = true,
                RequiresEmailVerification = false,
                Message = message,
                Token = token,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Roles = roles,
                ExpiresAt = expiresAt
            };

        private static AuthResponseDto FailRequiresEmailVerification(string message) => new()
        {
            Success = false,
            RequiresEmailVerification = true,
            Message = message
        };

        private static AuthResponseDto Fail(string message) => new()
        {
            Success = false,
            Message = message
        };

        private static string NormalizeEmail(string email) =>
            email.Trim().ToLowerInvariant();

        private static string NormalizeValue(string? value, bool allowEmpty = false)
        {
            var normalized = value?.Trim() ?? string.Empty;

            if (!allowEmpty && string.IsNullOrWhiteSpace(normalized))
                return string.Empty;

            return normalized;
        }

        private static string GetIdentityErrors(IdentityResult result, string? invalidTokenMessage = null)
        {
            if (!string.IsNullOrWhiteSpace(invalidTokenMessage) &&
                result.Errors.Any(e => string.Equals(e.Code, "InvalidToken", StringComparison.OrdinalIgnoreCase)))
            {
                return invalidTokenMessage;
            }

            return string.Join(", ", result.Errors.Select(e => e.Description));
        }

        private static bool IsPlatformAccount(IList<string> roles) =>
            roles.Contains("SuperAdmin") || roles.Contains("StoreOwner");

        private Task<bool> EmailBelongsToStoreCustomerAsync(string normalizedEmail) =>
            _context.StoreCustomers
                .IgnoreQueryFilters()
                .AnyAsync(x => x.Email == normalizedEmail);

        private async Task<AppUser?> FindPlatformUserByEmailAsync(string normalizedEmail)
        {
            var user = await _userManager.FindByEmailAsync(normalizedEmail);
            if (user != null)
                return user;

            user = await _context.Users.FirstOrDefaultAsync(x =>
                (x.Email != null && x.Email.ToLower() == normalizedEmail) ||
                (x.UserName != null && x.UserName.ToLower() == normalizedEmail));

            if (user == null)
                return null;

            var normalizedUserName = _userManager.NormalizeName(normalizedEmail);
            var normalizedEmailValue = _userManager.NormalizeEmail(normalizedEmail);
            var hasChanges = false;

            if (!string.Equals(user.Email, normalizedEmail, StringComparison.Ordinal))
            {
                user.Email = normalizedEmail;
                hasChanges = true;
            }

            if (!string.Equals(user.UserName, normalizedEmail, StringComparison.Ordinal))
            {
                user.UserName = normalizedEmail;
                hasChanges = true;
            }

            if (!string.Equals(user.NormalizedEmail, normalizedEmailValue, StringComparison.Ordinal))
            {
                user.NormalizedEmail = normalizedEmailValue;
                hasChanges = true;
            }

            if (!string.Equals(user.NormalizedUserName, normalizedUserName, StringComparison.Ordinal))
            {
                user.NormalizedUserName = normalizedUserName;
                hasChanges = true;
            }

            if (hasChanges)
                await _userManager.UpdateAsync(user);

            return user;
        }

        private async Task<onlineStore.Models.Store?> ResolveGoogleStoreAsync(Guid? storeId, string? storeSlug)
        {
            if (storeId.HasValue)
            {
                return await _context.Stores
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == storeId.Value && x.IsActive);
            }

            var normalizedSlug = storeSlug?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedSlug))
                return null;

            return await _context.Stores
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Slug == normalizedSlug && x.IsActive);
        }

        private async Task EnsureCustomerStoreLinkAsync(
            onlineStore.Models.Store store,
            string normalizedEmail,
            string normalizedFirstName,
            string normalizedLastName)
        {
            var existingCustomer = await _context.StoreCustomers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.StoreId == store.Id && x.Email == normalizedEmail);

            if (existingCustomer != null)
            {
                var shouldUpdate = false;

                if (existingCustomer.IsDeleted)
                {
                    existingCustomer.IsDeleted = false;
                    shouldUpdate = true;
                }

                if (!existingCustomer.IsActive)
                {
                    existingCustomer.IsActive = true;
                    shouldUpdate = true;
                }

                if (!existingCustomer.EmailConfirmed)
                {
                    existingCustomer.EmailConfirmed = true;
                    shouldUpdate = true;
                }

                if (string.IsNullOrWhiteSpace(existingCustomer.FirstName) && !string.IsNullOrWhiteSpace(normalizedFirstName))
                {
                    existingCustomer.FirstName = normalizedFirstName;
                    shouldUpdate = true;
                }

                if (string.IsNullOrWhiteSpace(existingCustomer.LastName) && !string.IsNullOrWhiteSpace(normalizedLastName))
                {
                    existingCustomer.LastName = normalizedLastName;
                    shouldUpdate = true;
                }

                if (shouldUpdate)
                    await _context.SaveChangesAsync();

                return;
            }

            var customer = new StoreCustomer
            {
                StoreId = store.Id,
                FirstName = string.IsNullOrWhiteSpace(normalizedFirstName) ? "Google" : normalizedFirstName,
                LastName = string.IsNullOrWhiteSpace(normalizedLastName) ? "Customer" : normalizedLastName,
                Email = normalizedEmail,
                EmailConfirmed = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            customer.PasswordHash = _storeCustomerPasswordHasher.HashPassword(customer, $"google:{Guid.NewGuid():N}");

            _context.StoreCustomers.Add(customer);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[GoogleLogin] CustomerStore link created. StoreId: {StoreId}, StoreCustomerId: {StoreCustomerId}, Email: {Email}",
                store.Id,
                customer.Id,
                normalizedEmail);
        }
    }
}
