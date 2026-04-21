using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using onlineStore.Data;
using onlineStore.Models;
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
using onlineStore.Services.Offer;
using onlineStore.Services.Pricing;
using onlineStore.Services.Product;
using onlineStore.Services.Review;
using onlineStore.Services.Section;
using onlineStore.Services.Store;
using onlineStore.Services.StoreCustomerAuth;
using onlineStore.Services.Subscription;
using onlineStore.Services.SuperAdminDashboard;
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


builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new EmptyStringToNullableGuidConverter());
    });


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
builder.Services.AddScoped<IStoreAuthorizationService, StoreAuthorizationService>();
builder.Services.AddScoped<IStoreAccountBoundaryService, StoreAccountBoundaryService>();
builder.Services.AddScoped<IPasswordHasher<StoreCustomer>, PasswordHasher<StoreCustomer>>();
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection(EmailSettings.SectionName));


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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("StoreCustomerOnly", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim(
            StoreCustomerClaimTypes.AccountType,
            StoreCustomerClaimTypes.StoreCustomerAccountType);
        policy.RequireAssertion(context =>
            !string.Equals(
                context.User.FindFirst(StoreCustomerClaimTypes.IsGuest)?.Value,
                "true",
                StringComparison.OrdinalIgnoreCase));
    });
});


// â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
// 4ï¸ڈâƒ£ JWT Authentication + External Cookie
// â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"]
    ?? throw new InvalidOperationException("JWT SecretKey not configured");
if (string.IsNullOrWhiteSpace(secretKey))
    throw new InvalidOperationException("JWT SecretKey is empty.");

// Validate FrontendSettings for Google Auth
var frontendBaseUrl = builder.Configuration["FrontendSettings:BaseUrl"];
var googleSuccessPath = builder.Configuration["FrontendSettings:GoogleAuthSuccessRedirectPath"];
var googleFailurePath = builder.Configuration["FrontendSettings:GoogleAuthFailureRedirectPath"];

if (string.IsNullOrWhiteSpace(frontendBaseUrl))
    throw new InvalidOperationException("FrontendSettings:BaseUrl is not configured. Required for Google Auth redirects.");

if (string.IsNullOrWhiteSpace(googleSuccessPath))
    throw new InvalidOperationException("FrontendSettings:GoogleAuthSuccessRedirectPath is not configured.");

if (string.IsNullOrWhiteSpace(googleFailurePath))
    throw new InvalidOperationException("FrontendSettings:GoogleAuthFailureRedirectPath is not configured.");

