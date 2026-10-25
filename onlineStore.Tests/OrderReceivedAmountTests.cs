using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using onlineStore.Data;
using onlineStore.DTOs.Order;
using onlineStore.Models;
using onlineStore.Models.Identity;
using onlineStore.Models.Orders;
using onlineStore.Security;
using onlineStore.Services.Order;

namespace onlineStore.Tests;

public class OrderReceivedAmountTests
{
    [Fact]
    public async Task OwnerCanRecordCorrectAndClearReceiptWhileOtherOwnersCannot()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var owner = new AppUser { FirstName = "Test", LastName = "Owner" };
        var store = new Store { Name = "Test", Slug = "receipt-test", Owner = owner };
        var customer = new StoreCustomer { Store = store, Email = "receipt@example.com" };
        var order = new Order { Store = store, StoreCustomer = customer, OrderNumber = "RECEIPT-1", TotalAmount = 115.50m, SubTotal = 100m, DeliveryCost = 15.50m };
        db.Add(order);
        await db.SaveChangesAsync();
        var actor = new Actor { UserId = owner.Id };
        var service = new OrderService(db, NullLogger<OrderService>.Instance, null!, actor);
        foreach (var amount in new decimal?[] { 0m, 50.25m, 115.50m, 130.75m, null })
        {
            var result = await service.UpdateReceivedAmountAsync(order.Id, new() { ReceivedAmount = amount });
            Assert.Equal(amount, result!.ReceivedAmount);
            Assert.Equal(amount.HasValue ? Math.Max(115.50m - amount.Value, 0m) : (decimal?)null, result.RemainingAmount);
            Assert.Equal(amount.HasValue ? Math.Max(amount.Value - 115.50m, 0m) : (decimal?)null, result.OverpaidAmount);
            db.ChangeTracker.Clear();
            Assert.Equal(amount, (await db.Orders.SingleAsync()).ReceivedAmount);
            Assert.Equal(amount, (await service.GetStoreOrderByIdAsync(store.Id, order.Id))!.ReceivedAmount);
            Assert.Null((await service.GetUserOrderByIdAsync(customer.Id, order.Id))!.ReceivedAmount);
        }
        actor.UserId = Guid.NewGuid();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UpdateReceivedAmountAsync(order.Id, new() { ReceivedAmount = 99m }));
        Assert.Null((await db.Orders.AsNoTracking().SingleAsync()).ReceivedAmount);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1.001")]
    [InlineData("10000000000000000")]
    public async Task RejectsInvalidAmountsBeforeSaving(string input)
    {
        var service = new OrderService(null!, NullLogger<OrderService>.Instance, null!, null!);
        var amount = decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture);
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateReceivedAmountAsync(Guid.NewGuid(), new() { ReceivedAmount = amount }));
    }

    private class Actor : ICurrentUserService
    {
        public Guid? UserId { get; set; }
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public bool IsStoreOwner => true;
    }
}
