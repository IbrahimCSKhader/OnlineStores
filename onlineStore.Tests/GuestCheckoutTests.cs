using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using onlineStore.Data;
using onlineStore.DTOs.Order;
using onlineStore.Models;
using onlineStore.Models.CartModels;
using onlineStore.Models.Enums;
using onlineStore.Models.Identity;
using onlineStore.Security;
using onlineStore.Services.Order;
using onlineStore.Services.Pricing;

namespace onlineStore.Tests;

public class GuestCheckoutTests
{
    private sealed class Fixture : IAsyncDisposable
    {
        public SqliteConnection Connection { get; } = new("Data Source=:memory:");
        public AppDbContext Db { get; private set; } = null!;
        public Store Store { get; private set; } = null!;
        public Product Product { get; private set; } = null!;
        public OrderService Orders { get; private set; } = null!;
        public async Task Initialize(bool allowGuest = true)
        {
            await Connection.OpenAsync();
            Db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(Connection).Options);
            await Db.Database.EnsureCreatedAsync();
            var owner = new AppUser { FirstName = "Store", LastName = "Owner" };
            Store = new() { Name = "Guest checkout", Slug = "guest-checkout", Owner = owner, RequireCustomerLoginForCheckout = !allowGuest };
            Product = new() { Store = Store, Name = "Product", Slug = "product", Price = 25m, StockQuantity = 10,
                Status = ProductStatus.Active, TrackInventory = true,
                Category = new() { Store = Store, Name = "Category", Slug = "category" },
                Section = new() { Store = Store, Name = "Section", Slug = "section" } };
            Db.Add(Product);
            await Db.SaveChangesAsync();
            Orders = new(Db, NullLogger<OrderService>.Instance, new CartPricingService(Db, NullLogger<CartPricingService>.Instance), new Actor());
        }
        public CreateGuestOrderDto Request() => new() { StoreId = Store.Id, CustomerName = "Guest Name", CustomerEmail = "guest@example.com",
            DeliveryPhone = "0591234567", DeliveryCity = "City", DeliveryAddress = "Street 12", CustomerNotes = "Call first",
            Items = [new() { ProductId = Product.Id, Quantity = 2 }] };
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Connection.DisposeAsync(); }
    }
    private class Actor : ICurrentUserService
    {
        public Guid? UserId => null;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => true;
        public bool IsStoreOwner => false;
    }

    [Fact]
    public async Task GuestOrderUsesServerPricesAndCreatesProfileWithoutLoginCredentials()
    {
        await using var f = new Fixture(); await f.Initialize();
        var result = await f.Orders.CreateGuestOrderAsync(f.Request());
        Assert.True(result.IsGuest);
        Assert.Null(result.StoreCustomerId);
        Assert.Equal("Guest Name", result.CustomerName);
        Assert.Equal("guest@example.com", result.CustomerEmail);
        Assert.Equal("0591234567", result.CustomerPhone);
        Assert.Equal("Street 12", result.DeliveryAddress);
        Assert.Equal("City", result.DeliveryCity);
        Assert.Equal("Call first", result.CustomerNotes);
        Assert.Equal(50m, result.TotalAmount);
        Assert.Equal(0, result.PointsEarned);
        var profile = Assert.Single(await f.Db.StoreCustomers.ToListAsync());
        Assert.True(profile.IsGuestProfile);
        Assert.Empty(profile.PasswordHash);
        Assert.Empty(await f.Db.Carts.ToListAsync());
        Assert.Empty(await f.Db.CartItems.ToListAsync());
        f.Db.ChangeTracker.Clear();
        var detail = await f.Orders.GetStoreOrderByIdAsync(f.Store.Id, result.Id);
        Assert.True(detail!.IsGuest);
        Assert.Equal("City", detail.DeliveryCity);
        Assert.Null(await f.Orders.GetUserOrderByIdAsync(Guid.NewGuid(), result.Id));
        await f.Orders.UpdateOrderStatusAsync(result.Id, new() { Status = OrderStatus.Confirmed });
        Assert.Equal(8, (await f.Db.Products.SingleAsync()).StockQuantity);
    }

    [Fact]
    public async Task GuestCheckoutAcceptsMissingEmail()
    {
        await using var f = new Fixture(); await f.Initialize();
        var request = f.Request();
        request.CustomerEmail = null;
        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        Assert.True(System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            request, new System.ComponentModel.DataAnnotations.ValidationContext(request), validationResults, true));

        var result = await f.Orders.CreateGuestOrderAsync(request);

        Assert.True(result.IsGuest);
        Assert.Null(result.CustomerEmail);
        Assert.Equal("Guest Name", result.CustomerName);
        Assert.Equal("0591234567", result.CustomerPhone);
        Assert.Null((await f.Db.Orders.AsNoTracking().SingleAsync()).CustomerEmail);
        var profile = Assert.Single(await f.Db.StoreCustomers.ToListAsync());
        Assert.True(profile.IsGuestProfile);
        Assert.Empty(profile.PasswordHash);
    }

    [Fact]
    public async Task ReturningGuestsShareOwnerProfileButNeverGainAccessToOtherOrders()
    {
        await using var f = new Fixture(); await f.Initialize();
        var first = await f.Orders.CreateGuestOrderAsync(f.Request());
        var request = f.Request(); request.DeliveryPhone = "+970591234567";
        var second = await f.Orders.CreateGuestOrderAsync(request);
        var profile = Assert.Single(await f.Db.StoreCustomers.ToListAsync());
        var orders = await f.Db.Orders.ToListAsync();
        Assert.All(orders, o => Assert.Equal(profile.Id, o.AccountCustomerId));
        Assert.Null(first.AccountCustomerId); Assert.Null(second.AccountCustomerId);
        Assert.Empty(await f.Db.Orders.Where(o => o.StoreCustomerId == profile.Id).ToListAsync());
        Assert.Null(await f.Orders.GetUserOrderByIdAsync(profile.Id, first.Id));
        Assert.Equal(100m, (await f.Db.CustomerLedgerEntries.ToListAsync()).Sum(x => x.Amount));
    }

    [Fact]
    public async Task LoginRequirementIsIndependentPerStoreAndEnforcedByServer()
    {
        await using var f = new Fixture(); await f.Initialize(false);
        Assert.True((await f.Db.Stores.AsNoTracking().SingleAsync()).RequireCustomerLoginForCheckout);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Orders.CreateGuestOrderAsync(f.Request()));
        Assert.Empty(await f.Db.Orders.ToListAsync());
        f.Store.RequireCustomerLoginForCheckout = false; await f.Db.SaveChangesAsync();
        Assert.True((await f.Orders.CreateGuestOrderAsync(f.Request())).IsGuest);
        Assert.True(new Store().RequireCustomerLoginForCheckout);
    }

    [Theory]
    [InlineData("wrong-product")]
    [InlineData("wrong-variant")]
    [InlineData("no-name")]
    [InlineData("empty-cart")]
    [InlineData("stock")]
    [InlineData("inactive-product")]
    [InlineData("catalog")]
    [InlineData("delivery")]
    public async Task RejectsInvalidGuestRequestsWithoutCreatingOrders(string kind)
    {
        await using var f = new Fixture(); await f.Initialize();
        var dto = f.Request();
        switch (kind)
        {
            case "wrong-product": dto.Items[0].ProductId = Guid.NewGuid(); break;
            case "wrong-variant": dto.Items[0].VariantId = Guid.NewGuid(); break;
            case "no-name": dto.CustomerName = " "; break;
            case "empty-cart": dto.Items.Clear(); break;
            case "stock": dto.Items[0].Quantity = 11; break;
            case "inactive-product": f.Product.IsDeleted = true; break;
            case "catalog": f.Store.IsCatalog = true; break;
            case "delivery": dto.DeliveryLocationId = Guid.NewGuid(); break;
        }
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => f.Orders.CreateGuestOrderAsync(dto));
        f.Db.ChangeTracker.Clear();
        Assert.Empty(await f.Db.Orders.ToListAsync());
    }

    [Fact]
    public async Task RegisteredCheckoutAndCustomerPointsStillWork()
    {
        await using var f = new Fixture(); await f.Initialize(false);
        var customer = new StoreCustomer { Store = f.Store, FirstName = "Registered", LastName = "Customer", Email = "registered@example.com" };
        f.Db.Add(new ShoppingCart { Store = f.Store, StoreCustomer = customer, Items = [new CartItem { Product = f.Product, Quantity = 1 }] });
        await f.Db.SaveChangesAsync();
        var dto = f.Request(); dto.CustomerName = null; dto.CustomerEmail = null;
        var result = await f.Orders.CreateOrderAsync(customer.Id, dto);
        Assert.False(result.IsGuest);
        Assert.Equal(customer.Id, result.StoreCustomerId);
        Assert.Equal("Registered Customer", result.CustomerName);
        Assert.Equal(25m, result.TotalAmount);
    }

    [Fact]
    public async Task GuestDeliveryUsesTheSelectedStoreLocationCost()
    {
        await using var f = new Fixture(); await f.Initialize();
        var location = new StoreDeliveryLocation { Store = f.Store, Name = "Local", Cost = 7.50m };
        f.Db.Add(location); await f.Db.SaveChangesAsync();
        var dto = f.Request(); dto.DeliveryLocationId = location.Id;
        var result = await f.Orders.CreateGuestOrderAsync(dto);
        Assert.Equal(57.50m, result.TotalAmount);
        Assert.Equal(7.50m, result.DeliveryCost);
        Assert.Equal("Local", result.DeliveryLocationName);
    }

    [Fact]
    public async Task GuestBundlePriceAndComponentsAreVerifiedAgainstTheStoreOffer()
    {
        await using var f = new Fixture(); await f.Initialize();
        var offer = new onlineStore.Models.Offers.Offer { Store = f.Store, Name = "Pair", Type = onlineStore.Models.Offers.Enums.OfferType.BundleFixedPrice,
            BundlePrice = 30m, IsActive = true, StartDate = DateTime.UtcNow.AddDays(-1),
            Items = [new() { Product = f.Product, RequiredQuantity = 2 }] };
        f.Db.Add(offer); await f.Db.SaveChangesAsync();
        var dto = f.Request();
        dto.Items[0].BundleOfferId = offer.Id; dto.Items[0].BundleGroupId = Guid.NewGuid();
        dto.Items[0].BundleQuantity = 2; dto.Items[0].BundleItemQuantity = 2; dto.Items[0].Quantity = 4;
        var result = await f.Orders.CreateGuestOrderAsync(dto);
        Assert.Equal(60m, result.TotalAmount);
        Assert.Equal(4, Assert.Single(result.Items).Quantity);
        Assert.Equal("Pair", result.Items[0].BundleName);
        var profile = Assert.Single(await f.Db.StoreCustomers.ToListAsync());
        Assert.True(profile.IsGuestProfile);
        Assert.Empty(profile.PasswordHash);
    }
}
