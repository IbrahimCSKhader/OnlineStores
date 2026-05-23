using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using onlineStore.Data;
using onlineStore.DTOs.Auth;
using onlineStore.Models;
using onlineStore.Models.CartModels;
using onlineStore.Models.Identity;
using onlineStore.Security;
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

        private const string StoreOwnerCustomerConflictMessage =
            "This email belongs to the store owner for this store. Use the platform owner authentication flow instead.";

        private const string GoogleStorefrontAuthMode = "storefront";
        private const string GoogleStorefrontSessionScope = "storefront";
        private const string GoogleStorefrontDashboard = "customer";

        private const string GoogleFailureStoreInvalidOrMissing = "store_invalid_or_missing";
        private const string GoogleFailureOwnerCustomerConflict = "owner_customer_conflict";
        private const string GoogleFailureJwtGenerationFailed = "jwt_generation_failed";
        private const string GoogleFailureUnexpectedError = "unexpected_error";

        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _context;
        private readonly IStoreAccountBoundaryService _storeAccountBoundaryService;
        private readonly IPasswordHasher<StoreCustomer> _storeCustomerPasswordHasher;
        private readonly IEmailService _emailService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            IConfiguration configuration,
            AppDbContext context,
            IStoreAccountBoundaryService storeAccountBoundaryService,
            IPasswordHasher<StoreCustomer> storeCustomerPasswordHasher,
            IEmailService emailService,
            ILogger<AuthService> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _context = context;
            _storeAccountBoundaryService = storeAccountBoundaryService;
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
                var user = await FindPlatformUserByEmailAsync(normalizedEmail);

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

                var user = await FindPlatformUserByEmailAsync(normalizedEmail);
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
            _logger.LogInformation(
                "[GoogleLogin] Starting Google login process for email: {Email}, InputStoreId: {InputStoreId}, InputStoreSlug: {InputStoreSlug}, RedirectToExists: {HasRedirectTo}",
                dto.Email,
                dto.StoreId,
                dto.StoreSlug,
                !string.IsNullOrWhiteSpace(dto.RedirectTo));

            try
            {
                var normalizedEmail = NormalizeEmail(dto.Email);
                var normalizedFirstName = NormalizeValue(dto.FirstName, allowEmpty: true);
                var normalizedLastName = NormalizeValue(dto.LastName, allowEmpty: true);
                var normalizedRedirectTo = NormalizeValue(dto.RedirectTo, allowEmpty: true);

                _logger.LogDebug(
                    "[GoogleLogin] Normalized inputs. Email: {Email}, FirstNameExists: {FirstNameExists}, LastNameExists: {LastNameExists}, RedirectToExists: {RedirectToExists}",
                    normalizedEmail,
                    !string.IsNullOrWhiteSpace(normalizedFirstName),
                    !string.IsNullOrWhiteSpace(normalizedLastName),
                    !string.IsNullOrWhiteSpace(normalizedRedirectTo));

                var store = await ResolveGoogleStoreAsync(dto.StoreId, dto.StoreSlug);
                if (store == null)
                {
                    _logger.LogWarning(
                        "[GoogleLogin] Google login aborted for {Email}: invalid or missing store context. StoreId: {StoreId}, StoreSlug: {StoreSlug}",
                        normalizedEmail,
                        dto.StoreId,
                        dto.StoreSlug);
                    return Fail(GoogleFailureStoreInvalidOrMissing, "Store context is missing or invalid.");
                }

                _logger.LogInformation(
                    "[GoogleLogin] Store context resolved. StoreId: {StoreId}, StoreSlug: {StoreSlug}, Email: {Email}",
                    store.Id,
                    store.Slug,
                    normalizedEmail);

                var storeOwnerGoogleAuthResult = await TryAuthenticateStoreOwnerViaGoogleAsync(
                    store,
                    normalizedEmail,
                    normalizedFirstName,
                    normalizedLastName,
                    normalizedRedirectTo);

                if (storeOwnerGoogleAuthResult != null)
                {
                    return storeOwnerGoogleAuthResult;
                }

                var strategy = _context.Database.CreateExecutionStrategy();

                return await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync();

                    try
                    {
                        _logger.LogDebug(
                            "[GoogleLogin] Transaction started for StoreId: {StoreId}, Email: {Email}",
                            store.Id,
                            normalizedEmail);

                        var linkResult = await EnsureCustomerStoreLinkAsync(
                            store,
                            normalizedEmail,
                            normalizedFirstName,
                            normalizedLastName);

                        var storeCustomer = linkResult.Customer;

                        _logger.LogInformation(
                            "[GoogleLogin] Customer link ready. StoreCustomerId: {StoreCustomerId}, WasCreated: {WasCreated}, WasReactivated: {WasReactivated}, CartProvisioned: {CartProvisioned}, HasPendingPersistence: {HasPendingPersistence}",
                            storeCustomer.Id,
                            linkResult.WasCreated,
                            linkResult.WasReactivated,
                            linkResult.CartProvisioned,
                            linkResult.PersistNeeded);

                        _logger.LogDebug(
                            "[GoogleLogin] Generating StoreCustomer JWT token for email: {Email}, StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}",
                            normalizedEmail,
                            storeCustomer.Id,
                            storeCustomer.StoreId);

                        (string Token, DateTime ExpiresAt) token;
                        try
                        {
                            token = GenerateStoreCustomerJwtToken(storeCustomer);
                        }
                        catch (Exception ex)
                        {
                            await transaction.RollbackAsync();
                            _logger.LogError(
                                ex,
                                "[GoogleLogin] JWT generation failed. Rolling back transaction. StoreId: {StoreId}, StoreCustomerId: {StoreCustomerId}, Email: {Email}",
                                store.Id,
                                storeCustomer.Id,
                                normalizedEmail);
                            return Fail(GoogleFailureJwtGenerationFailed, "Failed to generate authentication token.");
                        }

                        if (linkResult.PersistNeeded || _context.ChangeTracker.HasChanges())
                        {
                            _logger.LogDebug(
                                "[GoogleLogin] Persisting customer/cart changes for StoreId: {StoreId}, StoreCustomerId: {StoreCustomerId}",
                                store.Id,
                                storeCustomer.Id);
                            await _context.SaveChangesAsync();
                            _logger.LogDebug(
                                "[GoogleLogin] Persistence completed for StoreId: {StoreId}, StoreCustomerId: {StoreCustomerId}",
                                store.Id,
                                storeCustomer.Id);
                        }

                        await transaction.CommitAsync();
                        _logger.LogDebug(
                            "[GoogleLogin] Transaction committed for StoreId: {StoreId}, StoreCustomerId: {StoreCustomerId}",
                            store.Id,
                            storeCustomer.Id);

                        _logger.LogDebug(
                            "[GoogleLogin] StoreCustomer JWT token generated successfully for: {Email}, StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}, Expires: {ExpiresAt}",
                            normalizedEmail,
                            storeCustomer.Id,
                            storeCustomer.StoreId,
                            token.ExpiresAt);

                        _logger.LogInformation(
                            "[GoogleLogin] User logged in successfully via Google as StoreCustomer. Email: {Email}, StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}",
                            normalizedEmail,
                            storeCustomer.Id,
                            storeCustomer.StoreId);

                        return CreateStoreCustomerAuthenticatedResponse(
                            storeCustomer,
                            token.Token,
                            token.ExpiresAt,
                            store.Id,
                            store.Slug,
                            normalizedRedirectTo);
                    }
                    catch
                    {
                        try
                        {
                            await transaction.RollbackAsync();
                        }
                        catch (Exception rollbackEx)
                        {
                            _logger.LogWarning(
                                rollbackEx,
                                "[GoogleLogin] Transaction rollback failed after exception. StoreId: {StoreId}, Email: {Email}",
                                store.Id,
                                normalizedEmail);
                        }

                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GoogleLogin] Unhandled exception during Google login for {Email}. Error: {Message}",
                    dto.Email, ex.Message);
                return Fail(GoogleFailureUnexpectedError, "حدث خطأ أثناء تسجيل الدخول عبر Google");
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

        private (string Token, DateTime ExpiresAt) GenerateStoreCustomerJwtToken(StoreCustomer customer)
        {
            _logger.LogDebug(
                "[GenerateStoreCustomerJwtToken] Generating token for StoreCustomerId: {StoreCustomerId}, StoreId: {StoreId}, Email: {Email}",
                customer.Id,
                customer.StoreId,
                customer.Email);

            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"];

            if (string.IsNullOrWhiteSpace(secretKey))
            {
                _logger.LogError("[GenerateStoreCustomerJwtToken] JWT SecretKey is not configured.");
                throw new InvalidOperationException("JWT SecretKey is not configured");
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expiryInDaysValue = jwtSettings["ExpiryInDays"] ?? "7";
            if (!int.TryParse(expiryInDaysValue, out var expiryInDays) || expiryInDays <= 0)
            {
                _logger.LogWarning(
                    "[GenerateStoreCustomerJwtToken] Invalid ExpiryInDays value: {ExpiryInDaysValue}. Defaulting to 7.",
                    expiryInDaysValue);
                expiryInDays = 7;
            }

            var expiresAt = DateTime.UtcNow.AddDays(expiryInDays);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, customer.Id.ToString()),
                new(StoreCustomerClaimTypes.StoreCustomerId, customer.Id.ToString()),
                new(StoreCustomerClaimTypes.StoreId, customer.StoreId.ToString()),
                new(StoreCustomerClaimTypes.AccountType, StoreCustomerClaimTypes.StoreCustomerAccountType),
                new(StoreCustomerClaimTypes.IsGuest, "false"),
                new(ClaimTypes.Role, StoreCustomerClaimTypes.StoreCustomerAccountType),
                new(ClaimTypes.Email, customer.Email),
                new(ClaimTypes.GivenName, customer.FirstName),
                new(ClaimTypes.Surname, customer.LastName),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(
                    JwtRegisteredClaimNames.Iat,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                    ClaimValueTypes.Integer64)
            };

            _logger.LogDebug(
                "[GenerateStoreCustomerJwtToken] Claim set prepared for StoreCustomerId: {StoreCustomerId}. ClaimsCount: {ClaimsCount}",
                customer.Id,
                claims.Count);

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: expiresAt,
                signingCredentials: credentials);

            var tokenValue = new JwtSecurityTokenHandler().WriteToken(token);
            _logger.LogDebug(
                "[GenerateStoreCustomerJwtToken] Token generated successfully for StoreCustomerId: {StoreCustomerId}, ExpiresAt: {ExpiresAt}",
                customer.Id,
                expiresAt);

            return (tokenValue, expiresAt);
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
            string? message = null,
            Guid? storeId = null,
            string? storeSlug = null,
            string? redirectTo = null) => new()
            {
                Success = true,
                RequiresEmailVerification = false,
                Message = message,
                Token = token,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                AccountType = "PlatformUser",
                Roles = roles,
                ExpiresAt = expiresAt,
                StoreId = storeId,
                StoreSlug = storeSlug,
                RedirectTo = redirectTo,
                AuthMode = "platform",
                SessionScope = "platform",
                Dashboard = "owner"
            };

        private static AuthResponseDto CreateStoreCustomerAuthenticatedResponse(
            StoreCustomer customer,
            string token,
            DateTime expiresAt,
            Guid storeId,
            string? storeSlug,
            string? redirectTo,
            string? message = null) => new()
            {
                Success = true,
                RequiresEmailVerification = false,
                Message = message,
                Token = token,
                Email = customer.Email,
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                StoreCustomerId = customer.Id,
                AccountType = StoreCustomerClaimTypes.StoreCustomerAccountType,
                Roles = [StoreCustomerClaimTypes.StoreCustomerAccountType],
                ExpiresAt = expiresAt,
                StoreId = storeId,
                StoreSlug = storeSlug,
                RedirectTo = redirectTo,
                AuthMode = GoogleStorefrontAuthMode,
                SessionScope = GoogleStorefrontSessionScope,
                Dashboard = GoogleStorefrontDashboard
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

        private static AuthResponseDto Fail(string errorCode, string? message) => new()
        {
            Success = false,
            ErrorCode = errorCode,
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

        private async Task<AuthResponseDto?> TryAuthenticateStoreOwnerViaGoogleAsync(
            onlineStore.Models.Store store,
            string normalizedEmail,
            string normalizedFirstName,
            string normalizedLastName,
            string normalizedRedirectTo)
        {
            if (!await _storeAccountBoundaryService.IsStoreOwnerEmailAsync(store.Id, normalizedEmail))
                return null;

            _logger.LogInformation(
                "[GoogleLoginOwner] Google login matched the current store owner. StoreId: {StoreId}, OwnerId: {OwnerId}, Email: {Email}",
                store.Id,
                store.OwnerId,
                normalizedEmail);

            var owner = await FindPlatformUserByEmailAsync(normalizedEmail);
            if (owner == null || owner.Id != store.OwnerId)
            {
                _logger.LogWarning(
                    "[GoogleLoginOwner] Store owner lookup failed or mismatched. StoreId: {StoreId}, ExpectedOwnerId: {ExpectedOwnerId}, ResolvedOwnerId: {ResolvedOwnerId}, Email: {Email}",
                    store.Id,
                    store.OwnerId,
                    owner?.Id,
                    normalizedEmail);
                return Fail(GoogleFailureOwnerCustomerConflict, StoreOwnerCustomerConflictMessage);
            }

            var roles = await _userManager.GetRolesAsync(owner);
            if (!roles.Any(role => string.Equals(role, "StoreOwner", StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning(
                    "[GoogleLoginOwner] Store owner account is missing the StoreOwner role. StoreId: {StoreId}, OwnerId: {OwnerId}, Email: {Email}",
                    store.Id,
                    owner.Id,
                    normalizedEmail);
                return Fail(GoogleFailureOwnerCustomerConflict, StoreOwnerCustomerConflictMessage);
            }

            if (!owner.IsActive || owner.IsDeleted)
            {
                _logger.LogWarning(
                    "[GoogleLoginOwner] Store owner account is inactive or deleted. StoreId: {StoreId}, OwnerId: {OwnerId}, Email: {Email}, IsActive: {IsActive}, IsDeleted: {IsDeleted}",
                    store.Id,
                    owner.Id,
                    normalizedEmail,
                    owner.IsActive,
                    owner.IsDeleted);
                return Fail(GoogleFailureOwnerCustomerConflict, "This store owner account is inactive.");
            }

            var shouldUpdateOwner = false;

            if (!owner.EmailConfirmed)
            {
                owner.EmailConfirmed = true;
                shouldUpdateOwner = true;
            }

            if (string.IsNullOrWhiteSpace(owner.FirstName) && !string.IsNullOrWhiteSpace(normalizedFirstName))
            {
                owner.FirstName = normalizedFirstName;
                shouldUpdateOwner = true;
            }

            if (string.IsNullOrWhiteSpace(owner.LastName) && !string.IsNullOrWhiteSpace(normalizedLastName))
            {
                owner.LastName = normalizedLastName;
                shouldUpdateOwner = true;
            }

            if (shouldUpdateOwner)
            {
                var updateResult = await _userManager.UpdateAsync(owner);
                if (!updateResult.Succeeded)
                {
                    _logger.LogWarning(
                        "[GoogleLoginOwner] Failed to persist owner updates after Google verification. StoreId: {StoreId}, OwnerId: {OwnerId}, Email: {Email}, Errors: {Errors}",
                        store.Id,
                        owner.Id,
                        normalizedEmail,
                        updateResult.Errors.Select(error => $"{error.Code}:{error.Description}"));
                    return Fail(GoogleFailureUnexpectedError, "Failed to update the store owner account.");
                }
            }

            var token = await GenerateJwtToken(owner);

            _logger.LogInformation(
                "[GoogleLoginOwner] Store owner authenticated successfully via Google. StoreId: {StoreId}, OwnerId: {OwnerId}, Email: {Email}",
                store.Id,
                owner.Id,
                normalizedEmail);

            return CreateAuthenticatedResponse(
                owner,
                token.Token,
                token.ExpiresAt,
                roles,
                storeId: store.Id,
                storeSlug: store.Slug,
                redirectTo: normalizedRedirectTo);
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

        private async Task<StoreCustomerProvisionResult> EnsureCustomerStoreLinkAsync(
            onlineStore.Models.Store store,
            string normalizedEmail,
            string normalizedFirstName,
            string normalizedLastName)
        {
            _logger.LogDebug(
                "[EnsureCustomerStoreLink] Ensuring StoreCustomer link. StoreId: {StoreId}, Email: {Email}",
                store.Id,
                normalizedEmail);

            var existingCustomer = await _context.StoreCustomers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.StoreId == store.Id && x.Email == normalizedEmail);

            if (existingCustomer != null)
            {
                _logger.LogInformation(
                    "[EnsureCustomerStoreLink] Existing customer found. StoreCustomerId: {StoreCustomerId}, IsDeleted: {IsDeleted}, IsActive: {IsActive}, EmailConfirmed: {EmailConfirmed}",
                    existingCustomer.Id,
                    existingCustomer.IsDeleted,
                    existingCustomer.IsActive,
                    existingCustomer.EmailConfirmed);

                var shouldUpdate = false;
                var reactivated = false;

                if (existingCustomer.IsDeleted)
                {
                    existingCustomer.IsDeleted = false;
                    shouldUpdate = true;
                    reactivated = true;
                }

                if (!existingCustomer.IsActive)
                {
                    existingCustomer.IsActive = true;
                    shouldUpdate = true;
                    reactivated = true;
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

                if (string.IsNullOrWhiteSpace(existingCustomer.PasswordHash))
                {
                    existingCustomer.PasswordHash = _storeCustomerPasswordHasher.HashPassword(
                        existingCustomer,
                        $"google:{Guid.NewGuid():N}");
                    shouldUpdate = true;
                }

                var cartUpdated = await EnsureCustomerCartExistsAsync(existingCustomer.Id, store.Id);

                _logger.LogInformation(
                    "[EnsureCustomerStoreLink] Existing customer processed. StoreCustomerId: {StoreCustomerId}, ShouldUpdate: {ShouldUpdate}, CartUpdated: {CartUpdated}, Reactivated: {Reactivated}",
                    existingCustomer.Id,
                    shouldUpdate,
                    cartUpdated,
                    reactivated);

                return new StoreCustomerProvisionResult
                {
                    Customer = existingCustomer,
                    PersistNeeded = shouldUpdate || cartUpdated,
                    WasCreated = false,
                    WasReactivated = reactivated,
                    CartProvisioned = cartUpdated
                };
            }

            var customer = new StoreCustomer
            {
                Id = Guid.NewGuid(),
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
            var cartProvisioned = await EnsureCustomerCartExistsAsync(customer.Id, store.Id);

            _logger.LogInformation(
                "[EnsureCustomerStoreLink] New StoreCustomer prepared. StoreId: {StoreId}, StoreCustomerId: {StoreCustomerId}, Email: {Email}, CartProvisioned: {CartProvisioned}",
                store.Id,
                customer.Id,
                normalizedEmail,
                cartProvisioned);

            return new StoreCustomerProvisionResult
            {
                Customer = customer,
                PersistNeeded = true,
                WasCreated = true,
                WasReactivated = false,
                CartProvisioned = cartProvisioned
            };
        }

        private async Task<bool> EnsureCustomerCartExistsAsync(Guid storeCustomerId, Guid storeId)
        {
            _logger.LogDebug(
                "[EnsureCustomerCartExists] Ensuring cart. StoreId: {StoreId}, StoreCustomerId: {StoreCustomerId}",
                storeId,
                storeCustomerId);

            var hasActiveCart = await _context.Carts
                .AnyAsync(c => c.StoreCustomerId == storeCustomerId
                               && c.StoreId == storeId
                               && !c.IsDeleted);

            if (hasActiveCart)
            {
                _logger.LogDebug(
                    "[EnsureCustomerCartExists] Active cart already exists. StoreId: {StoreId}, StoreCustomerId: {StoreCustomerId}",
                    storeId,
                    storeCustomerId);
                return false;
            }

            var softDeletedCart = await _context.Carts
                .Where(c => c.StoreCustomerId == storeCustomerId
                            && c.StoreId == storeId
                            && c.IsDeleted)
                .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
                .FirstOrDefaultAsync();

            if (softDeletedCart != null)
            {
                softDeletedCart.IsDeleted = false;
                softDeletedCart.UpdatedAt = DateTime.UtcNow;

                _logger.LogInformation(
                    "[EnsureCustomerCartExists] Soft-deleted cart restored. CartId: {CartId}, StoreId: {StoreId}, StoreCustomerId: {StoreCustomerId}",
                    softDeletedCart.Id,
                    storeId,
                    storeCustomerId);
                return true;
            }

            _context.Carts.Add(new ShoppingCart
            {
                StoreCustomerId = storeCustomerId,
                StoreId = storeId,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            });

            _logger.LogInformation(
                "[EnsureCustomerCartExists] New cart prepared for creation. StoreId: {StoreId}, StoreCustomerId: {StoreCustomerId}",
                storeId,
                storeCustomerId);

            return true;
        }

        private sealed class StoreCustomerProvisionResult
        {
            public StoreCustomer Customer { get; set; } = null!;
            public bool PersistNeeded { get; set; }
            public bool WasCreated { get; set; }
            public bool WasReactivated { get; set; }
            public bool CartProvisioned { get; set; }
        }
    }
}
