using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using onlineStore.Controllers;
using onlineStore.DTOs.Store;
using onlineStore.Services.Store;
using onlineStore.Utilities;

namespace onlineStore.Tests;

public class StoreInstallManifestTests
{
    private static readonly StoreDto Store = new() { Slug = "test-shop", Name = "Test store", LogoUrl = "/uploads/logo.png", IsActive = true, CustomDomain = "shop.example" };
    private static JsonElement Manifest(string url, StoreDto? store = null, bool customDomain = false) =>
        JsonSerializer.SerializeToElement(StoreInstallManifest.Create(store ?? Store, new Uri(url), customDomain, new Uri("https://api.example/")));

    [Theory]
    [InlineData("/market/test-shop/products")]
    [InlineData("/market/test-shop/product/123")]
    [InlineData("/market/test-shop/category/456?sort=price&page=2#items")]
    [InlineData("/market/test-shop/offers/789")]
    [InlineData("/market/test-shop/")]
    [InlineData("/market/test-shop")]
    public void PreservesLaunchUrlAndBrandingAcrossFreshRequests(string path)
    {
        var url = "https://mawja.site" + path;
        var first = Manifest(url);
        var nextSession = Manifest(url);
        Assert.Equal(url, first.GetProperty("start_url").GetString());
        Assert.Equal(url, nextSession.GetProperty("id").GetString());
        Assert.Equal(Store.Name, first.GetProperty("name").GetString());
        Assert.Equal("https://api.example/uploads/logo.png", first.GetProperty("icons")[0].GetProperty("src").GetString());
        Assert.StartsWith(first.GetProperty("scope").GetString()!, url);
        Assert.NotEqual("https://mawja.site/", first.GetProperty("scope").GetString());
    }

    [Fact]
    public void SeparatesStoresAndDifferentPageShortcuts()
    {
        var a = Manifest("https://mawja.site/market/test-shop/products");
        var b = Manifest("https://mawja.site/market/other/products", new() { Slug = "other", Name = "Other", LogoUrl = "/other.png" });
        Assert.NotEqual(a.GetProperty("id").GetString(), b.GetProperty("id").GetString());
        Assert.NotEqual(a.GetProperty("id").GetString(), Manifest("https://mawja.site/market/test-shop/about").GetProperty("id").GetString());
        Assert.Equal("https://mawja.site/market/test-shop/", a.GetProperty("scope").GetString());
        Assert.Equal("https://shop.example/products", Manifest("https://shop.example/products", customDomain: true).GetProperty("start_url").GetString());
    }

    [Theory]
    [InlineData("/market/other/products")]
    [InlineData("/market/test-shop-extra/products")]
    [InlineData("/market/test-shop/../other/products")]
    [InlineData("/market/test-shop/login")]
    [InlineData("/market/test-shop/checkout")]
    [InlineData("/market/test-shop/products?token=secret")]
    [InlineData("/market/test-shop/products?preview=owner")]
    public void RejectsOtherStoresAndPrivateOrPreviewPages(string path) =>
        Assert.Throws<ArgumentException>(() => Manifest("https://mawja.site" + path));

    [Fact]
    public async Task ControllerReturnsPublicManifestWithoutCachingAndRejectsWrongDomains()
    {
        var controller = new StoreInstallController(new Stores(), new Origins())
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext() }
        };
        controller.Request.Scheme = "https";
        controller.Request.Host = new HostString("api.example");
        var result = Assert.IsType<JsonResult>(await controller.Manifest("test-shop", "https://mawja.site/market/test-shop/products"));
        Assert.Equal("application/manifest+json", result.ContentType);
        Assert.IsType<BadRequestObjectResult>(await controller.Manifest("test-shop", "https://evil.example/market/test-shop/products"));
        Assert.IsType<BadRequestObjectResult>(await controller.Manifest("test-shop", "https://other.example/products"));
        Assert.IsType<NotFoundResult>(await controller.Manifest("missing", "https://mawja.site/market/missing/products"));
        Assert.IsType<NotFoundResult>(await controller.Manifest("inactive", "https://mawja.site/market/inactive/products"));
        Assert.IsType<JsonResult>(await controller.Manifest("test-shop", "https://shop.example/products"));
        var cache = Assert.Single(typeof(StoreInstallController).GetMethod("Manifest")!.GetCustomAttributes(typeof(ResponseCacheAttribute), true).Cast<ResponseCacheAttribute>());
        Assert.True(cache.NoStore);
    }

    private sealed class Origins : IStorefrontOriginService
    {
        public bool IsAllowedOrigin(string? origin) => origin is "https://mawja.site" or "https://shop.example" or "https://other.example";
        public bool IsPlatformHost(string? host) => host == "mawja.site";
        public bool IsAllowedHost(string? host) => false;
        public string? GetCustomDomainHost(HttpContext context) => null;
    }
    private sealed class Stores : IStoreService
    {
        public Task<StoreDto?> GetStoreBySlugAsync(string slug, string? language = null) => Task.FromResult<StoreDto?>(slug == Store.Slug ? Store : slug == "inactive" ? new() { Slug = slug, IsActive = false } : null);
        public Task<List<StoreDto>> GetAllStoresAsync() => throw new NotSupportedException();
        public Task<StoreDto?> GetOwnedStoreAsync(CancellationToken token = default) => throw new NotSupportedException();
        public Task<StoreDto?> GetStoreByIdAsync(Guid id, string? language = null) => throw new NotSupportedException();
        public Task<StoreDto?> GetStoreByDomainAsync(string host, string? language = null) => throw new NotSupportedException();
        public Task<StoreDto> CreateStoreAsync(CreateStoreDto dto, string userId) => throw new NotSupportedException();
        public Task<StoreDto?> UpdateStoreAsync(Guid id, UpdateStoreDto dto) => throw new NotSupportedException();
        public Task<bool> DeleteStoreAsync(Guid id) => throw new NotSupportedException();
        public Task<int?> IncrementStoreVisitAsync(Guid id) => throw new NotSupportedException();
        public Task<int?> GetStoreVisitCountAsync(Guid id) => throw new NotSupportedException();
    }
}
