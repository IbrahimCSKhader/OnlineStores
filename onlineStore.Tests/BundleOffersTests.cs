using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using onlineStore.Data;
using onlineStore.DTOs.Cart;
using onlineStore.DTOs.Offer;
using onlineStore.DTOs.Order;
using onlineStore.Models;
using onlineStore.Models.CartModels;
using onlineStore.Models.Enums;
using onlineStore.Models.Identity;
using onlineStore.Models.Offers.Enums;
using onlineStore.Models.Subscriptions;
using onlineStore.Security;
using onlineStore.Services.Cart;
using onlineStore.Services.Offer;
using onlineStore.Services.Order;
using onlineStore.Services.Pricing;
using onlineStore.Services.Subscription;

namespace onlineStore.Tests;

public class BundleOffersTests
{
    [Fact]
    public void FixedPriceAllocationPreservesEveryCentIncludingZeroPriceComponents()
    {
        foreach (var regularPrice in new[] { 0m, 13.23m })
        foreach (var quantity in new[] { 1, 3, 999 })
        {
            var rows = Enumerable.Range(0, 7).Select(i => new CartItem { Product = new Product { Price = regularPrice },
                Quantity = (i + 1) * quantity }).ToList();
            BundleCartPricing.Allocate(rows, 10.01m, quantity);
            Assert.Equal(10.01m * quantity, rows.Sum(r => r.BundleLineTotal));
            Assert.All(rows, r => { Assert.True(r.BundleLineTotal >= 0); Assert.Equal(quantity, r.BundleQuantity); });
        }
    }

