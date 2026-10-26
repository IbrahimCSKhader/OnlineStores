using System.Text.Json;
using System.Text.RegularExpressions;
using onlineStore.DTOs.Store;

namespace onlineStore.Utilities;

public static class StoreAppearancePolicy
{
    public static readonly string[] Colors = ["primary", "secondary", "background", "surface", "textPrimary", "textSecondary", "border", "headerBackground", "headerText", "buttonBackground", "buttonText", "productCardBackground", "productTitleColor", "priceColor", "storeTitleColor", "storeDescriptionColor"];
    public static readonly string[] Fonts = ["fontFamily", "headingFontFamily", "storeTitleFontFamily", "storeDescriptionFontFamily", "productTitleFontFamily", "priceFontFamily", "buttonFontFamily"];
    public static readonly string[] Sizes = ["fontSize", "storeTitleFontSize", "storeDescriptionFontSize", "productTitleFontSize", "priceFontSize", "buttonFontSize"];
    public static readonly string[] FontFamilies = ["Arial", "Tahoma", "Verdana", "Georgia", "Roboto", "sans-serif", "serif"];

    public static StoreAppearanceDto Read(string? json) => string.IsNullOrWhiteSpace(json)
        ? new() : JsonSerializer.Deserialize<StoreAppearanceDto>(json)!;

    public static string Serialize(StoreAppearanceDto dto) => JsonSerializer.Serialize(dto);

    public static StoreAppearanceDto Validate(StoreAppearanceDto dto)
    {
        return new() { Light = ValidateMode(dto.Light), Dark = ValidateMode(dto.Dark) };
    }

    private static Dictionary<string, string> ValidateMode(Dictionary<string, string>? values)
    {
        if (values == null || values.Count > Colors.Length + Fonts.Length + Sizes.Length)
            throw new ArgumentException("Invalid appearance settings.");
        var result = new Dictionary<string, string>();
        foreach (var (key, value) in values)
        {
            if (value == null) throw new ArgumentException($"Invalid appearance value: {key}.");
            var normalized = value.Trim();
            var valid = Colors.Contains(key)
                ? Regex.IsMatch(normalized, "^#[0-9a-fA-F]{6}$")
                : Fonts.Contains(key)
                    ? FontFamilies.Contains(normalized)
                    : Sizes.Contains(key) && Regex.IsMatch(normalized, "^[0-9]{2}px$") &&
                        int.Parse(normalized[..^2]) is >= 10 and <= 96;
            if (!valid) throw new ArgumentException($"Invalid appearance value: {key}.");
            result[key] = normalized;
        }
        return result;
    }
}
