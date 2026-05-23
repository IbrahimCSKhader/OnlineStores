using System.Globalization;

namespace onlineStore.Utilities
{
    public static class StoreDomainNormalizer
    {
        public static string? NormalizeHost(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var candidate = value.Trim();

            if (!candidate.Contains("://", StringComparison.Ordinal))
                candidate = $"https://{candidate}";

            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
                return null;

            var host = uri.IdnHost
                .Trim()
                .Trim('.')
                .ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(host))
                return null;

            if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) &&
                host.Length > 4)
            {
                host = host[4..];
            }

            return IsValidHost(host) ? host : null;
        }

        public static string? NormalizeOrigin(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
                return null;

            if (!IsHttpScheme(uri.Scheme))
                return null;

            var host = NormalizeHost(uri.Host);
            if (string.IsNullOrWhiteSpace(host))
                return null;

            var hasDefaultPort =
                (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) && uri.Port == 443) ||
                (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && uri.Port == 80);

            var authority = hasDefaultPort || uri.IsDefaultPort
                ? host
                : $"{host}:{uri.Port}";

            return $"{uri.Scheme.ToLowerInvariant()}://{authority}";
        }

        private static bool IsHttpScheme(string scheme)
        {
            return string.Equals(scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsValidHost(string host)
        {
            if (host.Length > 255)
                return false;

            return Uri.CheckHostName(host) is UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6;
        }
    }
}