    [BundleDatabaseFact]
    public async Task BundleLifecyclePersistsSnapshotsAndTracksOnlyTrackedInventory()
    {
        var connection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("BUNDLE_TEST_SQLSERVER"))
        { InitialCatalog = "BundleTests_" + Guid.NewGuid().ToString("N") };
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection.ConnectionString).Options);
        try
        {
            await db.Database.EnsureCreatedAsync();
            // Exercise the actual additive migration against an existing schema.
            var migration = new onlineStore.Migrations.AddBundleCartAndOrderSnapshots();
            var generator = db.GetService<IMigrationsSqlGenerator>();
            foreach (var sql in generator.Generate(migration.DownOperations, db.Model)) await db.Database.ExecuteSqlRawAsync(sql.CommandText);
            foreach (var sql in generator.Generate(migration.UpOperations, db.Model)) await db.Database.ExecuteSqlRawAsync(sql.CommandText);
            var owner = new AppUser { FirstName = "Test", LastName = "Owner" };
            var store = new Store { Name = "Bundles", Slug = "bundles", Owner = owner };
            var category = new Category { Store = store, Name = "Test", Slug = "test" };
            var section = new Section { Store = store, Name = "Test", Slug = "test" };
            var first = new Product { Store = store, Name = "Tracked", Slug = "tracked", Price = 30m, Category = category, Section = section,
                Status = ProductStatus.Active, TrackInventory = true, ThumbnailUrl = "/first.png" };
            var second = new Product { Store = store, Name = "Untracked", Slug = "untracked", Price = 40m, Category = category, Section = section,
                Status = ProductStatus.Active, TrackInventory = false, ThumbnailUrl = "/second.png" };
            first.Variants.Add(new ProductVariant { Name = "Default", IsDefault = true, IsActive = true, StockQuantity = 20 });
            second.Variants.Add(new ProductVariant { Name = "Default", IsDefault = true, IsActive = true, StockQuantity = 0 });
            var customer = new StoreCustomer { Store = store, Email = "bundle@test.example", IsActive = true };
            var plan = new SubscriptionPlan { Name = "Test", Code = "test", CanUseOffers = true };
            db.AddRange(store, first, second, customer, new StoreSubscription { Store = store, SubscriptionPlan = plan, StartDate = DateTime.UtcNow.AddDays(-1) });
            await db.SaveChangesAsync();
            var actor = new Actor(owner.Id);
            var ownership = new StoreOwnershipService(db);
            var subscriptions = new SubscriptionService(db, NullLogger<SubscriptionService>.Instance, actor, ownership);
            var offers = new OfferService(db, NullLogger<OfferService>.Instance, actor, ownership, subscriptions);
            var pricing = new CartPricingService(db, NullLogger<CartPricingService>.Instance);
            var carts = new CartService(db, NullLogger<CartService>.Instance, pricing);
            var orders = new OrderService(db, NullLogger<OrderService>.Instance, pricing, actor);
            var offer = await offers.CreateOfferAsync(new CreateOfferDto { StoreId = store.Id, Name = "Summer package", Type = OfferType.BundleFixedPrice,
                BundlePrice = 75.01m, StartDate = DateTime.UtcNow.AddMinutes(-5), Items = new() {
                    new() { ProductId = first.Id, RequiredQuantity = 2 }, new() { ProductId = second.Id, RequiredQuantity = 1 } } });
            Assert.Equal(100m, offer.RegularPrice);
            Assert.Equal(10, offer.AvailableBundles);
            Assert.All(offer.Items, i => Assert.NotNull(i.ProductImageUrl));
            await Assert.ThrowsAsync<InvalidOperationException>(() => carts.AddBundleAsync(customer.Id,
                new() { StoreId = store.Id, OfferId = offer.Id, Quantity = 11 }));
            Assert.Empty(await db.CartItems.ToListAsync());
            // Clear tracking after a failed transaction, matching a fresh HTTP request scope.
            db.ChangeTracker.Clear();
            var cart = await carts.AddBundleAsync(customer.Id, new() { StoreId = store.Id, OfferId = offer.Id, Quantity = 2 });
            Assert.Equal(150.02m, cart.TotalAmount);
            Assert.Equal(2, cart.Items.Count);
            Assert.All(cart.Items, i => Assert.Equal(offer.Id, i.BundleOfferId));
            Assert.Equal(150.02m, cart.Items.Sum(i => i.TotalPrice));
            cart = await carts.UpdateCartItemAsync(customer.Id, cart.Items[0].Id, new() { Quantity = 3 });
            Assert.Equal(225.03m, cart.TotalAmount);
            Assert.Contains(cart.Items, i => i.ProductId == first.Id && i.Quantity == 6);
            await Assert.ThrowsAsync<InvalidOperationException>(() => carts.UpdateCartItemAsync(customer.Id, cart.Items[0].Id, new() { Quantity = 11 }));
            db.ChangeTracker.Clear();
            cart = await carts.GetCartAsync(customer.Id, store.Id);
            Assert.Equal(225.03m, cart.TotalAmount);
            cart = await carts.RemoveFromCartAsync(customer.Id, cart.Items[0].Id);
            Assert.Empty(cart.Items);
            cart = await carts.AddBundleAsync(customer.Id, new() { StoreId = store.Id, OfferId = offer.Id, Quantity = 2 });
            // Selecting products separately must remain independent of explicit bundles.
            cart = await carts.AddToCartAsync(customer.Id, new() { StoreId = store.Id, ProductId = first.Id, Quantity = 1 });
            Assert.Equal(180.02m, cart.TotalAmount);
            Assert.Equal(3, cart.Items.Count);
            // Wholesale discounts do not alter the fixed bundle price.
            var trackedCustomer = await db.StoreCustomers.SingleAsync(c => c.Id == customer.Id);
            trackedCustomer.DiscountPercentage = 10m;
            await db.SaveChangesAsync();
            var order = await orders.CreateOrderAsync(customer.Id, new() { StoreId = store.Id, DeliveryAddress = "Street", DeliveryCity = "City", DeliveryPhone = "0590000000" });
            Assert.Equal(177.02m, order.TotalAmount);
            Assert.Equal(150.02m, order.Items.Where(i => i.BundleGroupId.HasValue).Sum(i => i.TotalPrice));
            Assert.Equal(2, order.Items.Count(i => i.BundleName == "Summer package"));
            Assert.True((await orders.GetUserOrdersAsync(customer.Id)).Single().ContainsBundles);
            // Existing lifecycle reserves stock when the store confirms an order, once only.
            await orders.UpdateOrderStatusAsync(order.Id, new() { Status = OrderStatus.Confirmed });
            Assert.Equal(15, (await db.ProductVariants.SingleAsync(v => v.ProductId == first.Id)).StockQuantity);
            Assert.Equal(0, (await db.ProductVariants.SingleAsync(v => v.ProductId == second.Id)).StockQuantity);
            await orders.UpdateOrderStatusAsync(order.Id, new() { Status = OrderStatus.Confirmed });
            Assert.Equal(15, (await db.ProductVariants.SingleAsync(v => v.ProductId == first.Id)).StockQuantity);
            await orders.UpdateOrderStatusAsync(order.Id, new() { Status = OrderStatus.Cancelled });
            Assert.Equal(20, (await db.ProductVariants.SingleAsync(v => v.ProductId == first.Id)).StockQuantity);
            await offers.UpdateOfferAsync(offer.Id, new UpdateOfferDto { Name = "Changed", BundlePrice = 60m });
            Assert.All(await db.OrderItems.Where(i => i.BundleGroupId.HasValue).ToListAsync(), i => {
                Assert.Equal("Summer package", i.BundleName); Assert.Equal(75.01m, i.BundlePrice); });
            await carts.AddBundleAsync(customer.Id, new() { StoreId = store.Id, OfferId = offer.Id });
            await offers.UpdateOfferAsync(offer.Id, new UpdateOfferDto { EndDate = DateTime.UtcNow.AddSeconds(-1) });
            await Assert.ThrowsAsync<InvalidOperationException>(() => orders.CreateOrderAsync(customer.Id,
                new() { StoreId = store.Id, DeliveryAddress = "Street", DeliveryCity = "City", DeliveryPhone = "0590000000" }));
            await offers.UpdateOfferAsync(offer.Id, new UpdateOfferDto { EndDate = DateTime.UtcNow.AddDays(1) });
            actor.UserId = Guid.NewGuid();
            await Assert.ThrowsAsync<KeyNotFoundException>(() => offers.UpdateOfferAsync(offer.Id, new UpdateOfferDto { IsActive = true }));
            actor.UserId = owner.Id;
            await offers.DeleteOfferAsync(offer.Id);
            await Assert.ThrowsAsync<InvalidOperationException>(() => orders.CreateOrderAsync(customer.Id,
                new() { StoreId = store.Id, DeliveryAddress = "Street", DeliveryCity = "City", DeliveryPhone = "0590000000" }));
            Assert.Equal(2, (await carts.GetCartAsync(customer.Id, store.Id)).Items.Count);
            Assert.Empty(await offers.GetActiveOffersForStoreAsync(store.Id));
            actor.UserId = Guid.NewGuid();
            Assert.Null(await offers.UpdateOfferAsync(offer.Id, new UpdateOfferDto { IsActive = true }));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
    private sealed class Actor(Guid id) : ICurrentUserService
    {
        public Guid? UserId { get; set; } = id;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public bool IsStoreOwner => true;
    }
}
public sealed class BundleDatabaseFactAttribute : FactAttribute
{
    public BundleDatabaseFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BUNDLE_TEST_SQLSERVER")))
            Skip = "Set BUNDLE_TEST_SQLSERVER to run in an isolated SQL Server database.";
    }
}
