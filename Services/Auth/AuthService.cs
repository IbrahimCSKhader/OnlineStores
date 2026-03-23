using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using onlineStore.Data;
using onlineStore.Models.Identity;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using onlineStore.DTOs.Auth;
namespace onlineStore.Services.AuthServices
{

    public class AuthService : IAuthService
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _context;
        private readonly ILogger<AuthService> _logger;
        public AuthService(
           UserManager<AppUser> userManager,
           SignInManager<AppUser> signInManager,
           IConfiguration configuration,
           AppDbContext context,
           ILogger<AuthService> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _context = context;
            _logger = logger;
        }
        public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
        {
            try
            {

                var existingUser = await _userManager.FindByEmailAsync(dto.Email);
                if (existingUser != null)
                    return Fail("البريد الإلكتروني مستخدم مسبقاً");

                var user = new AppUser
                {
                    FirstName = dto.FirstName.Trim(),
                    LastName = dto.LastName.Trim(),
                    Email = dto.Email.Trim().ToLower(),
                    UserName = dto.Email.Trim().ToLower(),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user, dto.Password);

                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return Fail(errors);
                }

                await _userManager.AddToRoleAsync(user, "Customer");

                var token = await GenerateJwtToken(user);

                _logger.LogInformation("User registered: {Email}", user.Email);

                return new AuthResponseDto
                {
                    Success = true,
                    Token = token.Token,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Roles = new List<string> { "Customer" },
                    ExpiresAt = token.ExpiresAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration for {Email}", dto.Email);
                return Fail("حدث خطأ أثناء التسجيل");
            }
        }



        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {
            try
            {

                var user = await _userManager.FindByEmailAsync(dto.Email.Trim().ToLower());
                if (user == null)
                    return Fail("البريد الإلكتروني أو كلمة المرور غير صحيحة");

                if (!user.IsActive)
                    return Fail("الحساب موقوف، تواصل مع الدعم");

                if (await _userManager.IsLockedOutAsync(user))
                    return Fail("الحساب مقفل مؤقتاً بسبب محاولات خاطئة، حاول بعد 5 دقائق");

                var result = await _signInManager.CheckPasswordSignInAsync(
                    user, dto.Password, lockoutOnFailure: true);

                if (!result.Succeeded)
                    return Fail("البريد الإلكتروني أو كلمة المرور غير صحيحة");

                var token = await GenerateJwtToken(user);

                _logger.LogInformation("User logged in: {Email}", user.Email);

                var roles = await _userManager.GetRolesAsync(user);

                return new AuthResponseDto
                {
                    Success = true,
                    Token = token.Token,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Roles = roles,
                    ExpiresAt = token.ExpiresAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for {Email}", dto.Email);
                return Fail("حدث خطأ أثناء تسجيل الدخول");
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
                new Claim(JwtRegisteredClaimNames.Iat,
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
                signingCredentials: credentials
            );

            return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
        }



        private static AuthResponseDto Fail(string message) => new()
        {
            Success = false,
            Message = message
        };public async Task<AuthResponseDto> GoogleLoginAsync(GoogleAuthDto dto)
{
    try
    {
        // ════════════════════════════════════════════════════
        // 1️⃣ تحقق إذا اليوزر موجود
        // ════════════════════════════════════════════════════
        var user = await _userManager.FindByEmailAsync(dto.Email);

        if (user == null)
        {
            // ════════════════════════════════════════════════════
            // 2️⃣ لو مش موجود — اعمل حساب جديد تلقائياً
            // ════════════════════════════════════════════════════
            user = new AppUser
            {
                Email = dto.Email,
                UserName = dto.Email,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
                // 🔐 بدون باسورد — لأنه بيسجل بـ Google
            };

            var createResult = await _userManager.CreateAsync(user);

            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors
                    .Select(e => e.Description));
                return Fail(errors);
            }

            await _userManager.AddToRoleAsync(user, "Customer");
            _logger.LogInformation("New user registered via Google: {Email}", dto.Email);
        }
        else
        {
            // ════════════════════════════════════════════════════
            // 3️⃣ لو موجود — تحقق إن الحساب مش موقوف
            // ════════════════════════════════════════════════════
            if (!user.IsActive)
                return Fail("الحساب موقوف، تواصل مع الدعم");

            _logger.LogInformation("User logged in via Google: {Email}", dto.Email);
        }

        // ════════════════════════════════════════════════════
        // 4️⃣ اعمل JWT Token وارجعه
        // ════════════════════════════════════════════════════
        var token = await GenerateJwtToken(user);
        var roles = await _userManager.GetRolesAsync(user);

        return new AuthResponseDto
        {
            Success = true,
            Token = token.Token,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Roles = roles,
            ExpiresAt = token.ExpiresAt
        };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error during Google login for {Email}", dto.Email);
        return Fail("حدث خطأ أثناء تسجيل الدخول بـ Google");
    }
}
        public async Task<AuthResponseDto> GoogleLoginAsync(GoogleAuthDto dto)
        {
            try
            {
                // ════════════════════════════════════════════════════
                // 1️⃣ تحقق إذا اليوزر موجود
                // ════════════════════════════════════════════════════
                var user = await _userManager.FindByEmailAsync(dto.Email);

                if (user == null)
                {
                    // ════════════════════════════════════════════════════
                    // 2️⃣ لو مش موجود — اعمل حساب جديد تلقائياً
                    // ════════════════════════════════════════════════════
                    user = new AppUser
                    {
                        Email = dto.Email,
                        UserName = dto.Email,
                        FirstName = dto.FirstName,
                        LastName = dto.LastName,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                        // 🔐 بدون باسورد — لأنه بيسجل بـ Google
                    };

                    var createResult = await _userManager.CreateAsync(user);

                    if (!createResult.Succeeded)
                    {
                        var errors = string.Join(", ", createResult.Errors
                            .Select(e => e.Description));
                        return Fail(errors);
                    }

                    await _userManager.AddToRoleAsync(user, "Customer");
                    _logger.LogInformation("New user registered via Google: {Email}", dto.Email);
                }
                else
                {
                    // ════════════════════════════════════════════════════
                    // 3️⃣ لو موجود — تحقق إن الحساب مش موقوف
                    // ════════════════════════════════════════════════════
                    if (!user.IsActive)
                        return Fail("الحساب موقوف، تواصل مع الدعم");

                    _logger.LogInformation("User logged in via Google: {Email}", dto.Email);
                }

                // ════════════════════════════════════════════════════
                // 4️⃣ اعمل JWT Token وارجعه
                // ════════════════════════════════════════════════════
                var token = await GenerateJwtToken(user);
                var roles = await _userManager.GetRolesAsync(user);

                return new AuthResponseDto
                {
                    Success = true,
                    Token = token.Token,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Roles = roles,
                    ExpiresAt = token.ExpiresAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Google login for {Email}", dto.Email);
                return Fail("حدث خطأ أثناء تسجيل الدخول بـ Google");
            }
        }
    }
}

