using System.Text;

namespace onlineStore.Utilities;

public static class CustomerPhone
{
    public static string? Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var digits = new StringBuilder();
        foreach (var c in phone)
        {
            if (char.IsDigit(c)) digits.Append((int)char.GetNumericValue(c));
            else if (!char.IsWhiteSpace(c) && c is not ('+' or '-' or '(' or ')' or '.')) return null;
        }
        var value = digits.ToString();
        if (value.StartsWith("00")) value = value[2..];
        // Palestinian/Israeli local mobile formats share the same national number.
        if ((value.StartsWith("970") || value.StartsWith("972")) && value.Length is 12 or 13)
            value = value[3..].TrimStart('0');
        else if (value.Length == 10 && value.StartsWith("05")) value = value[1..];
        return value.Length is >= 7 and <= 15 ? value : null;
    }

    public static string Name(string? name) => string.Join(" ", (name ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}
