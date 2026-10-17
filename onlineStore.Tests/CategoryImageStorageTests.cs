using Microsoft.AspNetCore.Http;
using onlineStore.Utilities;

namespace onlineStore.Tests;

public sealed class CategoryImageStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "category-images-" + Guid.NewGuid());
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9ZQmcAAAAASUVORK5CYII=");
    private static IFormFile File(byte[] bytes, string name) => new FormFile(new MemoryStream(bytes), 0, bytes.Length, "ImageFile", name);

    [Fact]
    public async Task StoresImageUnderItsStoreAndPreservesBytes()
    {
        var storeId = Guid.NewGuid();
        var result = await CategoryImageStorage.SaveAsync(File(Png, "../../room.PNG"), _root, storeId);
        Assert.StartsWith($"/wwwroot/uploads/stores/{storeId}/categories/", result.Url);
        Assert.Equal(Png, await System.IO.File.ReadAllBytesAsync(result.FilePath));
        Assert.Equal(".png", Path.GetExtension(result.FilePath));
        var replacement = await CategoryImageStorage.SaveAsync(File(Png, "room.png"), _root, storeId);
        Assert.NotEqual(result.Url, replacement.Url);
        Assert.True(System.IO.File.Exists(result.FilePath));
    }

    [Theory]
    [InlineData("image.svg")]
    [InlineData("image.exe")]
    [InlineData("image.jpg")]
    public async Task RejectsUnsupportedOrMismatchedFilesWithoutWriting(string name)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => CategoryImageStorage.SaveAsync(File(Png, name), _root, Guid.NewGuid()));
        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public async Task RejectsEmptyAndOversizedFiles()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => CategoryImageStorage.SaveAsync(File([], "empty.png"), _root, Guid.NewGuid()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => CategoryImageStorage.SaveAsync(File(new byte[CategoryImageStorage.MaxFileSize + 1], "large.png"), _root, Guid.NewGuid()));
        Assert.False(Directory.Exists(_root));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
