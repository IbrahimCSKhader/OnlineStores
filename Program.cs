using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using onlineStore.Data;
using onlineStore.Models.Identity;
using onlineStore.Security;
using onlineStore.Serialization;
using onlineStore.Services.AuthServices;
using onlineStore.Services.Cart;
using onlineStore.Services.Category;
using onlineStore.Services.Coupon;
using onlineStore.Services.CustomerStore;
using onlineStore.Services.Email;
using onlineStore.Services.Order;
using onlineStore.Services.Product;
using onlineStore.Services.Review;
using onlineStore.Services.Section;
using onlineStore.Services.Store;
using onlineStore.Settings;
using Scalar.AspNetCore;
using System.Diagnostics;
using System.Security.Claims;
using System.Text;
var builder = WebApplication.CreateBuilder(args);
var productionCorsOrigins = new HashSet<string>(
    (builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? ["https://onlinestoresfrontend.onrender.com", "http://localhost:5173"])
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Select(origin => origin.TrimEnd('/')),
    StringComparer.OrdinalIgnoreCase);

static bool IsAllowedCorsOrigin(string origin, HashSet<string> allowedOrigins)
{
    if (string.IsNullOrWhiteSpace(origin))
    {
        return false;
    }

    var normalizedOrigin = origin.TrimEnd('/');

    if (allowedOrigins.Contains(normalizedOrigin))
    {
        return true;
    }

    return Uri.TryCreate(normalizedOrigin, UriKind.Absolute, out var uri)
        && uri.IsLoopback;
}


// ════════════════════════════════════════════════════
// 1️⃣ Controllers
// ════════════════════════════════════════════════════
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new EmptyStringToNullableGuidConverter());
    });


// ════════════════════════════════════════════════════
// 2️⃣ DbContext
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

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IStoreOwnershipService, StoreOwnershipService>();
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection(EmailSettings.SectionName));


// ════════════════════════════════════════════════════
// 3️⃣ Identity
// ════════════════════════════════════════════════════
builder.Services.AddIdentity<AppUser, AppRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireLowercase = true;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedAccount = false;
    options.Tokens.EmailConfirmationTokenProvider = TokenOptions.DefaultEmailProvider;
    options.Tokens.PasswordResetTokenProvider = TokenOptions.DefaultEmailProvider;
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
if (string.IsNullOrWhiteSpace(secretKey))
    throw new InvalidOperationException("JWT SecretKey is empty.");

var key = Encoding.UTF8.GetBytes(secretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.MapInboundClaims = true;
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.SaveToken = true;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ClockSkew = TimeSpan.Zero,

        RoleClaimType = ClaimTypes.Role,
        NameClaimType = ClaimTypes.NameIdentifier
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            Debug.WriteLine("========== JWT OnMessageReceived ==========");
            Debug.WriteLine("PATH: " + context.HttpContext.Request.Path);

            var authHeader = context.Request.Headers.Authorization.ToString();
            Debug.WriteLine("AUTH HEADER: " +
                (string.IsNullOrWhiteSpace(authHeader) ? "EMPTY" : authHeader));

            Debug.WriteLine("RAW TOKEN FROM CONTEXT: " +
                (string.IsNullOrWhiteSpace(context.Token) ? "NULL / EMPTY" : context.Token));

            return Task.CompletedTask;
        },

        OnTokenValidated = context =>
        {
            Debug.WriteLine("========== JWT OnTokenValidated ==========");
            Debug.WriteLine("AUTHENTICATED: true");

            var identity = context.Principal?.Identity;
            Debug.WriteLine("AUTH TYPE: " + identity?.AuthenticationType);
            Debug.WriteLine("IS AUTHENTICATED: " + identity?.IsAuthenticated);

            if (context.Principal != null)
            {
                foreach (var claim in context.Principal.Claims)
                {
                    Debug.WriteLine($"CLAIM => Type: {claim.Type} | Value: {claim.Value}");
                }
            }

            return Task.CompletedTask;
        },

        OnAuthenticationFailed = context =>
        {
            Debug.WriteLine("========== JWT OnAuthenticationFailed ==========");
            Debug.WriteLine("JWT ERROR TYPE: " + context.Exception.GetType().Name);
            Debug.WriteLine("JWT ERROR MESSAGE: " + context.Exception.Message);

            if (context.Exception.InnerException != null)
            {
                Debug.WriteLine("JWT INNER ERROR: " + context.Exception.InnerException.Message);
            }

            return Task.CompletedTask;
        },

        OnChallenge = context =>
        {
            Debug.WriteLine("========== JWT OnChallenge ==========");
            Debug.WriteLine("ERROR: " + context.Error);
            Debug.WriteLine("ERROR DESCRIPTION: " + context.ErrorDescription);

            return Task.CompletedTask;
        },

        OnForbidden = context =>
        {
            Debug.WriteLine("========== JWT OnForbidden ==========");
            Debug.WriteLine("User is authenticated but NOT authorized.");

            var user = context.HttpContext.User;

            if (user?.Identity != null)
            {
                Debug.WriteLine("IS AUTHENTICATED: " + user.Identity.IsAuthenticated);
                Debug.WriteLine("AUTH TYPE: " + user.Identity.AuthenticationType);
            }

            foreach (var claim in user?.Claims ?? Enumerable.Empty<Claim>())
            {
                Debug   .WriteLine($"FORBIDDEN CLAIM => Type: {claim.Type} | Value: {claim.Value}");
            }

            return Task.CompletedTask;
        }
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
        policy.SetIsOriginAllowed(origin =>
              IsAllowedCorsOrigin(origin, productionCorsOrigins))
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});


// ════════════════════════════════════════════════════
// 6️⃣ OpenAPI + Scalar
// ════════════════════════════════════════════════════
builder.Services.AddOpenApi();


// ════════════════════════════════════════════════════
// 7️⃣ Services
// ════════════════════════════════════════════════════
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<ICouponService, CouponService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IStoreService, StoreService>();
builder.Services.AddScoped<ISectionService, SectionService>();
builder.Services.AddScoped<ICustomerStoreService, CustomerStoreService>();

// ════════════════════════════════════════════════════
var app = builder.Build();
Console.WriteLine("ContentRootPath: " + app.Environment.ContentRootPath);
Console.WriteLine("WebRootPath: " + app.Environment.WebRootPath);
var logger = app.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("StartupPaths");

logger.LogInformation("ContentRootPath: {Path}", app.Environment.ContentRootPath);
logger.LogInformation("WebRootPath: {Path}", app.Environment.WebRootPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        app.Environment.ContentRootPath
    ),
    RequestPath = ""
});// ════════════════════════════════════════════════════
app.MapGet("/debug-static", (IWebHostEnvironment env) =>
{
    var webRoot = env.WebRootPath;
    var testFile = Path.Combine(webRoot ?? "", "test.txt");

    return Results.Ok(new
    {
        env.ContentRootPath,
        env.WebRootPath,
        testFile,
        testFileExists = System.IO.File.Exists(testFile)
    });
});

// ════════════════════════════════════════════════════
// 8️⃣ Middleware Pipeline
// ⚠️ الترتيب مهم جداً
// ════════════════════════════════════════════════════
// Global Exception Handler — أول شي دايماً
app.UseMiddleware<GlobalExceptionHandler>();

// Scalar — بس في Development
    app.MapOpenApi();
    app.MapScalarApiReference();

app.UseHttpsRedirection();

// CORS حسب البيئة
app.UseCors(app.Environment.IsDevelopment()
    ? "DevelopmentPolicy"
    : "ProductionPolicy");

app.UseAuthentication();
app.UseAuthorization();

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