var key = Encoding.UTF8.GetBytes(secretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    // External providers (Google) sign in to a temporary external cookie, then API issues JWT.
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
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
                Debug.WriteLine($"FORBIDDEN CLAIM => Type: {claim.Type} | Value: {claim.Value}");
            }

            return Task.CompletedTask;
        }
    };
})
.AddGoogle(options =>
{
    var clientId = builder.Configuration["Authentication:Google:ClientId"];
    var clientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

    if (string.IsNullOrWhiteSpace(clientId))
        throw new InvalidOperationException("Authentication:Google:ClientId is not configured.");

    if (string.IsNullOrWhiteSpace(clientSecret))
        throw new InvalidOperationException("Authentication:Google:ClientSecret is not configured.");

    options.ClientId = clientId;
    options.ClientSecret = clientSecret;
    options.SignInScheme = IdentityConstants.ExternalScheme;
    // Keep OAuth handler callback separate from MVC endpoint to avoid state/correlation conflicts.
    options.CallbackPath = "/signin-google";

    // Enhanced logging for Google auth events
    options.Events = new Microsoft.AspNetCore.Authentication.OAuth.OAuthEvents
    {
        OnCreatingTicket = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Google OAuth: Creating ticket for user");
            return Task.CompletedTask;
        },
        OnTicketReceived = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Google OAuth: Ticket received, Principal authenticated: {IsAuthenticated}",
                context.Principal?.Identity?.IsAuthenticated ?? false);
            return Task.CompletedTask;
        },
        OnAccessDenied = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILogger<Program>>();
            logger.LogWarning("Google OAuth: Access denied by user");
            return Task.CompletedTask;
        },
        OnRemoteFailure = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILogger<Program>>();
            logger.LogError(context.Failure, "Google OAuth: Remote failure occurred");

            // Prevent unhandled exception bubble-up and redirect user to the same callback flow
            // that originally initiated the Google challenge.
            context.HandleResponse();

            var callbackPath = context.Properties?.RedirectUri;
            if (string.IsNullOrWhiteSpace(callbackPath) ||
                !callbackPath.StartsWith("/", StringComparison.Ordinal) ||
                callbackPath.StartsWith("//", StringComparison.Ordinal))
            {
                callbackPath = "/api/auth/google-callback";
            }

            var query = new Dictionary<string, string?>
            {
                ["remoteError"] = "google_oauth_failed"
            };

            if (context.Properties?.Items != null)
            {
                if (context.Properties.Items.TryGetValue("storeId", out var storeId) && !string.IsNullOrWhiteSpace(storeId))
                    query["storeId"] = storeId;

                if (context.Properties.Items.TryGetValue("storeSlug", out var storeSlug) && !string.IsNullOrWhiteSpace(storeSlug))
                    query["storeSlug"] = storeSlug;

                if (context.Properties.Items.TryGetValue("redirectTo", out var redirectTo) && !string.IsNullOrWhiteSpace(redirectTo))
                    query["redirectTo"] = redirectTo;
            }

            var callbackUrl = QueryHelpers.AddQueryString(callbackPath, query);
            context.Response.Redirect(callbackUrl);
            return Task.CompletedTask;
        }
    };
});


// â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
// 5ï¸ڈâƒ£ CORS
// â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
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


// â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
// 6ï¸ڈâƒ£ OpenAPI + Scalar
// â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
builder.Services.AddOpenApi();


// â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
// 7ï¸ڈâƒ£ Services
// â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<ICouponService, CouponService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<ISubscriptionPlanService, SubscriptionPlanService>();
builder.Services.AddScoped<IStoreSubscriptionService, StoreSubscriptionService>();
builder.Services.AddScoped<IOfferService, OfferService>();
builder.Services.AddScoped<ICartPricingService, CartPricingService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IStoreService, StoreService>();
builder.Services.AddScoped<ISectionService, SectionService>();
builder.Services.AddScoped<ICustomerStoreService, CustomerStoreService>();
builder.Services.AddScoped<IStoreCustomerEmailWorkflowService, StoreCustomerEmailWorkflowService>();
builder.Services.AddScoped<IStoreCustomerAuthService, StoreCustomerAuthService>();
builder.Services.AddScoped<ISuperAdminDashboardService, SuperAdminDashboardService>();
var configuredDataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
var dataProtectionKeysPath = configuredDataProtectionKeysPath;

if (string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    dataProtectionKeysPath = builder.Environment.IsDevelopment()
        ? Path.Combine(builder.Environment.ContentRootPath, "artifacts", "keys")
        : @"D:\Sites\site58172\keys";
}

if (!Path.IsPathRooted(dataProtectionKeysPath))
{
    dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, dataProtectionKeysPath);
}

Directory.CreateDirectory(dataProtectionKeysPath);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("OnlineStoreApp");
var app = builder.Build();
Console.WriteLine("ContentRootPath: " + app.Environment.ContentRootPath);
Console.WriteLine("WebRootPath: " + app.Environment.WebRootPath);
Console.WriteLine("DataProtectionKeysPath: " + dataProtectionKeysPath);
var logger = app.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("StartupPaths");

