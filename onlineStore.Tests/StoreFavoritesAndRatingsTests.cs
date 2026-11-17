using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using onlineStore.Controllers;
using onlineStore.Data;
using onlineStore.Models;
using onlineStore.Models.Enums;
using onlineStore.Models.Identity;
using onlineStore.Security;
using onlineStore.Services.Store;

namespace onlineStore.Tests;

public class StoreFavoritesAndRatingsTests
{
    [Fact]
    public async Task FavoritesMergeIsIdempotentAndScopedToTheStoreAndCustomer()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDatabaseAsync(connection);
        var first = AddStore(db, "first");
        var second = AddStore(db, "second");
        var online = AddProduct(db, first, "online");
        var offline = AddProduct(db, first, "offline");
        var draft = AddProduct(db, first, "draft", ProductStatus.Draft);
        var other = AddProduct(db, second, "other");
        var customer = new StoreCustomer { Store = first, Email = "first@example.com" };
        var anotherCustomer = new StoreCustomer { Store = first, Email = "second@example.com" };
        db.AddRange(customer, anotherCustomer);
        await db.SaveChangesAsync();

        var service = new StoreFavoriteService(db);
        var controller = new StoreFavoritesController(service)
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext { User = CustomerPrincipal(customer) } }
        };
        Assert.IsType<ForbidResult>(await controller.Get(second.Id, default));
        Assert.IsType<NotFoundResult>(await controller.Add(first.Id, other.Id, default));
        Assert.IsType<NoContentResult>(await controller.Add(first.Id, online.Id, default));

        var request = new MergeFavoritesRequest([online.Id, offline.Id, offline.Id, draft.Id, other.Id, Guid.NewGuid()]);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var response = Assert.IsType<OkObjectResult>(await controller.Merge(first.Id, request, default));
            Assert.Equal(new[] { online.Id, offline.Id }.Order(), Assert.IsType<Guid[]>(response.Value).Order());
        }
        Assert.Empty(await service.GetAsync(first.Id, anotherCustomer.Id, default));
        Assert.Equal(2, await db.StoreFavorites.CountAsync());

        Assert.IsType<NoContentResult>(await controller.Remove(first.Id, offline.Id, default));
        Assert.IsType<NoContentResult>(await controller.Remove(first.Id, offline.Id, default));
        Assert.Equal([online.Id], await service.GetAsync(first.Id, customer.Id, default));
        online.IsDeleted = true;
        await db.SaveChangesAsync();
        Assert.Empty(await service.GetAsync(first.Id, customer.Id, default));
    }

    [Fact]
    public async Task GuestAndCustomerRatingsAreUpdatedAndCountedPerStore()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDatabaseAsync(connection);
        var first = AddStore(db, "first");
        var second = AddStore(db, "second");
        first.VisitCount = 25;
        AddProduct(db, first, "active");
        AddProduct(db, first, "draft", ProductStatus.Draft);
        AddProduct(db, second, "other");
        var customer = new StoreCustomer { Store = first, Email = "customer@example.com" };
        db.Add(customer);
        await db.SaveChangesAsync();

        var controller = new StoreStatisticsController(new(db), new(db))
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext() }
        };
        var before = Assert.IsType<StoreStatistics>(Assert.IsType<OkObjectResult>((await controller.Get(first.Id, default)).Result).Value);
        Assert.Equal(new StoreStatistics(25, 1, null, 0), before);
        Assert.IsType<BadRequestResult>((await controller.Rate(first.Id, new(4, null), default)).Result);
        var guest = Guid.NewGuid();
        Assert.IsType<OkObjectResult>((await controller.Rate(first.Id, new(2, guest), default)).Result);
        Assert.IsType<OkObjectResult>((await controller.Rate(first.Id, new(4, guest), default)).Result);
        Assert.IsType<OkObjectResult>((await controller.Rate(second.Id, new(1, guest), default)).Result);

        controller.HttpContext.User = CustomerPrincipal(customer);
        Assert.IsType<ForbidResult>((await controller.Rate(second.Id, new(4, null), default)).Result);
        var after = Assert.IsType<StoreStatistics>(Assert.IsType<OkObjectResult>((await controller.Rate(first.Id, new(5, null), default)).Result).Value);
        Assert.Equal(new StoreStatistics(25, 1, 4.5, 2), after);
        var other = Assert.IsType<StoreStatistics>(Assert.IsType<OkObjectResult>((await controller.Get(second.Id, default)).Result).Value);
        Assert.Equal(new StoreStatistics(0, 1, 1, 1), other);
    }

    private static async Task<AppDbContext> CreateDatabaseAsync(SqliteConnection connection)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static Store AddStore(AppDbContext db, string slug)
    {
        var store = new Store { Name = slug, Slug = slug, Owner = new AppUser { FirstName = "Owner", LastName = slug } };
        db.Add(store);
        return store;
    }

    private static Product AddProduct(AppDbContext db, Store store, string slug, ProductStatus status = ProductStatus.Active)
    {
        var section = new Section { Store = store, Name = slug, Slug = slug };
        var category = new Category { Store = store, Name = slug, Slug = slug };
        var product = new Product { Store = store, Section = section, Category = category, Name = slug, Slug = slug, Status = status };
        db.Add(product);
        return product;
    }

    private static ClaimsPrincipal CustomerPrincipal(StoreCustomer customer) =>
        new(new ClaimsIdentity([
            new Claim(StoreCustomerClaimTypes.AccountType, StoreCustomerClaimTypes.StoreCustomerAccountType),
            new Claim(StoreCustomerClaimTypes.StoreId, customer.StoreId.ToString()),
            new Claim(StoreCustomerClaimTypes.StoreCustomerId, customer.Id.ToString()),
        ], "Test"));
}
