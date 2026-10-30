using onlineStore.DTOs.Store;

namespace onlineStore.Utilities;

public static class StoreInstallManifest
{
    public static object Create(StoreDto store, Uri launchUrl, bool customDomain, Uri assetBase)
    {
        var origin = launchUrl.GetLeftPart(UriPartial.Authority);
        var scopePath = customDomain ? "/" : $"/market/{Uri.EscapeDataString(store.Slug)}/";
        var storeRoot = scopePath.TrimEnd('/');
        var path = launchUrl.AbsolutePath;
        if (path != storeRoot && !path.StartsWith(scopePath, StringComparison.Ordinal))
            throw new ArgumentException("Launch URL must belong to this store.");

        // Do not persist authentication, checkout or owner-preview URLs in an installed app.
        var relativePath = path == storeRoot ? "" : path[scopePath.Length..].TrimEnd('/');
        if (relativePath != "" && relativePath != "products" && relativePath != "about" &&
            relativePath != "contact" && relativePath != "offers" &&
            !System.Text.RegularExpressions.Regex.IsMatch(relativePath, @"^(product|category|offers)/[^/]+$"))
            throw new ArgumentException("Only public store pages can be installed.");
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(launchUrl.Query);
        if (query.Keys.Any(key => !new[] { "search", "q", "page", "category", "sort", "language", "lang" }.Contains(key, StringComparer.OrdinalIgnoreCase)))
            throw new ArgumentException("Unsupported launch query.");

        // Existing store-root routes omit the trailing slash; their scope must include
        // that exact URL. Nested pages use the narrower directory scope.
        var scope = origin + (path == storeRoot ? storeRoot : scopePath);
        var icon = Uri.TryCreate(assetBase, store.LogoUrl ?? "", out var logo) &&
            (logo.Scheme == "https" || logo.Scheme == "http") && !string.IsNullOrWhiteSpace(store.LogoUrl)
            ? logo.AbsoluteUri : origin + "/mawja%20logo.jpeg";
        var startUrl = launchUrl.AbsoluteUri;
        return new
        {
            name = store.Name,
            short_name = store.Name,
            // Stable for this launch URL, distinct from both Mawja and other page shortcuts.
            id = startUrl,
            start_url = startUrl,
            scope,
            display = "standalone",
            icons = new[] { new { src = icon, purpose = "any" } }
        };
    }
}
