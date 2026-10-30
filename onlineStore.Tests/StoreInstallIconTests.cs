using onlineStore.Utilities;
using SkiaSharp;

namespace onlineStore.Tests;

public class StoreInstallIconTests
{
    [Theory]
    [InlineData(192)]
    [InlineData(512)]
    public void ProducesRealSquarePngIconsEvenWithoutALogo(int size)
    {
        var png = StoreInstallIcon.Render(null, size);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png[..8]);
        using var image = SKBitmap.Decode(png);
        Assert.Equal(size, image.Width);
        Assert.Equal(size, image.Height);
    }

    [Fact]
    public void UsesStoreLogoAndPadsWithoutCropping()
    {
        var root = Path.Combine(Path.GetTempPath(), "mawja-icons-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "uploads"));
        try
        {
            var file = Path.Combine(root, "uploads", "logo.png");
            using (var logo = new SKBitmap(400, 200))
            {
                logo.Erase(new SKColor(240, 30, 40));
                using var image = SKImage.FromBitmap(logo);
                using var bytes = image.Encode(SKEncodedImageFormat.Png, 100);
                using var output = File.Create(file);
                bytes.SaveTo(output);
            }
            Assert.Equal(file, StoreInstallIcon.FindLogo("/uploads/logo.png", root, null));
            using var icon = SKBitmap.Decode(StoreInstallIcon.Render(file, 192));
            Assert.Equal(new SKColor(240, 30, 40), icon.GetPixel(96, 96));
            Assert.Equal(SKColors.White, icon.GetPixel(96, 0));
            Assert.Null(StoreInstallIcon.FindLogo("/appsettings.json", root, null));
            Assert.Null(StoreInstallIcon.FindLogo("/uploads/../appsettings.json", root, null));
            Assert.Null(StoreInstallIcon.FindLogo("/uploads/../../appsettings.json", root, null));
            Assert.Null(StoreInstallIcon.FindLogo("/uploads/%2e%2e/%2e%2e/appsettings.json", root, null));
        }
        finally { Directory.Delete(root, true); }
    }
}
