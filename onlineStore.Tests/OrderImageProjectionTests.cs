using System.Reflection;
using onlineStore.DTOs.Order;
using onlineStore.Models;
using onlineStore.Models.Orders;
using onlineStore.Services.Order;

namespace onlineStore.Tests;

public class OrderImageProjectionTests
{
    private static OrderDto Map(Product product, ProductVariant? variant = null)
    {
        var order = new Order { Items = new List<OrderItem> {
            new() { ProductId = product.Id, ProductName = "Purchased product", Product = product, Variant = variant, Quantity = 1 }
        } };
        var method = typeof(OrderService).GetMethod("MapOrderToDto", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (OrderDto)method.Invoke(null, [order])!;
    }

    [Fact]
    public void UsesProductThumbnailWhenThereIsNoVariant()
    {
        var item = Assert.Single(Map(new Product { ThumbnailUrl = "/product.jpg" }).Items);
        Assert.Equal("/product.jpg", item.ProductImageUrl);
        Assert.Equal("/product.jpg", item.EffectiveVariantImageUrl);
        Assert.Equal("Purchased product", item.ProductName);
    }

    [Fact]
    public void PrefersPurchasedVariantImage()
    {
        var item = Assert.Single(Map(new Product { ThumbnailUrl = "/product.jpg" }, new ProductVariant { ImageUrl = "/variant.jpg" }).Items);
        Assert.Equal("/variant.jpg", item.EffectiveVariantImageUrl);
    }

    [Fact]
    public void FallsBackToProductGalleryAndIgnoresDeletedImages()
    {
        var item = Assert.Single(Map(new Product { Images = new List<ProductImage> {
            new() { Url = "/deleted.jpg", IsDeleted = true },
            new() { Url = "/gallery.jpg", DisplayOrder = 1 }
        } }).Items);
        Assert.Equal("/gallery.jpg", item.ProductImageUrl);
    }
}
