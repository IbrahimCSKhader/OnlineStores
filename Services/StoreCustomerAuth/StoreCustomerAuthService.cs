using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using onlineStore.Data;
using onlineStore.DTOs.StoreCustomerAuth;
using onlineStore.Models;
using onlineStore.Security;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace onlineStore.Services.StoreCustomerAuth
{
    public class StoreCustomerAuthService : IStoreCustomerAuthService
    {
        private const string GenericForgotPasswordMessage =
            "If this account exists in the selected store, a password reset code has been sent to the email address.";

        private const string StoreOwnerCustomerConflictMessage =
            "This email belongs to the store owner for this store. Use the platform owner authentication flow instead.";

        private readonly AppDbContext _context;
        private readonly IPasswordHasher<StoreCustomer> _passwordHasher;
        private readonly IConfiguration _configuration;
        private readonly IStoreAccountBoundaryService _storeAccountBoundaryService;
        private readonly IStoreCustomerEmailWorkflowService _emailWorkflowService;
        private readonly ILogger<StoreCustomerAuthService> _logger;

        public StoreCustomerAuthService(
            AppDbContext context,
            IPasswordHasher<StoreCustomer> passwordHasher,
            IConfiguration configuration,
            IStoreAccountBoundaryService storeAccountBoundaryService,
            IStoreCustomerEmailWorkflowService emailWorkflowService,
            ILogger<StoreCustomerAuthService> logger)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _configuration = configuration;
            _storeAccountBoundaryService = storeAccountBoundaryService;
            _emailWorkflowService = emailWorkflowService;
            _logger = logger;
        }

        public async Task<StoreCustomerAuthResponseDto> RegisterAsync(StoreCustomerRegisterDto dto)
        {
            try
            {
                var store = await _context.Stores
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == dto.StoreId);

                if (store == null || !store.IsActive)
                    return Fail("The selected store is not available.");

                var normalizedEmail = NormalizeEmail(dto.Email);

                if (await IsReservedForStoreOwnerAsync(dto.StoreId, normalizedEmail))
                    return Fail(StoreOwnerCustomerConflictMessage);

                var exists = await _context.StoreCustomers
                    .IgnoreQueryFilters()
                    .AnyAsync(c => c.StoreId == dto.StoreId && c.Email == normalizedEmail);

                if (exists)
                    return Fail("This email is already registered in the selected store.");

                var customer = new StoreCustomer
                {
                    StoreId = dto.StoreId,
                    FirstName = NormalizeValue(dto.FirstName),
                    LastName = NormalizeValue(dto.LastName),
                    Email = normalizedEmail,
                    Phone = NormalizeOptionalValue(dto.Phone),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                customer.PasswordHash = _passwordHasher.HashPassword(customer, dto.Password);

                var verificationCode = _emailWorkflowService.PrepareEmailVerification(customer);

                _context.StoreCustomers.Add(customer);
                await _context.SaveChangesAsync();

                var emailSent = await _emailWorkflowService.SendEmailVerificationCodeAsync(
                    customer,
                    verificationCode,
                    "store customer registration");

                _logger.LogInformation(
                    "Store customer registered. Verification required. StoreId: {StoreId}, StoreCustomerId: {StoreCustomerId}",
                    customer.StoreId,
                    customer.Id);

                return RegistrationPendingVerification(
                    customer,
                    emailSent
                        ? "Registration completed. A verification code has been sent to your email."
                        : "Registration completed, but the verification email could not be sent right now. Please request a new code.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while registering store customer for store {StoreId}", dto.StoreId);
                return Fail("An unexpected error occurred while registering the account.");
            }
        }

        public async Task<StoreCustomerAuthResponseDto> LoginAsync(StoreCustomerLoginDto dto)
        {
            try
            {
                var normalizedEmail = NormalizeEmail(dto.Email);

                if (await IsReservedForStoreOwnerAsync(dto.StoreId, normalizedEmail))
                    return Fail(StoreOwnerCustomerConflictMessage);

                var customer = await _context.StoreCustomers
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(c => c.StoreId == dto.StoreId && c.Email == normalizedEmail);

                if (customer == null)
                    return Fail("Invalid email or password.");

                if (customer.IsDeleted)
                    return Fail("This account is no longer available.");

                if (!customer.IsActive)
                    return Fail("This account is inactive.");

                if (!customer.EmailConfirmed)
                    return VerificationRequired(
                        customer,
                        "Your email is not verified yet. Complete verification before logging in.");

                var verificationResult = _passwordHasher.VerifyHashedPassword(
                    customer,
                    customer.PasswordHash,
                    dto.Password);

                if (verificationResult == PasswordVerificationResult.Failed)
                    return Fail("Invalid email or password.");

                if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    customer.PasswordHash = _passwordHasher.HashPassword(customer, dto.Password);
                    await _context.SaveChangesAsync();
                }

                var token = GenerateJwtToken(customer);

                _logger.LogInformation(
                    "Store customer logged in successfully. StoreId: {StoreId}, StoreCustomerId: {StoreCustomerId}",
                    customer.StoreId,
                    customer.Id);

                return Success(customer, token.Token, token.ExpiresAt, "Login completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while logging in store customer for store {StoreId}", dto.StoreId);
                return Fail("An unexpected error occurred while logging in.");
            }
        }

        public async Task<StoreCustomerAuthResponseDto> VerifyEmailAsync(StoreCustomerVerifyEmailDto dto)
        {
            try
            {
                if (await IsReservedForStoreOwnerAsync(dto.StoreId, dto.Email))
                    return Fail(StoreOwnerCustomerConflictMessage);

                var customer = await FindStoreCustomerAsync(dto.StoreId, dto.Email);
                if (customer == null)
                    return Fail("Invalid email verification data.");

                if (!customer.IsActive || customer.IsDeleted)
                    return Fail("This account is not available.");

                if (customer.EmailConfirmed)
                    return Fail("Email is already verified. Please log in.");

                if (!_emailWorkflowService.IsValidEmailVerificationCode(customer, dto.Code))
                    return Fail("The verification code is invalid or expired.");

                _emailWorkflowService.ConfirmEmail(customer);
                await _context.SaveChangesAsync();

                var authToken = GenerateJwtToken(customer);

                await _emailWorkflowService.SendWelcomeEmailAsync(customer, "store customer email verification");

                _logger.LogInformation(
                    "Store customer email verified successfully. StoreId: {StoreId}, StoreCustomerId: {StoreCustomerId}",
                    customer.StoreId,
                    customer.Id);

                return Success(customer, authToken.Token, authToken.ExpiresAt, "Email verified successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while verifying email for store customer in store {StoreId}", dto.StoreId);
                return Fail("An unexpected error occurred while verifying the email.");
            }
        }

        public async Task<(bool Success, string Message)> ResendVerificationCodeAsync(StoreCustomerResendVerificationCodeDto dto)
        {
            try
            {
                if (await IsReservedForStoreOwnerAsync(dto.StoreId, dto.Email))
                    return (false, StoreOwnerCustomerConflictMessage);

                var customer = await FindStoreCustomerAsync(dto.StoreId, dto.Email);

                if (customer == null || !customer.IsActive || customer.IsDeleted)
                    return (true, "If this account exists and still needs verification, a new code has been sent.");

                if (customer.EmailConfirmed)
                    return (true, "This email is already verified.");

                var verificationCode = _emailWorkflowService.PrepareEmailVerification(customer);
                await _context.SaveChangesAsync();

                var emailSent = await _emailWorkflowService.SendEmailVerificationCodeAsync(
                    customer,
                    verificationCode,
                    "store customer verification resend");

                return (
                    true,
                    emailSent
                        ? "A new verification code has been sent to your email."
                        : "The account still requires verification, but the email could not be sent right now.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while resending verification code for store customer in store {StoreId}", dto.StoreId);
                return (false, "An unexpected error occurred while resending the verification code.");
            }
        }

        public async Task<(bool Success, string Message)> ForgotPasswordAsync(StoreCustomerForgotPasswordDto dto)
        {
            try
            {
                var normalizedEmail = NormalizeEmail(dto.Email);

                if (dto.StoreId == Guid.Empty)
                {
                    _logger.LogWarning(
                        "Store customer forgot password requested without a valid StoreId. Email: {Email}",
                        normalizedEmail);

                    return (true, GenericForgotPasswordMessage);
                }

                if (await IsReservedForStoreOwnerAsync(dto.StoreId, normalizedEmail))
                    return (true, GenericForgotPasswordMessage);

                var customer = await FindStoreCustomerAsync(dto.StoreId, normalizedEmail);

                if (customer == null || !customer.IsActive || customer.IsDeleted)
                {
                    _logger.LogWarning(
                        "Store customer forgot password request did not match an active customer. StoreId: {StoreId}, Email: {Email}, CustomerFound: {CustomerFound}, IsActive: {IsActive}, IsDeleted: {IsDeleted}",
                        dto.StoreId,
                        normalizedEmail,
                        customer != null,
                        customer?.IsActive,
                        customer?.IsDeleted);

                    return (true, GenericForgotPasswordMessage);
                }

                var resetCode = _emailWorkflowService.PreparePasswordReset(customer);
                await _context.SaveChangesAsync();

                await _emailWorkflowService.SendPasswordResetCodeAsync(
                    customer,
                    resetCode,
                    "store customer forgot password");

                return (true, GenericForgotPasswordMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during forgot password for store customer in store {StoreId}", dto.StoreId);
                return (false, "An unexpected error occurred while requesting password reset.");
            }
        }

        public async Task<(bool Success, string Message)> ResetPasswordAsync(StoreCustomerResetPasswordDto dto)
        {
            try
            {
                var normalizedEmail = NormalizeEmail(dto.Email);
                var normalizedCode = NormalizeValue(dto.Code);

                if (dto.StoreId == Guid.Empty)
                {
                    _logger.LogWarning(
                        "Store customer password reset failed because StoreId was empty. Email: {Email}",
                        normalizedEmail);

                    return (false, "StoreId is required for store customer password reset.");
                }

                if (await IsReservedForStoreOwnerAsync(dto.StoreId, normalizedEmail))
                    return (false, "Invalid password reset data.");

                var customer = await FindStoreCustomerAsync(dto.StoreId, normalizedEmail);

                if (customer == null || !customer.IsActive || customer.IsDeleted)
                {
                    _logger.LogWarning(
                        "Store customer password reset failed because the account context was invalid. StoreId: {StoreId}, Email: {Email}, CustomerFound: {CustomerFound}, IsActive: {IsActive}, IsDeleted: {IsDeleted}",
                        dto.StoreId,
                        normalizedEmail,
                        customer != null,
                        customer?.IsActive,
                        customer?.IsDeleted);

                    return (false, "Invalid password reset data.");
                }

                if (!_emailWorkflowService.IsValidPasswordResetCode(customer, normalizedCode))
                {
                    _logger.LogWarning(
                        "Store customer password reset failed because the reset code was invalid or expired. StoreId: {StoreId}, StoreCustomerId: {StoreCustomerId}, Email: {Email}",
                        customer.StoreId,
                        customer.Id,
                        customer.Email);

                    return (false, "The password reset code is invalid or expired.");
                }

                customer.PasswordHash = _passwordHasher.HashPassword(customer, dto.NewPassword);
                _emailWorkflowService.ClearPasswordReset(customer);

                await _context.SaveChangesAsync();

                await _emailWorkflowService.SendPasswordResetConfirmationAsync(
                    customer,
                    "store customer password reset");

                _logger.LogInformation(
                    "Store customer password reset completed successfully. StoreId: {StoreId}, StoreCustomerId: {StoreCustomerId}",
                    customer.StoreId,
                    customer.Id);

                return (true, "Password reset completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during password reset for store customer in store {StoreId}", dto.StoreId);
                return (false, "An unexpected error occurred while resetting the password.");
            }
        }

        public async Task<(bool Success, string Message)> SetPasswordAsync(
            Guid storeCustomerId,
            Guid storeId,
            StoreCustomerSetPasswordDto dto)
        {
            try
            {
                if (dto.NewPassword != dto.ConfirmPassword)
                    return (false, "Password confirmation does not match.");

                var customer = await _context.StoreCustomers
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(c => c.Id == storeCustomerId && c.StoreId == storeId);

                return await SetPasswordInternalAsync(customer, dto.NewPassword, "store customer set password");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error while setting password for store customer. StoreId: {StoreId}, StoreCustomerId: {StoreCustomerId}",
                    storeId,
                    storeCustomerId);

                return (false, "An unexpected error occurred while setting the password.");
            }
        }

        public async Task<(bool Success, string Message)> SetPasswordByEmailAsync(
            string email,
            Guid storeId,
            StoreCustomerSetPasswordDto dto)
        {
            try
            {
                if (dto.NewPassword != dto.ConfirmPassword)
                    return (false, "Password confirmation does not match.");

                var normalizedEmail = NormalizeEmail(email);

                if (await IsReservedForStoreOwnerAsync(storeId, normalizedEmail))
                    return (false, StoreOwnerCustomerConflictMessage);

                var customer = await _context.StoreCustomers
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(c => c.StoreId == storeId && c.Email == normalizedEmail);

                return await SetPasswordInternalAsync(customer, dto.NewPassword, "store customer set password from auth user");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error while setting password by email for store customer. StoreId: {StoreId}, Email: {Email}",
                    storeId,
                    email);

                return (false, "An unexpected error occurred while setting the password.");
            }
        }

        private (string Token, DateTime ExpiresAt) GenerateJwtToken(StoreCustomer customer, bool isGuest = false)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"]
                ?? throw new InvalidOperationException("JWT SecretKey not configured");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expiresAt = DateTime.UtcNow.AddDays(int.Parse(jwtSettings["ExpiryInDays"] ?? "7"));

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, customer.Id.ToString()),
                new(StoreCustomerClaimTypes.StoreCustomerId, customer.Id.ToString()),
                new(StoreCustomerClaimTypes.StoreId, customer.StoreId.ToString()),
                new(StoreCustomerClaimTypes.AccountType, StoreCustomerClaimTypes.StoreCustomerAccountType),
                new(StoreCustomerClaimTypes.IsGuest, isGuest.ToString().ToLowerInvariant()),
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

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: expiresAt,
                signingCredentials: credentials);

            return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
        }

        private async Task<StoreCustomer?> FindStoreCustomerAsync(Guid storeId, string email) =>
            await _context.StoreCustomers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.StoreId == storeId && c.Email == NormalizeEmail(email));

        private Task<bool> IsReservedForStoreOwnerAsync(Guid storeId, string email) =>
            _storeAccountBoundaryService.IsStoreOwnerEmailAsync(storeId, NormalizeEmail(email));

        private async Task<(bool Success, string Message)> SetPasswordInternalAsync(
            StoreCustomer? customer,
            string newPassword,
            string source)
        {
            if (customer == null || customer.IsDeleted)
                return (false, "Store customer account was not found.");

            if (!customer.IsActive)
                return (false, "This account is inactive.");

            if (await IsReservedForStoreOwnerAsync(customer.StoreId, customer.Email))
                return (false, StoreOwnerCustomerConflictMessage);

            customer.PasswordHash = _passwordHasher.HashPassword(customer, newPassword);
            _emailWorkflowService.ClearPasswordReset(customer);

            await _context.SaveChangesAsync();

            await _emailWorkflowService.SendPasswordResetConfirmationAsync(customer, source);

            _logger.LogInformation(
                "Store customer password was set successfully. StoreId: {StoreId}, StoreCustomerId: {StoreCustomerId}, Email: {Email}, Source: {Source}",
                customer.StoreId,
                customer.Id,
                customer.Email,
                source);

            return (true, "Password set successfully.");
        }

        private static StoreCustomerAuthResponseDto Success(
            StoreCustomer customer,
            string token,
            DateTime expiresAt,
            string? message = null,
            bool isGuest = false) => new()
        {
            Success = true,
            RequiresEmailVerification = false,
            IsGuest = isGuest,
            Message = message,
            Token = token,
            StoreCustomerId = customer.Id,
            StoreId = customer.StoreId,
            Email = customer.Email,
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            ExpiresAt = expiresAt
        };

        private static StoreCustomerAuthResponseDto VerificationRequired(
            StoreCustomer customer,
            string message) => new()
        {
            Success = false,
            RequiresEmailVerification = true,
            IsGuest = false,
            Message = message,
            StoreCustomerId = customer.Id,
            StoreId = customer.StoreId,
            Email = customer.Email,
            FirstName = customer.FirstName,
            LastName = customer.LastName
        };

        private static StoreCustomerAuthResponseDto RegistrationPendingVerification(
            StoreCustomer customer,
            string message) => new()
        {
            Success = true,
            RequiresEmailVerification = true,
            IsGuest = false,
            Message = message,
            StoreCustomerId = customer.Id,
            StoreId = customer.StoreId,
            Email = customer.Email,
            FirstName = customer.FirstName,
            LastName = customer.LastName
        };

        private static StoreCustomerAuthResponseDto Fail(string message) => new()
        {
            Success = false,
            RequiresEmailVerification = false,
            IsGuest = false,
            Message = message
        };

        private static string NormalizeEmail(string email) =>
            email.Trim().ToLowerInvariant();

        private static string NormalizeValue(string value) =>
            value.Trim();

        private static string? NormalizeOptionalValue(string? value)
        {
            var normalizedValue = value?.Trim();
            return string.IsNullOrWhiteSpace(normalizedValue) ? null : normalizedValue;
        }
    }
}