logger.LogInformation("ContentRootPath: {Path}", app.Environment.ContentRootPath);
logger.LogInformation("WebRootPath: {Path}", app.Environment.WebRootPath);
logger.LogInformation("DataProtectionKeysPath: {Path}", dataProtectionKeysPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        app.Environment.ContentRootPath
    ),
    RequestPath = ""
});// â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
if (app.Environment.IsDevelopment())
{
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

    app.MapGet("/api/diagnostics/auth", async (
        UserManager<AppUser> userManager,
        RoleManager<AppRole> roleManager,
        IConfiguration config,
        ILogger<Program> logger) =>
    {
        try
        {
            logger.LogInformation("Diagnostics endpoint called");

            var rolesExist = new Dictionary<string, bool>
            {
                ["SuperAdmin"] = await roleManager.RoleExistsAsync("SuperAdmin"),
                ["StoreOwner"] = await roleManager.RoleExistsAsync("StoreOwner"),
                ["Customer"] = await roleManager.RoleExistsAsync("Customer")
            };

            var googleClientId = config["Authentication:Google:ClientId"];
            var googleClientSecret = config["Authentication:Google:ClientSecret"];
            var jwtSecret = config["JwtSettings:SecretKey"];
            var jwtIssuer = config["JwtSettings:Issuer"];
            var jwtAudience = config["JwtSettings:Audience"];
            var frontendBaseUrl = config["FrontendSettings:BaseUrl"];
            var successPath = config["FrontendSettings:GoogleAuthSuccessRedirectPath"];
            var failurePath = config["FrontendSettings:GoogleAuthFailureRedirectPath"];

            var result = new
            {
                Timestamp = DateTime.UtcNow,
                Roles = rolesExist,
                GoogleSettings = new
                {
                    ClientIdConfigured = !string.IsNullOrWhiteSpace(googleClientId),
                    ClientIdLength = googleClientId?.Length ?? 0,
                    ClientSecretConfigured = !string.IsNullOrWhiteSpace(googleClientSecret),
                    ClientSecretLength = googleClientSecret?.Length ?? 0
                },
                JwtSettings = new
                {
                    SecretKeyConfigured = !string.IsNullOrWhiteSpace(jwtSecret),
                    SecretKeyLength = jwtSecret?.Length ?? 0,
                    Issuer = jwtIssuer,
                    Audience = jwtAudience
                },
                FrontendSettings = new
                {
                    BaseUrl = frontendBaseUrl,
                    SuccessPath = successPath,
                    FailurePath = failurePath,
                    AllConfigured = !string.IsNullOrWhiteSpace(frontendBaseUrl)
                        && !string.IsNullOrWhiteSpace(successPath)
                        && !string.IsNullOrWhiteSpace(failurePath)
                }
            };

            logger.LogInformation("Diagnostics completed successfully");
            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Diagnostics endpoint failed");
            return Results.Problem($"Diagnostics failed: {ex.Message}");
        }
    });

    app.MapGet("/api/diagnostics/test-auth-service", async (
        IAuthService authService,
        ILogger<Program> logger) =>
    {
        try
        {
            logger.LogInformation("Testing AuthService injection");

            var serviceType = authService?.GetType()?.FullName ?? "null";

            return Results.Ok(new
            {
                ServiceResolved = authService != null,
                ServiceType = serviceType,
                Message = "AuthService resolved successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AuthService test failed");
            return Results.Problem($"AuthService test failed: {ex.Message}");
        }
    });

    app.MapGet("/api/diagnostics/exception-test", (ILogger<Program> logger) =>
    {
        logger.LogInformation("Exception test endpoint called - will throw exception");
        throw new InvalidOperationException("This is a test exception to verify error handling");
    });
}

// â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
// 8ï¸ڈâƒ£ Middleware Pipeline
// âڑ ï¸ڈ ط§ظ„طھط±طھظٹط¨ ظ…ظ‡ظ… ط¬ط¯ط§ظ‹
// â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
// Request Logging Middleware for Google Auth diagnostics
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/auth/google")
        || context.Request.Path.StartsWithSegments("/api/store-customer-auth/google"))
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogInformation(
            "[GoogleAuthPipeline] Request: {Method} {Path}{QueryString} | Headers: {@Headers}",
            context.Request.Method,
            context.Request.Path,
            context.Request.QueryString,
            context.Request.Headers.Select(h => $"{h.Key}: {h.Value}").ToList()
        );

        try
        {
            await next();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[GoogleAuthPipeline] EXCEPTION in pipeline for {Method} {Path}. Type: {ExceptionType}, Message: {Message}",
                context.Request.Method,
                context.Request.Path,
                ex.GetType().Name,
                ex.Message
            );
            throw;
        }

        logger.LogInformation(
            "[GoogleAuthPipeline] Response: {StatusCode} for {Method} {Path}",
            context.Response.StatusCode,
            context.Request.Method,
            context.Request.Path
        );
    }
    else
    {
        await next();
    }
});

