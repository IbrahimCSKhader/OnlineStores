using System.Text.RegularExpressions;

namespace onlineStore.Models
{
    public static class StoreContactPlatforms
    {
        public const string Instagram = "Instagram";
        public const string TikTok = "TikTok";
        public const string Facebook = "Facebook";
        public const string Snapchat = "Snapchat";
        public const string WhatsApp = "WhatsApp";

        private static readonly string[] SupportedPlatforms =
        {
            Instagram,
            TikTok,
            Facebook,
            Snapchat,
            WhatsApp
        };

        public static IReadOnlyList<string> All => SupportedPlatforms;

        public static bool IsSupported(string? platform)
        {
            if (string.IsNullOrWhiteSpace(platform))
                return false;

            return SupportedPlatforms.Any(x =>
                string.Equals(x, platform.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public static string NormalizePlatform(string platform)
        {
            var normalizedPlatform = SupportedPlatforms.FirstOrDefault(x =>
                string.Equals(x, platform.Trim(), StringComparison.OrdinalIgnoreCase));

            if (normalizedPlatform == null)
                throw new Exception(
                    "Unsupported contact platform. Allowed platforms are Instagram, TikTok, Facebook, Snapchat, WhatsApp.");

            return normalizedPlatform;
        }

        public static string NormalizeUsername(string platform, string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new Exception("Contact username is required.");

            var normalizedPlatform = NormalizePlatform(platform);
            var normalizedUsername = username.Trim();

            if (normalizedPlatform == WhatsApp)
            {
                normalizedUsername = Regex.Replace(normalizedUsername, "\\D", string.Empty);

                if (string.IsNullOrWhiteSpace(normalizedUsername))
                    throw new Exception("WhatsApp account must contain digits only.");

                return normalizedUsername;
            }

            return normalizedUsername.TrimStart('@');
        }

        public static string BuildUrl(string platform, string username)
        {
            var normalizedPlatform = NormalizePlatform(platform);
            var normalizedUsername = NormalizeUsername(normalizedPlatform, username);

            return normalizedPlatform switch
            {
                Instagram => $"https://www.instagram.com/{normalizedUsername}",
                TikTok => $"https://www.tiktok.com/@{normalizedUsername}",
                Facebook => $"https://www.facebook.com/{normalizedUsername}",
                Snapchat => $"https://www.snapchat.com/add/{normalizedUsername}",
                WhatsApp => $"https://wa.me/{normalizedUsername}",
                _ => throw new Exception("Unsupported contact platform.")
            };
        }
    }
}
