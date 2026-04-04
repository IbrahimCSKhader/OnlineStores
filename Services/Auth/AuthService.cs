using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using onlineStore.Data;
using onlineStore.DTOs.Auth;
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

        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            IConfiguration configuration,
            AppDbContext context,
            IEmailService emailService,
            ILogger<AuthService> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
        {
            try
            {
                var normalizedEmail = NormalizeEmail(dto.Email);
                var normalizedFirstName = NormalizeValue(dto.FirstName);
                var normalizedLastName = NormalizeValue(dto.LastName);

                var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);
                if (existingUser != null)
                {
                    if (!existingUser.EmailConfirmed)
                    {
                        var codeSent = await TrySendEmailVerificationCodeAsync(
                            existingUser,
                            "registration retry");

                        return CreateVerificationPendingResponse(
                            existingUser,
                            codeSent
                                ? "الحساب موجود لكنه غير مفعل. تم إرسال كود تحقق جديد إلى بريدك الإلكتروني."
                                : "الحساب موجود لكنه غير مفعل. تعذر إرسال كود التحقق حالياً، يمكنك طلب إعادة الإرسال.",
                            new List<string> { "Customer" });
                    }

                    return Fail("البريد الإلكتروني مستخدم مسبقاً");
                }

                var user = new AppUser
                {
                    FirstName = normalizedFirstName,
                    LastName = normalizedLastName,
                    Email = normalizedEmail,
                    UserName = normalizedEmail,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user, dto.Password);

                if (!result.Succeeded)
                    return Fail(GetIdentityErrors(result));

                await _userManager.AddToRoleAsync(user, "Customer");

                _logger.LogInformation("User registered and awaiting email verification: {Email}", user.Email);

                var verificationCodeSent = await TrySendEmailVerificationCodeAsync(
                    user,
                    "registration");

                return CreateVerificationPendingResponse(
                    user,
                    verificationCodeSent
                        ? "تم إنشاء الحساب بنجاح. تم إرسال كود التحقق إلى بريدك الإلكتروني."
                        : "تم إنشاء الحساب بنجاح لكن تعذر إرسال كود التحقق حالياً، يمكنك طلب إعادة الإرسال.",
                    new List<string> { "Customer" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration for {Email}", dto.Email);
                return Fail("حدث خطأ أثناء التسجيل");
            }
        }

        public async Task<AuthResponseDto> VerifyEmailAsync(VerifyEmailDto dto)
        {
            try
            {
                var normalizedEmail = NormalizeEmail(dto.Email);
                var verificationCode = NormalizeValue(dto.Code);

                var user = await _userManager.FindByEmailAsync(normalizedEmail);
                if (user == null)
                    return Fail("البريد الإلكتروني أو كود التحقق غير صحيح");

                if (!user.IsActive)
                    return Fail("الحساب موقوف، تواصل مع الدعم");

                if (user.EmailConfirmed)
                    return Fail("البريد الإلكتروني مفعل مسبقاً، يمكنك تسجيل الدخول مباشرة.");

                var confirmResult = await _userManager.ConfirmEmailAsync(user, verificationCode);

                if (!confirmResult.Succeeded)
                    return Fail(GetIdentityErrors(confirmResult, "كود التحقق غير صحيح أو منتهي الصلاحية"));

                var token = await GenerateJwtToken(user);
                var roles = await _userManager.GetRolesAsync(user);

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

        public async Task<(bool Success, string Message)> ResendVerificationCodeAsync(
            ResendVerificationCodeDto dto)
        {
            try
            {
                var normalizedEmail = NormalizeEmail(dto.Email);
                var user = await _userManager.FindByEmailAsync(normalizedEmail);

                if (user == null || !user.IsActive)
                    return (true, "إذا كان الحساب موجوداً وغير مفعل، فسيتم إرسال كود تحقق جديد.");

                if (user.EmailConfirmed)
                    return (true, "البريد الإلكتروني مفعل مسبقاً.");

                var emailSent = await TrySendEmailVerificationCodeAsync(user, "verification resend");

                return (
                    true,
                    emailSent
                        ? "تم إرسال كود تحقق جديد إلى بريدك الإلكتروني."
                        : "تعذر إرسال كود التحقق حالياً، حاول مرة أخرى بعد قليل.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while resending verification code for {Email}", dto.Email);
                return (false, "حدث خطأ أثناء إعادة إرسال كود التحقق");
            }
        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {
            try
            {
                var normalizedEmail = NormalizeEmail(dto.Email);

                var user = await _userManager.FindByEmailAsync(normalizedEmail);
                if (user == null)
                    return Fail("البريد الإلكتروني أو كلمة المرور غير صحيحة");

                if (!user.IsActive)
                    return Fail("الحساب موقوف، تواصل مع الدعم");

                if (await _userManager.IsLockedOutAsync(user))
                    return Fail("الحساب مقفل مؤقتاً بسبب محاولات خاطئة، حاول بعد 5 دقائق");

                var result = await _signInManager.CheckPasswordSignInAsync(
                    user,
                    dto.Password,
                    lockoutOnFailure: true);

                if (!result.Succeeded)
                    return Fail("البريد الإلكتروني أو كلمة المرور غير صحيحة");

                if (!user.EmailConfirmed)
                {
                    var codeSent = await TrySendEmailVerificationCodeAsync(user, "login");

                    return new AuthResponseDto
                    {
                        Success = false,
                        RequiresEmailVerification = true,
                        Message = codeSent
                            ? "لا يمكنك تسجيل الدخول قبل تفعيل البريد الإلكتروني. تم إرسال كود تحقق جديد إلى بريدك."
                            : "لا يمكنك تسجيل الدخول قبل تفعيل البريد الإلكتروني. تعذر إرسال الكود حالياً، استخدم إعادة الإرسال.",
                        Email = user.Email,
                        FirstName = user.FirstName,
                        LastName = user.LastName
                    };
                }

                var token = await GenerateJwtToken(user);

                _logger.LogInformation("User logged in: {Email}", user.Email);

                var roles = await _userManager.GetRolesAsync(user);

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

        public async Task<(bool Success, string Message)> ForgotPasswordAsync(ForgotPasswordDto dto)
        {
            try
            {
                var normalizedEmail = NormalizeEmail(dto.Email);
                var user = await _userManager.FindByEmailAsync(normalizedEmail);

                if (user == null || !user.IsActive)
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

                var resetResult = await _userManager.ResetPasswordAsync(
                    user,
                    resetCode,
                    dto.NewPassword);

                if (!resetResult.Succeeded)
                {
                    return (
                        false,
                        GetIdentityErrors(
                            resetResult,
                            "كود إعادة تعيين كلمة المرور غير صحيح أو منتهي الصلاحية"));
                }

                await _userManager.UpdateSecurityStampAsync(user);

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
            try
            {
                var normalizedEmail = NormalizeEmail(dto.Email);
                var normalizedFirstName = NormalizeValue(dto.FirstName, allowEmpty: true);
                var normalizedLastName = NormalizeValue(dto.LastName, allowEmpty: true);

                var user = await _userManager.FindByEmailAsync(normalizedEmail);
                var isNewUser = false;

                if (user == null)
                {
                    isNewUser = true;

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

                    var createResult = await _userManager.CreateAsync(user);

                    if (!createResult.Succeeded)
                        return Fail(GetIdentityErrors(createResult));

                    await _userManager.AddToRoleAsync(user, "Customer");
                    _logger.LogInformation("New user registered via Google: {Email}", normalizedEmail);
                }
                else
                {
                    if (!user.IsActive)
                        return Fail("الحساب موقوف، تواصل مع الدعم");

                    if (!user.EmailConfirmed)
                    {
                        user.EmailConfirmed = true;
                        await _userManager.UpdateAsync(user);
                    }

                    _logger.LogInformation("User logged in via Google: {Email}", normalizedEmail);
                }

                var token = await GenerateJwtToken(user);
                var roles = await _userManager.GetRolesAsync(user);

                if (isNewUser)
                    await TrySendWelcomeEmailAsync(user.Email, user.FirstName, "Google registration");

                return CreateAuthenticatedResponse(
                    user,
                    token.Token,
                    token.ExpiresAt,
                    roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Google login for {Email}", dto.Email);
                return Fail("حدث خطأ أثناء تسجيل الدخول بـ Google");
            }
        }

        public async Task<OwnerResponseDto> CreateOwnerAsync(CreateOwnerDto dto)
        {
            var normalizedEmail = NormalizeEmail(dto.Email);

            var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);
            if (existingUser != null)
                throw new Exception("Email is already in use");

            var owner = new AppUser
            {
                FirstName = NormalizeValue(dto.FirstName),
                LastName = NormalizeValue(dto.LastName),
                Email = normalizedEmail,
                UserName = normalizedEmail,
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

        public async Task<(bool Success, string Message)> ChangeUserPasswordBySuperAdminAsync(
            Guid userId,
            string newPassword)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());

                if (user == null)
                    return (false, "user does not exist");

                if (!user.IsActive)
                    return (false, "user does not exist");

                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);

                var result = await _userManager.ResetPasswordAsync(
                    user,
                    resetToken,
                    newPassword);

                if (!result.Succeeded)
                    return (false, GetIdentityErrors(result));

                await _userManager.UpdateSecurityStampAsync(user);

                _logger.LogInformation(
                    "Password changed by SuperAdmin for user: {UserId}",
                    userId);

                return (true, "password changed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error while SuperAdmin changing password for user {UserId}",
                    userId);

                return (false, "there are an error in the proccess");
            }
        }

        private async Task<(string Token, DateTime ExpiresAt)> GenerateJwtToken(AppUser user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"]!;
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var roles = await _userManager.GetRolesAsync(user);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email!),
                new Claim(ClaimTypes.GivenName, user.FirstName),
                new Claim(ClaimTypes.Surname, user.LastName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(
                    JwtRegisteredClaimNames.Iat,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
            };

            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            var expiryDays = int.Parse(jwtSettings["ExpiryInDays"] ?? "7");
            var expiresAt = DateTime.UtcNow.AddDays(expiryDays);

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: expiresAt,
                signingCredentials: credentials);

            return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
        }

        private async Task<bool> TrySendEmailVerificationCodeAsync(AppUser user, string source)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                _logger.LogWarning(
                    "Email verification code skipped after {Source} because the recipient email is missing.",
                    source);
                return false;
            }

            try
            {
                var verificationCode = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var template = EmailTemplateBuilder.BuildEmailVerificationCodeEmail(
                    user.FirstName,
                    verificationCode);

                await _emailService.SendEmailAsync(
                    user.Email,
                    template.Subject,
                    template.HtmlBody);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error while sending verification code to {Email} after {Source}.",
                    user.Email,
                    source);
                return false;
            }
        }

        private async Task<bool> TrySendPasswordResetCodeAsync(AppUser user, string source)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                _logger.LogWarning(
                    "Password reset code skipped after {Source} because the recipient email is missing.",
                    source);
                return false;
            }

            try
            {
                var resetCode = await _userManager.GeneratePasswordResetTokenAsync(user);
                var template = EmailTemplateBuilder.BuildPasswordResetCodeEmail(
                    user.FirstName,
                    resetCode);

                await _emailService.SendEmailAsync(
                    user.Email,
                    template.Subject,
                    template.HtmlBody);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error while sending password reset code to {Email} after {Source}.",
                    user.Email,
                    source);
                return false;
            }
        }

        private async Task TrySendWelcomeEmailAsync(
            string? toEmail,
            string? firstName,
            string source)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                _logger.LogWarning(
                    "Welcome email skipped after {Source} because the recipient email is missing.",
                    source);
                return;
            }

            try
            {
                var template = EmailTemplateBuilder.BuildWelcomeEmail(firstName);

                await _emailService.SendEmailAsync(
                    toEmail,
                    template.Subject,
                    template.HtmlBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error while sending welcome email to {Email} after {Source}. The main flow continued normally.",
                    toEmail,
                    source);
            }
        }

        private AuthResponseDto CreateVerificationPendingResponse(
            AppUser user,
            string message,
            IList<string>? roles = null) => new()
        {
            Success = true,
            RequiresEmailVerification = true,
            Message = message,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Roles = roles
        };

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

        private static string NormalizeEmail(string email) =>
            email.Trim().ToLowerInvariant();

        private static string NormalizeValue(string? value, bool allowEmpty = false)
        {
            var normalizedValue = value?.Trim() ?? string.Empty;

            if (!allowEmpty && string.IsNullOrWhiteSpace(normalizedValue))
                return string.Empty;

            return normalizedValue;
        }

        private static string GetIdentityErrors(
            IdentityResult result,
            string? invalidTokenMessage = null)
        {
            if (!string.IsNullOrWhiteSpace(invalidTokenMessage) &&
                result.Errors.Any(error =>
                    string.Equals(error.Code, "InvalidToken", StringComparison.OrdinalIgnoreCase)))
            {
                return invalidTokenMessage;
            }

            return string.Join(", ", result.Errors.Select(e => e.Description));
        }

        private static AuthResponseDto Fail(string message) => new()
        {
            Success = false,
            Message = message
        };
    }
}
