using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using onlineStore.Controllers;
using onlineStore.Data;
using onlineStore.Security;

namespace onlineStore.Tests;

public class OrderInvoicePrintTests
{
    [Fact]
    public async Task CountsAttemptsOncePerRequestAndIsolatesStores()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE Orders (Id TEXT PRIMARY KEY, StoreId TEXT, IsDeleted INTEGER NOT NULL)");
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE OrderInvoicePrints (Id TEXT PRIMARY KEY, OrderId TEXT, PrintedByUserId TEXT, CreatedAt TEXT)");
        var store = Guid.NewGuid(); var otherStore = Guid.NewGuid(); var order = Guid.NewGuid(); var otherOrder = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Orders VALUES ({order}, {store}, {0})");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Orders VALUES ({otherOrder}, {otherStore}, {0})");
        var controller = new OrderInvoicePrintController(db, new Actor(), new Access(store));
        var request = new OrderInvoicePrintController.RecordPrintRequest(Guid.NewGuid());
        async Task<int> Record(OrderInvoicePrintController.RecordPrintRequest body) =>
            Assert.IsType<OrderInvoicePrintController.PrintCountDto>(Assert.IsType<OkObjectResult>(await controller.Record(store, order, body, default)).Value).PrintCount;
        Assert.Equal(1, await Record(request));
        Assert.Equal(1, await Record(request));
        Assert.Equal(2, await Record(new(Guid.NewGuid())));
        var sameStoreOrder = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Orders VALUES ({sameStoreOrder}, {store}, {0})");
        Assert.IsType<ConflictObjectResult>(await controller.Record(store, sameStoreOrder, request, default));
        Assert.IsType<NotFoundResult>(await controller.Record(store, otherOrder, new(Guid.NewGuid()), default));
        Assert.IsType<ForbidResult>(await controller.Record(otherStore, otherOrder, new(Guid.NewGuid()), default));
        Assert.IsType<ForbidResult>(await controller.GetCounts(otherStore, default));
        Assert.IsType<BadRequestObjectResult>(await controller.Record(store, order, new(Guid.Empty), default));
        var counts = Assert.IsType<List<OrderInvoicePrintController.PrintCountDto>>(Assert.IsType<OkObjectResult>(await controller.GetCounts(store, default)).Value);
        Assert.Equal(2, Assert.Single(counts).PrintCount);
        Assert.NotNull(counts[0].LastPrintedAt);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Orders SET IsDeleted=1 WHERE Id={order}");
        Assert.Empty(Assert.IsType<List<OrderInvoicePrintController.PrintCountDto>>(Assert.IsType<OkObjectResult>(await controller.GetCounts(store, default)).Value));
        Assert.IsType<NotFoundResult>(await controller.Record(store, order, new(Guid.NewGuid()), default));
    }

    [Fact]
    public void MigrationCreatesOnlyThePrintLogWithAnIdempotencyKeyAndOrderIndex()
    {
        var migration = new onlineStore.Migrations.AddOrderInvoicePrints();
        var table = Assert.Single(migration.UpOperations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.CreateTableOperation>());
        Assert.Equal("OrderInvoicePrints", table.Name);
        Assert.Equal("Id", Assert.Single(table.PrimaryKey!.Columns));
        Assert.Equal("Orders", Assert.Single(table.ForeignKeys).PrincipalTable);
        var index = Assert.Single(migration.UpOperations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.CreateIndexOperation>());
        Assert.Equal(new[] { "OrderId", "CreatedAt" }, index.Columns);
    }

    private class Actor : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public bool IsStoreOwner => true;
    }
    private class Access(Guid allowedStore) : IStoreAuthorizationService
    {
        public Task<bool> CanManageStoreAsync(Guid user, Guid store, CancellationToken ct = default) => Task.FromResult(store == allowedStore);
        public Task<bool> IsSuperAdminAsync(Guid user, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> CanManageProductAsync(Guid user, Guid product, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> CanManageCategoryAsync(Guid user, Guid category, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> CanManageSectionAsync(Guid user, Guid section, CancellationToken ct = default) => Task.FromResult(false);
    }
}
