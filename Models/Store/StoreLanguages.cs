namespace onlineStore.Models;

public static class StoreLanguages
{
    public const string Arabic = "A";
    public const string English = "E";
    public const string Hebrew = "I";

    public static readonly string[] Supported = [Arabic, English, Hebrew];

    public static string NormalizeOrDefault(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Arabic;

        return value.Trim().ToUpperInvariant() switch
        {
            "A" or "AR" or "ARA" or "ARABIC" => Arabic,
            "E" or "EN" or "ENG" or "ENGLISH" => English,
            "I" or "HE" or "HEB" or "HEBREW" or "IVRIT" => Hebrew,
            _ => Arabic
        };
    }

    public static bool IsBaseLanguage(string? value) =>
        NormalizeOrDefault(value) == Arabic;
}
