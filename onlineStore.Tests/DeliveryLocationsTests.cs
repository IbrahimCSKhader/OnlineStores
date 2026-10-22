using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using onlineStore.Data;
using onlineStore.DTOs.Order;
using onlineStore.DTOs.Store;
using onlineStore.Migrations;
using onlineStore.Models;
using onlineStore.Models.CartModels;
using onlineStore.Models.Discounts;
using onlineStore.Models.Enums;
using onlineStore.Models.Identity;
using onlineStore.Security;
using onlineStore.Services.Order;
using onlineStore.Services.Pricing;
using onlineStore.Services.Store;

namespace onlineStore.Tests;

public class DeliveryLocationsTests
{
    [DeliveryDatabaseFact]
    public async Task DeliverySelectionPricingHistoryAndStoreOwnership()
    {
        // Use an isolated database, never the application's configured database.
        var connection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable("DELIVERY_TEST_SQLSERVER"))
        { InitialCatalog = "DeliveryTests_" + Guid.NewGuid().ToString("N") };
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connection.ConnectionString).Options);
        try
        {
            await db.Database.EnsureCreatedAsync();
            var owner = new AppUser { Id = Guid.NewGuid(), FirstName = "Test", LastName = "Owner" };
            var role = new AppRole { Id = Guid.NewGuid(), Name = "StoreOwner", NormalizedName = "STOREOWNER" };
            var store = new Store { Name = "Test", Slug = "delivery-test", OwnerId = owner.Id };
            var other = new Store { Name = "Other", Slug = "other-store", OwnerId = Guid.NewGuid() };
            var customer = new StoreCustomer { Store = store, Email = "test@example.com" };
            var product = new Product { Store = store, Name = "Test", Slug = "product", Price = 100,
                Category = new Category { Store = store, Name = "Test", Slug = "category" },
                Section = new Section { Store = store, Name = "Test", Slug = "section" }, TrackInventory = false };
            db.AddRange(owner, role, store, customer, product);
            db.UserRoles.Add(new() { UserId = owner.Id, RoleId = role.Id });
            await db.SaveChangesAsync();
            var catalogMigration = new AddStoreCatalogMode();
            var catalogSql = db.GetService<IMigrationsSqlGenerator>();
            foreach (var sql in catalogSql.Generate(catalogMigration.DownOperations, db.Model))
                await db.Database.ExecuteSqlRawAsync(sql.CommandText);
            foreach (var sql in catalogSql.Generate(catalogMigration.UpOperations, db.Model))
                await db.Database.ExecuteSqlRawAsync(sql.CommandText);
            Assert.False(await db.Stores.AsNoTracking().Where(x => x.Id == store.Id).Select(x => x.IsCatalog).SingleAsync());
            Assert.False(new CreateStoreDto().IsCatalog);
            var actor = new TestActor(owner.Id);
            var orders = new OrderService(db, NullLogger<OrderService>.Instance,
                new CartPricingService(db, NullLogger<CartPricingService>.Instance), actor);
            var stores = new StoreService(db, NullLogger<StoreService>.Instance, actor,
                new StoreAuthorizationService(db), null!, null!, null!, null!);
            var request = new CreateOrderDto { StoreId = store.Id, DeliveryAddress = "Street",
                DeliveryCity = "City", DeliveryPhone = "0590000000" };
            async Task RefillCart()
            {
                db.Carts.Add(new ShoppingCart { StoreId = store.Id, StoreCustomerId = customer.Id,
                    Items = new List<CartItem> { new() { ProductId = product.Id, Quantity = 1, UnitPrice = 100 } } });
                await db.SaveChangesAsync();
            }
            await RefillCart();
            var legacy = await orders.CreateOrderAsync(customer.Id, request);
            Assert.Equal("افتراضي", legacy.DeliveryLocationName);
            Assert.Null(legacy.DeliveryLocationId);
            Assert.Equal(100m, legacy.TotalAmount);
            Assert.Equal(0m, legacy.DeliveryCost);

            // Exercise the actual migration on an existing order, including the backfill.
            var migration = new AddStoreDeliveryLocations();
            var generator = db.GetService<IMigrationsSqlGenerator>();
            foreach (var sql in generator.Generate(migration.DownOperations, db.Model))
                await db.Database.ExecuteSqlRawAsync(sql.CommandText);
            foreach (var sql in generator.Generate(migration.UpOperations, db.Model))
                await db.Database.ExecuteSqlRawAsync(sql.CommandText);
            db.ChangeTracker.Clear();
            var restored = await db.Orders.SingleAsync();
            Assert.Equal("افتراضي", restored.DeliveryLocationName);
            Assert.Equal(0m, restored.DeliveryCost);
            Assert.Equal(100m, restored.TotalAmount);

            var updated = await stores.UpdateStoreAsync(store.Id, new UpdateStoreDto {
                DeliveryLocations = new() { new() { Name = "Hebron", Cost = 15.50m } } });
            var location = Assert.Single(updated!.DeliveryLocations);
            await RefillCart();
            await Assert.ThrowsAsync<ArgumentException>(() => orders.CreateOrderAsync(customer.Id, request));
            request.DeliveryLocationId = Guid.NewGuid();
            await Assert.ThrowsAsync<ArgumentException>(() => orders.CreateOrderAsync(customer.Id, request));
            Assert.Equal(1, await db.CartItems.CountAsync());
            // A foreign store location must also be rejected.
            other.OwnerId = owner.Id;
            db.Stores.Add(other);
            var foreign = new StoreDeliveryLocation { Store = other, Name = "Other", Cost = 1 };
            db.StoreDeliveryLocations.Add(foreign);
            db.Coupons.Add(new Coupon { StoreId = store.Id, Code = "SAVE", DiscountType = DiscountType.Percentage,
                DiscountValue = 20, PerUserLimit = null });
            await db.SaveChangesAsync();
            request.DeliveryLocationId = foreign.Id;
            await Assert.ThrowsAsync<ArgumentException>(() => orders.CreateOrderAsync(customer.Id, request));
            request.DeliveryLocationId = location.Id;
            request.CouponCode = "SAVE";
            var paid = await orders.CreateOrderAsync(customer.Id, request);
            Assert.Equal(20m, paid.DiscountAmount);
            Assert.Equal(95.50m, paid.TotalAmount);
            Assert.Equal(15.50m, paid.DeliveryCost);
            Assert.Equal(location.Id, paid.DeliveryLocationId);
            await stores.UpdateStoreAsync(store.Id, new UpdateStoreDto {
                DeliveryLocations = new() { new() { Id = location.Id, Name = "Changed", Cost = 30 } } });
            Assert.Equal(15.50m, (await orders.GetUserOrdersAsync(customer.Id)).Single(x => x.Id == paid.Id).DeliveryCost);
            await stores.UpdateStoreAsync(store.Id, new UpdateStoreDto { DeliveryLocations = new() });
            await RefillCart();
            await Assert.ThrowsAsync<ArgumentException>(() => orders.CreateOrderAsync(customer.Id, request));
            request.DeliveryLocationId = null;
            request.CouponCode = null;
            Assert.Equal(100m, (await orders.CreateOrderAsync(customer.Id, request)).TotalAmount);
            db.ChangeTracker.Clear();
            var historical = await db.Orders.SingleAsync(x => x.Id == paid.Id);
            Assert.Equal("Hebron", historical.DeliveryLocationName);
            Assert.Equal(15.50m, historical.DeliveryCost);
            await Assert.ThrowsAsync<ArgumentException>(() => stores.UpdateStoreAsync(store.Id,
                new UpdateStoreDto { DeliveryLocations = new() { new() { Name = "Invalid", Cost = -1 } } }));
            var catalog = await db.Stores.SingleAsync(x => x.Id == store.Id);
            catalog.IsCatalog = true;
            await db.SaveChangesAsync();
            Assert.True((await stores.GetAllStoresAsync()).Single(x => x.Id == store.Id).IsCatalog);
            await RefillCart();
            var cartItem = await db.CartItems.SingleAsync();
            var carts = new onlineStore.Services.Cart.CartService(db,
                NullLogger<onlineStore.Services.Cart.CartService>.Instance,
                new CartPricingService(db, NullLogger<CartPricingService>.Instance));
            await Assert.ThrowsAsync<ArgumentException>(() => carts.AddToCartAsync(customer.Id,
                new onlineStore.DTOs.Cart.AddToCartDto { StoreId = store.Id, ProductId = product.Id, Quantity = 1 }));
            await Assert.ThrowsAsync<ArgumentException>(() => carts.UpdateCartItemAsync(customer.Id, cartItem.Id,
                new onlineStore.DTOs.Cart.UpdateCartItemDto { Quantity = 2 }));
            await Assert.ThrowsAsync<ArgumentException>(() => orders.CreateOrderAsync(customer.Id, request));
            Assert.Equal(1, (await db.CartItems.SingleAsync()).Quantity);
            catalog.IsCatalog = false;
            await db.SaveChangesAsync();
            Assert.Equal(100m, (await orders.CreateOrderAsync(customer.Id, request)).TotalAmount);
            actor.UserId = Guid.NewGuid();
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => stores.UpdateStoreAsync(store.Id,
                new UpdateStoreDto { DeliveryLocations = new() }));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private sealed class TestActor(Guid id) : ICurrentUserService
    {
        public Guid? UserId { get; set; } = id;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public bool IsStoreOwner => true;
    }
}

public sealed class DeliveryDatabaseFactAttribute : FactAttribute
{
    public DeliveryDatabaseFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DELIVERY_TEST_SQLSERVER")))
            Skip = "Set DELIVERY_TEST_SQLSERVER to run against an isolated SQL Server test database.";
    }
}