// Global Exception Handler â€” ط£ظˆظ„ ط´ظٹ ط¯ط§ظٹظ…ط§ظ‹
app.UseMiddleware<GlobalExceptionHandler>();

// Scalar â€” ط¨ط³ ظپظٹ Development
app.MapOpenApi();
app.MapScalarApiReference();

app.UseHttpsRedirection();

// CORS ط­ط³ط¨ ط§ظ„ط¨ظٹط¦ط©
app.UseCors(app.Environment.IsDevelopment()
    ? "DevelopmentPolicy"
    : "ProductionPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();


using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider
        .GetRequiredService<AppDbContext>();

    var subscriptionService = scope.ServiceProvider
        .GetRequiredService<ISubscriptionService>();

    await context.Database.MigrateAsync();
    await subscriptionService.EnsureDefaultPlansSeededAsync();

    var userManager = scope.ServiceProvider
        .GetRequiredService<UserManager<AppUser>>();
    var roleManager = scope.ServiceProvider
        .GetRequiredService<RoleManager<AppRole>>();

    await SeedRolesAndAdmin(userManager, roleManager);
    await EnsureStoreOwnerRolesForAssignedStores(context, userManager);
}

app.Run();



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
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(adminUser, "Admin@12345");
        if (result.Succeeded)
            await userManager.AddToRoleAsync(adminUser, "SuperAdmin");
    }
    else
    {
        if (!adminUser.EmailConfirmed)
        {
            adminUser.EmailConfirmed = true;
            await userManager.UpdateAsync(adminUser);
        }

        if (!await userManager.IsInRoleAsync(adminUser, "SuperAdmin"))
            await userManager.AddToRoleAsync(adminUser, "SuperAdmin");
    }
}

async Task EnsureStoreOwnerRolesForAssignedStores(
    AppDbContext context,
    UserManager<AppUser> userManager)
{
    var storeOwnerIds = await context.Stores
        .AsNoTracking()
        .Select(store => store.OwnerId)
        .Distinct()
        .ToListAsync();

    foreach (var ownerId in storeOwnerIds)
    {
        var owner = await userManager.FindByIdAsync(ownerId.ToString());
        if (owner == null)
            continue;

        if (!await userManager.IsInRoleAsync(owner, "StoreOwner"))
            await userManager.AddToRoleAsync(owner, "StoreOwner");
    }
}


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
            // Enhanced logging with request details
            var requestPath = context.Request.Path;
            var requestMethod = context.Request.Method;
            var requestQuery = context.Request.QueryString.ToString();

            _logger.LogError(ex,
                "[GlobalException] Unhandled exception occurred.\n" +
                "Path: {Path}\n" +
                "Method: {Method}\n" +
                "Query: {Query}\n" +
                "Exception Type: {ExceptionType}\n" +
                "Message: {Message}\n" +
                "StackTrace: {StackTrace}",
                requestPath,
                requestMethod,
                requestQuery,
                ex.GetType().FullName,
                ex.Message,
                ex.StackTrace
            );

            // Log inner exception if exists
            if (ex.InnerException != null)
            {
                _logger.LogError(ex.InnerException,
                    "[GlobalException] INNER EXCEPTION.\n" +
                    "Type: {ExceptionType}\n" +
                    "Message: {Message}\n" +
                    "StackTrace: {StackTrace}",
                    ex.InnerException.GetType().FullName,
                    ex.InnerException.Message,
                    ex.InnerException.StackTrace
                );
            }

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
                detail = isDev ? $"{ex.GetType().Name}: {ex.Message}" : null,
                stackTrace = isDev ? ex.StackTrace : null,
                path = requestPath,
                timestamp = DateTime.UtcNow
            };

            await context.Response.WriteAsJsonAsync(response);
        }
    }
}

