using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.CustomerStore;
using onlineStore.Models;
using onlineStore.Models.Enums;
using onlineStore.Models.Identity;
using onlineStore.Models.Orders;
using onlineStore.Security;
using onlineStore.Services.CustomerStore;
using onlineStore.Utilities;

namespace onlineStore.Tests;

public class CustomerAccountLedgerTests
{
    private class Actor : ICurrentUserService
    {
        public Guid? UserId { get; set; } = Guid.NewGuid();
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public bool IsStoreOwner => true;
    }
    private class Access(Guid storeId) : IStoreAuthorizationService
    {
        public bool Allowed { get; set; } = true;
        public Task<bool> CanManageStoreAsync(Guid userId, Guid id, CancellationToken ct = default) => Task.FromResult(Allowed && id == storeId);
        public Task<bool> IsSuperAdminAsync(Guid id, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> CanManageProductAsync(Guid userId, Guid id, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> CanManageCategoryAsync(Guid userId, Guid id, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> CanManageSectionAsync(Guid userId, Guid id, CancellationToken ct = default) => Task.FromResult(false);
    }
    private class Fixture : IAsyncDisposable
    {
        public SqliteConnection Connection = new("Data Source=:memory:");
        public AppDbContext Db = null!;
        public Store Store = null!;
        public StoreCustomer Customer = null!;
        public CustomerAccountService Service = null!;
        public Access Access = null!;
        public async Task Initialize()
        {
            await Connection.OpenAsync();
            Db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(Connection).Options);
            await Db.Database.EnsureCreatedAsync();
            Store = new() { Name = "Store", Slug = "ledger", Owner = new AppUser { FirstName = "Owner", LastName = "Test" } };
            Customer = new() { Store = Store, FirstName = "Ahmad", LastName = "Test", Phone = "0591234567", Email = "ahmad@example.com" };
            Db.Add(Customer); await Db.SaveChangesAsync();
            Access = new(Store.Id); Service = new(Db, new Actor(), Access);
        }
        public async Task<Order> Order(decimal total, decimal? paid = null, bool initialized = true)
        {
            var order = new Order { StoreId = Store.Id, StoreCustomerId = Customer.Id, AccountCustomerId = initialized ? Customer.Id : null,
                TotalAmount = total, ReceivedAmount = paid, OrderNumber = Guid.NewGuid().ToString(), CustomerName = "Ahmad Test", DeliveryPhone = "0591234567" };
            Db.Add(order); await Db.SaveChangesAsync(); return order;
        }
        public RecordCustomerPaymentDto Payment(decimal amount, params (Guid order, decimal amount)[] allocations) => new()
        {
            Amount = amount, RequestKey = Guid.NewGuid().ToString(), Method = "Cash", PaymentDate = DateTime.UtcNow.AddMinutes(-1),
            Allocations = allocations.Select(x => new PaymentAllocationInput { OrderId = x.order, Amount = x.amount }).ToList()
        };
        public Task<CustomerAccountDto> Account() => Service.Account(Store.Id, Customer.Id, default);
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Connection.DisposeAsync(); }
    }

    [Theory]
    [InlineData("059-123-4567", "591234567")]
    [InlineData("+970 59 123 4567", "591234567")]
    [InlineData("00972 (59) 1234567", "591234567")]
    [InlineData("٠٥٩١٢٣٤٥٦٧", "591234567")]
    [InlineData("123", null)]
    [InlineData("0591234567 ext 5", null)]
    public void NormalizesReliablePhoneFormats(string phone, string? expected) => Assert.Equal(expected, CustomerPhone.Normalize(phone));

    [Fact]
    public async Task PartialMultipleAndCrossOrderPaymentsAreCountedOnceAndRetryIsIdempotent()
    {
        await using var f = new Fixture(); await f.Initialize();
        var a = await f.Order(200); var b = await f.Order(350);
        var payment = f.Payment(100, (a.Id, 100));
        var id = await f.Service.Payment(f.Store.Id, f.Customer.Id, payment, default);
        Assert.Equal(id, await f.Service.Payment(f.Store.Id, f.Customer.Id, payment, default));
        await f.Service.Payment(f.Store.Id, f.Customer.Id, f.Payment(150, (a.Id, 50), (b.Id, 100)), default);
        var account = await f.Account();
        Assert.Equal(550m, account.TotalOrders); Assert.Equal(250m, account.TotalPayments); Assert.Equal(300m, account.OutstandingBalance);
        Assert.Equal(50m, account.Orders.Single(x => x.Id == a.Id).RemainingAmount);
        Assert.All(account.Orders, x => Assert.Equal("partial", x.PaymentStatus));
        Assert.Equal(300m, account.Statement.Last().Balance);
        payment.Amount = 101;
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.Payment(f.Store.Id, f.Customer.Id, payment, default));
    }

