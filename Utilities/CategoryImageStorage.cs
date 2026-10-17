namespace onlineStore.Utilities;

public static class CategoryImageStorage
{
    public const long MaxFileSize = 5 * 1024 * 1024;
    public sealed record SavedImage(string Url, string FilePath);

    public static async Task<SavedImage> SaveAsync(
        IFormFile file, string webRootPath, Guid storeId,
        CancellationToken cancellationToken = default)
    {
        if (file.Length <= 0 || file.Length > MaxFileSize)
            throw new InvalidOperationException("حجم الصورة يجب أن يكون أكبر من صفر ولا يتجاوز 5 ميجابايت.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        await using var input = file.OpenReadStream();
        var header = new byte[12];
        var bytesRead = await input.ReadAtLeastAsync(header, header.Length, false, cancellationToken);
        var valid = extension switch
        {
            ".png" => bytesRead >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            ".jpg" or ".jpeg" => bytesRead >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255,
            ".webp" => bytesRead >= 12 && header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };
        if (!valid)
            throw new InvalidOperationException("اختر صورة صالحة بصيغة JPG أو PNG أو WebP.");

        var directory = Path.Combine(webRootPath, "uploads", "stores", storeId.ToString(), "categories");
        Directory.CreateDirectory(directory);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var path = Path.Combine(directory, fileName);
        try
        {
            await using var output = new FileStream(path, FileMode.CreateNew);
            await output.WriteAsync(header.AsMemory(0, bytesRead), cancellationToken);
            await input.CopyToAsync(output, cancellationToken);
        }
        catch
        {
            File.Delete(path);
            throw;
        }
        return new SavedImage($"/wwwroot/uploads/stores/{storeId}/categories/{fileName}", path);
    }
}
