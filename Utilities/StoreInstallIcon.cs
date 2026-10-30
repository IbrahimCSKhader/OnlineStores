using SkiaSharp;

namespace onlineStore.Utilities;

public static class StoreInstallIcon
{
    public static string? FindLogo(string? logoUrl, string contentRoot, string? webRoot)
    {
        if (string.IsNullOrWhiteSpace(logoUrl)) return null;
        var path = Uri.TryCreate(logoUrl, UriKind.Absolute, out var uri) ? uri.AbsolutePath : logoUrl.Split('?')[0];
        path = Uri.UnescapeDataString(path).Replace('\\', '/').TrimStart('/');
        // Only public image directories can be used; never read configuration or arbitrary files.
        var publicDirectories = new[] { "uploads/", "wwwroot/", "store-branding/", "seed-images/" };
        if (!publicDirectories.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            return null;
        foreach (var root in new[] { contentRoot, webRoot }.Where(root => !string.IsNullOrEmpty(root)))
        {
            var boundary = Path.GetFullPath(root!) + Path.DirectorySeparatorChar;
            var candidate = Path.GetFullPath(Path.Combine(root!, path));
            var relative = Path.GetRelativePath(root!, candidate).Replace('\\', '/');
            if (candidate.StartsWith(boundary, StringComparison.OrdinalIgnoreCase) &&
                publicDirectories.Any(prefix => relative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) && File.Exists(candidate)) return candidate;
        }
        return null;
    }

    public static byte[] Render(string? sourcePath, int size)
    {
        if (size is not (192 or 512)) throw new ArgumentOutOfRangeException(nameof(size));
        SKBitmap? source = null;
        if (sourcePath is not null)
        {
            try { source = SKBitmap.Decode(sourcePath); }
            catch (IOException) { }
        }
        using var bitmap = source;
        using var surface = SKSurface.Create(new SKImageInfo(size, size));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        if (bitmap is null)
        {
            // A simple wave fallback for missing/unsupported logos; no font or network dependency.
            using var paint = new SKPaint { Color = new SKColor(16, 116, 190) };
            using var wave = new SKPath();
            wave.MoveTo(0, size);
            for (var x = 0; x <= size; x++)
            {
                var edge = (float)(size * (0.55 + 0.12 * Math.Sin(x * Math.PI * 2 / size)));
                wave.LineTo(x, edge);
            }
            wave.LineTo(size, size);
            wave.Close();
            canvas.DrawPath(wave, paint);
        }
        else
        {
            var ratio = Math.Min((float)size / bitmap.Width, (float)size / bitmap.Height);
            var width = bitmap.Width * ratio;
            var height = bitmap.Height * ratio;
            canvas.DrawBitmap(bitmap, SKRect.Create((size - width) / 2, (size - height) / 2, width, height));
        }
        using var image = surface.Snapshot();
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }
}
