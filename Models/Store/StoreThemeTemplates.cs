namespace onlineStore.Models
{
    public static class StoreThemeTemplates
    {
        public const string Default = "D";
        public const string L = "L";
        public const string F = "F";
        public const string P = "P";
        public const string B = "B";
        public const string Blue = "U";
        public const string Brown = "N";
        public const string Purple = "V";
        public const string Gold = "Y";
        public const string DeepGreen = "E";
        public const string Red = "R";


        public static readonly string[] AllowedValues =
            [Default, L, F, P, B, Blue, Brown, Purple, Gold, DeepGreen, Red];

        public static bool IsValid(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var normalizedValue = value.Trim().ToUpperInvariant();
            return AllowedValues.Contains(normalizedValue);
        }

        public static string NormalizeOrDefault(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Default;

            var normalizedValue = value.Trim().ToUpperInvariant();
            return IsValid(normalizedValue) ? normalizedValue : Default;
        }

        public static string NormalizeForResponse(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Default;

            // The database constraint is the source of truth for persisted templates.
            // Preserve its normalized value so an older API allow-list cannot silently
            // downgrade a newly added template to the default theme.
            return value.Trim().ToUpperInvariant();
        }

        public static string NormalizeOrThrow(string? value, string? paramName = null)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Default;

            var normalizedValue = value.Trim().ToUpperInvariant();
            if (IsValid(normalizedValue))
                return normalizedValue;

            throw new ArgumentException(
                $"ThemeTemplate must be one of: {string.Join(", ", AllowedValues)}.",
                paramName ?? nameof(value));
        }
    }
}
