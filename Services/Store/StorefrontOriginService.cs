using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using onlineStore.Data;
using onlineStore.Utilities;

namespace onlineStore.Services.Store
{
    public class StorefrontOriginService : IStorefrontOriginService
    {
        private const string AllowedCustomDomainHostsCacheKey = "storefront-origin-service:allowed-custom-domain-hosts";
        private static readonly TimeSpan AllowedCustomDomainHostsCacheDuration = TimeSpan.FromMinutes(1);

        private readonly IConfiguration _configuration;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<StorefrontOriginService> _logger;
        private readonly HashSet<string> _configuredOrigins;
        private readonly HashSet<string> _configuredHosts;

        public StorefrontOriginService(
            IConfiguration configuration,
            IServiceScopeFactory scopeFactory,
            IMemoryCache memoryCache,
            ILogger<StorefrontOriginService> logger)
        {
            _configuration = configuration;
            _scopeFactory = scopeFactory;
            _memoryCache = memoryCache;
            _logger = logger;
            _configuredOrigins = BuildConfiguredOrigins(configuration);
            _configuredHosts = BuildConfiguredHosts(_configuredOrigins, configuration);
        }

        public bool IsAllowedOrigin(string? origin)
        {
            var normalizedOrigin = StoreDomainNormalizer.NormalizeOrigin(origin);
            if (string.IsNullOrWhiteSpace(normalizedOrigin))
                return false;

            if (_configuredOrigins.Contains(normalizedOrigin))
                return true;

            if (!Uri.TryCreate(normalizedOrigin, UriKind.Absolute, out var uri))
                return false;

            if (uri.IsLoopback)
                return true;

            return IsAllowedHost(uri.Host);
        }

        public bool IsAllowedHost(string? host)
        {
            var normalizedHost = StoreDomainNormalizer.NormalizeHost(host);
            if (string.IsNullOrWhiteSpace(normalizedHost))
                return false;

            if (_configuredHosts.Contains(normalizedHost))
                return true;

            var allowedCustomDomainHosts = GetAllowedCustomDomainHosts();
            return allowedCustomDomainHosts.Contains(normalizedHost);
        }

        public bool IsPlatformHost(string? host)
        {
            var normalizedHost = StoreDomainNormalizer.NormalizeHost(host);
            if (string.IsNullOrWhiteSpace(normalizedHost))
                return true;

            if (_configuredHosts.Contains(normalizedHost))
                return true;

            return IsLoopbackHost(normalizedHost);
        }

        public string? GetCustomDomainHost(HttpContext httpContext)
        {
            var normalizedHost = StoreDomainNormalizer.NormalizeHost(httpContext?.Request.Host.Host);
            if (string.IsNullOrWhiteSpace(normalizedHost))
                return null;

            if (IsPlatformHost(normalizedHost))
                return null;

            return GetAllowedCustomDomainHosts().Contains(normalizedHost)
                ? normalizedHost
                : null;
        }

        private HashSet<string> GetAllowedCustomDomainHosts()
        {
            return _memoryCache.GetOrCreate(
                AllowedCustomDomainHostsCacheKey,
                entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = AllowedCustomDomainHostsCacheDuration;
                    return LoadAllowedCustomDomainHosts();
                }) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        private HashSet<string> LoadAllowedCustomDomainHosts()
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            try
            {
                var customDomains = dbContext.Stores
                    .AsNoTracking()
                    .Select(store => store.CustomDomain)
                    .Where(customDomain => !string.IsNullOrWhiteSpace(customDomain))
                    .ToList();

                return new HashSet<string>(
                    customDomains
                        .Select(StoreDomainNormalizer.NormalizeHost)
                        .Where(host => !string.IsNullOrWhiteSpace(host))
                        .Cast<string>(),
                    StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "StorefrontOriginService failed to refresh custom domain hosts cache. Falling back to configured origins only.");

                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static HashSet<string> BuildConfiguredOrigins(IConfiguration configuration)
        {
            var origins = configuration
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? Array.Empty<string>();

            return new HashSet<string>(
                origins
                    .Select(StoreDomainNormalizer.NormalizeOrigin)
                    .Where(origin => !string.IsNullOrWhiteSpace(origin))
                    .Cast<string>(),
                StringComparer.OrdinalIgnoreCase);
        }

        private static HashSet<string> BuildConfiguredHosts(
            IEnumerable<string> configuredOrigins,
            IConfiguration configuration)
        {
            var hosts = configuredOrigins
                .Select(origin => Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                    ? StoreDomainNormalizer.NormalizeHost(uri.Host)
                    : null)
                .Where(host => !string.IsNullOrWhiteSpace(host))
                .Cast<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var frontendBaseUrl = configuration["FrontendSettings:BaseUrl"];
            var frontendHost = StoreDomainNormalizer.NormalizeHost(frontendBaseUrl);

            if (!string.IsNullOrWhiteSpace(frontendHost))
                hosts.Add(frontendHost);

            return hosts;
        }

        private static bool IsLoopbackHost(string host)
        {
            return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase);
        }
    }
}
