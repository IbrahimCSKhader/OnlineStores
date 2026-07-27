namespace onlineStore.Models
{
    public static class StoreThemeTemplates
    {
        public const string Default = "D";
        public const string L = "L";
        public const string F = "F";
        public const string P = "P";
        public const string B = "B";


        public static readonly string[] AllowedValues = [Default, L, F, P, B];

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
