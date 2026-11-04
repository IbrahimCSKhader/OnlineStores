using System.Text.Json;

namespace onlineStore.Tests;

public class RealCosmeticsCatalogTests
{
    [Fact]
    public void EveryCatalogProductHasAUniqueSlugAndDocumentedRasterImages()
    {
        using var catalog = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "CatalogAssets", "real-products.json")));
        var products = catalog.RootElement.GetProperty("products").EnumerateArray().ToList();
        Assert.Equal(32, products.Count);
        Assert.Equal(32, products.Select(p => p.GetProperty("slug").GetString()).Distinct().Count());
        foreach (var product in products)
        {
            Assert.StartsWith("alsujood-cosmetics-", product.GetProperty("slug").GetString());
            Assert.False(string.IsNullOrWhiteSpace(product.GetProperty("name").GetString()));
            Assert.Equal("https", new Uri(product.GetProperty("source").GetString()!).Scheme);
            var images = product.GetProperty("images").EnumerateArray().Select(x => x.GetString()!).ToList();
            Assert.NotEmpty(images);
            Assert.All(images, image => {
                Assert.Equal("https", new Uri(image).Scheme);
                Assert.DoesNotContain(".svg", image);
                Assert.DoesNotContain("logo", image);
                Assert.DoesNotContain("seed-images", image);
            });
        }
    }
}