    [Fact]
    public async Task AdvanceCreditAllocationReversalCancellationAndRefundStayBalanced()
    {
        await using var f = new Fixture(); await f.Initialize();
        var order = await f.Order(200);
        var payment = await f.Service.Payment(f.Store.Id, f.Customer.Id, f.Payment(250), default);
        Assert.Equal(50m, (await f.Account()).Credit);
        Assert.Equal(250m, (await f.Account()).UnallocatedCredit);
        await f.Service.AllocateCredit(f.Store.Id, f.Customer.Id, f.Payment(0, (order.Id, 200)), default);
        Assert.Equal("paid", (await f.Account()).Orders.Single().PaymentStatus);
        Assert.Equal(250m, (await f.Account()).TotalPayments);
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.Reverse(f.Store.Id, f.Customer.Id, payment, "wrong receipt", default));
        f.Db.ChangeTracker.Clear();
        order = await f.Db.Orders.SingleAsync(); order.Status = OrderStatus.Cancelled; await f.Db.SaveChangesAsync();
        var account = await f.Account(); Assert.Equal(0m, account.TotalOrders); Assert.Equal(250m, account.Credit);
        var refund = f.Payment(250, (order.Id, 200)); refund.IsRefund = true;
        await f.Service.Payment(f.Store.Id, f.Customer.Id, refund, default);
        account = await f.Account(); Assert.Equal(0m, account.Credit); Assert.Equal(0m, account.TotalPayments); Assert.Equal(0m, account.Orders.Single().PaidAmount);
    }

    [Fact]
    public async Task OrderEditsAndLegacyCorrectionsAppendHistoryAndKeepOpeningReceipts()
    {
        await using var f = new Fixture(); await f.Initialize();
        var order = await f.Order(100, 30, false);
        Assert.Equal(1, await f.Service.Backfill(f.Store.Id, default));
        Assert.Equal(0, await f.Service.Backfill(f.Store.Id, default));
        Assert.Equal(30m, (await f.Account()).TotalPayments);
        order.TotalAmount = 150; order.ReceivedAmount = 75; await f.Db.SaveChangesAsync();
        var account = await f.Account(); Assert.Equal(150m, account.TotalOrders); Assert.Equal(75m, account.TotalPayments); Assert.Equal(75m, account.OutstandingBalance);
        Assert.Equal(2, account.Statement.Count(x => x.Kind == "OrderCharge"));
        Assert.Single(account.Statement, x => x.Kind == "OpeningReceipt");
        order.Status = OrderStatus.Refunded; await f.Db.SaveChangesAsync();
        Assert.Equal(75m, (await f.Account()).Credit);
        order.Status = OrderStatus.Pending; await f.Db.SaveChangesAsync();
        Assert.Equal(75m, (await f.Account()).OutstandingBalance);
    }

    [Fact]
    public async Task IdentityIsStoreScopedAndSharedOrMissingPhoneNeverMatchesByNameAlone()
    {
        await using var f = new Fixture(); await f.Initialize();
        var match = await CustomerIdentity.ResolveAsync(f.Db, f.Store.Id, "Ahmad Test", "+970591234567");
        Assert.Equal(f.Customer.Id, match.Id);
        var shared = await CustomerIdentity.ResolveAsync(f.Db, f.Store.Id, "Different Person", "0591234567");
        Assert.NotEqual(f.Customer.Id, shared.Id); Assert.True(shared.HasSharedPhone); await f.Db.SaveChangesAsync();
        var missing = await CustomerIdentity.ResolveAsync(f.Db, f.Store.Id, "Ahmad Test", null);
        Assert.NotEqual(f.Customer.Id, missing.Id);
        var other = new Store { Name = "Other", Slug = "other", OwnerId = f.Store.OwnerId }; f.Db.Add(other); await f.Db.SaveChangesAsync();
        var isolated = await CustomerIdentity.ResolveAsync(f.Db, other.Id, "Ahmad Test", "0591234567");
        Assert.NotEqual(f.Customer.Id, isolated.Id);
    }

    [Fact]
    public async Task MergeAndReassignmentPreserveSnapshotsAndLeaveAuditWithoutChangingLoginOwnership()
    {
        await using var f = new Fixture(); await f.Initialize();
        var order = await f.Order(100, 50);
        var target = new StoreCustomer { StoreId = f.Store.Id, Email = "target@example.com", FirstName = "Target" }; f.Db.Add(target); await f.Db.SaveChangesAsync();
        await f.Service.Reassign(f.Store.Id, order.Id, target.Id, "correct phone association", default);
        Assert.Equal(f.Customer.Id, order.StoreCustomerId); Assert.Equal("Ahmad Test", order.CustomerName);
        Assert.Equal(50m, (await f.Service.Account(f.Store.Id, target.Id, default)).OutstandingBalance);
        await f.Service.Merge(f.Store.Id, f.Customer.Id, target.Id, "duplicate", default);
        Assert.Equal(target.Id, f.Customer.MergedIntoCustomerId);
        Assert.Equal(2, await f.Db.CustomerAccountAudits.CountAsync());
        Assert.Equal(50m, (await f.Service.Account(f.Store.Id, target.Id, default)).TotalPayments);
    }

    [Fact]
    public async Task RejectsCrossStoreAndCrossCustomerAccessAndInvalidMoney()
    {
        await using var f = new Fixture(); await f.Initialize(); var order = await f.Order(100);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.Account(Guid.NewGuid(), f.Customer.Id, default));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.Payment(f.Store.Id, f.Customer.Id, f.Payment(110, (order.Id, 110)), default));
        foreach (var amount in new[] { -1m, 0m, 1.001m })
            await Assert.ThrowsAsync<ArgumentException>(() => f.Service.Payment(f.Store.Id, f.Customer.Id, f.Payment(amount), default));
        f.Access.Allowed = false;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.Payment(f.Store.Id, f.Customer.Id, f.Payment(10), default));
    }

    [Fact]
    public async Task RegisteredCheckoutReusesGuestProfileWithoutChangingCredentialsOrOrderAuthorization()
    {
        await using var f = new Fixture(); await f.Initialize();
        var guest = new StoreCustomer { StoreId = f.Store.Id, FirstName = "Ahmad", LastName = "Test", Phone = "+970591234567",
            Email = "guest@profiles.invalid", IsGuestProfile = true };
        f.Db.Add(guest); await f.Db.SaveChangesAsync();
        var identity = await CustomerIdentity.ResolveAsync(f.Db, f.Store.Id, "ignored", null, f.Customer.Id);
        Assert.Equal(guest.Id, identity.Id); Assert.Equal(guest.Id, f.Customer.MergedIntoCustomerId);
        Assert.Equal("ahmad@example.com", f.Customer.Email);
        await f.Db.SaveChangesAsync();
        Assert.Single(await f.Db.CustomerAccountAudits.ToListAsync());
    }

    [Fact]
    public async Task PostedReceiptsCannotBeEditedAndReversalIsIdempotent()
    {
        await using var f = new Fixture(); await f.Initialize(); var order = await f.Order(100);
        var payment = await f.Service.Payment(f.Store.Id, f.Customer.Id, f.Payment(50, (order.Id, 50)), default);
        var reversed = await f.Service.Reverse(f.Store.Id, f.Customer.Id, payment, "duplicate receipt", default);
        Assert.Equal(reversed, await f.Service.Reverse(f.Store.Id, f.Customer.Id, payment, "duplicate receipt", default));
        Assert.Equal(0m, (await f.Account()).TotalPayments);
        var entry = await f.Db.CustomerLedgerEntries.SingleAsync(x => x.Id == payment); entry.Amount = -500;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task ContactSnapshotChangesCannotRepostStaleOrderChargesOrReceipts()
    {
        await using var f = new Fixture(); await f.Initialize(); var stale = await f.Order(100, 20);
        await using (var other = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(f.Connection).Options))
        {
            var current = await other.Orders.SingleAsync(); current.ReceivedAmount = 50; current.Status = OrderStatus.Cancelled;
            await other.SaveChangesAsync();
        }
        stale.CustomerName = "Historical name snapshot";
        await f.Db.SaveChangesAsync();
        var account = await f.Account(); Assert.Equal(0m, account.TotalOrders); Assert.Equal(50m, account.Credit);
        Assert.Equal(4, account.Statement.Count);
    }
}
