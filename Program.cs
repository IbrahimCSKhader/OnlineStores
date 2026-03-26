using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using onlineStore.Data;
using onlineStore.Models.Identity;
using onlineStore.Services.AuthServices;
using onlineStore.Services.Cart;
using onlineStore.Services.Coupon;
using onlineStore.Services.Order;
using onlineStore.Services.Product;
using onlineStore.Services.Review;
using System.Text;
using Scalar.AspNetCore;


var builder = WebApplication.CreateBuilder(args);


// ════════════════════════════════════════════════════
// 1️⃣ Controllers
// ════════════════════════════════════════════════════
builder.Services.AddControllers();


// ════════════════════════════════════════════════════
// 2️⃣ DbContext
// EnableRetryOnFailure = لو الداتابيس انقطع لحظياً
// بيحاول تاني بدل ما يطير error فوراً
// ════════════════════════════════════════════════════
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null
        )
    )
);


// ════════════════════════════════════════════════════
// 3️⃣ Identity
// ════════════════════════════════════════════════════
builder.Services.AddIdentity<AppUser, AppRole>(options =>
{
    // ── Password ──
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireLowercase = true;

    // ── User ──
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedAccount = false;

    // ── Lockout — حماية من Brute Force ──
    // بعد 5 محاولات فاشلة = قفل 5 دقائق
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();


// ════════════════════════════════════════════════════
// 4️⃣ JWT Authentication
// ════════════════════════════════════════════════════
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"]
    ?? throw new InvalidOperationException("JWT SecretKey not configured");
var key = Encoding.UTF8.GetBytes(secretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ClockSkew = TimeSpan.Zero
    };
})
.AddGoogle(options =>
{
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
});



// ════════════════════════════════════════════════════
// 5️⃣ CORS
// ════════════════════════════════════════════════════
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevelopmentPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });

    options.AddPolicy("ProductionPolicy", policy =>
    {
        policy.WithOrigins(
                "https://yourdomain.com",
                "https://www.yourdomain.com"
              )
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});


// ════════════════════════════════════════════════════
// 6️⃣ Swagger — .NET 10 Built-in OpenAPI
// ════════════════════════════════════════════════════
builder.Services.AddOpenApi();


// ════════════════════════════════════════════════════
// 7️⃣ Services
// ════════════════════════════════════════════════════
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IProductService, ProductService>();
 builder.Services.AddScoped<IOrderService, OrderService>();
 builder.Services.AddScoped<ICouponService, CouponService>();
 builder.Services.AddScoped<IReviewService, ReviewService>();


var app = builder.Build();


// ════════════════════════════════════════════════════
// 8️⃣ Middleware Pipeline
// ⚠️ الترتيب مهم جداً — لا تغيره
// ════════════════════════════════════════════════════

// Global Exception Handler — لازم يكون أول شي
app.UseMiddleware<GlobalExceptionHandler>();

// Swagger في Development بس
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(); 
}
//builder.Services.AddAuthentication()
//    .AddGoogle(options =>
//    {
//        options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
//        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
//    });
app.UseHttpsRedirection();

// CORS حسب البيئة
app.UseCors(app.Environment.IsDevelopment()
    ? "DevelopmentPolicy"
    : "ProductionPolicy");

app.UseAuthentication();  // مين أنت؟
app.UseAuthorization();   // شو مسموح لك؟

app.MapControllers();


// ════════════════════════════════════════════════════
// 9️⃣ Seed Data
// ════════════════════════════════════════════════════
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider
        .GetRequiredService<AppDbContext>();

    await context.Database.MigrateAsync();

    var userManager = scope.ServiceProvider
        .GetRequiredService<UserManager<AppUser>>();
    var roleManager = scope.ServiceProvider
        .GetRequiredService<RoleManager<AppRole>>();

    await SeedRolesAndAdmin(userManager, roleManager);
}

app.Run();


// ════════════════════════════════════════════════════
// 🌱 SeedRolesAndAdmin
// ════════════════════════════════════════════════════
async Task SeedRolesAndAdmin(
    UserManager<AppUser> userManager,
    RoleManager<AppRole> roleManager)
{
    string[] roles = { "SuperAdmin", "StoreOwner", "Customer" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new AppRole { Name = role });
    }

    var adminEmail = "admin@onlinestore.com";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);

    if (adminUser == null)
    {
        adminUser = new AppUser
        {
            FirstName = "Super",
            LastName = "Admin",
            Email = adminEmail,
            UserName = adminEmail,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(adminUser, "Admin@12345");
        if (result.Succeeded)
            await userManager.AddToRoleAsync(adminUser, "SuperAdmin");
    }
}


// ════════════════════════════════════════════════════
// 🛡️ GlobalExceptionHandler
// ════════════════════════════════════════════════════
public class GlobalExceptionHandler
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(
        RequestDelegate next,
        ILogger<GlobalExceptionHandler> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred");

            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";

            var isDev = context.RequestServices
                .GetRequiredService<IWebHostEnvironment>()
                .IsDevelopment();

            var response = new
            {
                success = false,
                statusCode = 500,
                message = "An unexpected error occurred",
                detail = isDev ? ex.Message : null
            };

            await context.Response.WriteAsJsonAsync(response);
        }
    }
}