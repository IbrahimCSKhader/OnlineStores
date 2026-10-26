using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using onlineStore.Controllers;
using onlineStore.Data;
using onlineStore.DTOs.Store;
using onlineStore.Models;
using onlineStore.Models.Identity;
using onlineStore.Security;
using onlineStore.Services.Store;
using onlineStore.Utilities;

namespace onlineStore.Tests;

public class StoreAppearanceTests
{
    [Theory]
    [InlineData("primary", "red")]
    [InlineData("primary", "#fff; background:url(https://example.com)")]
    [InlineData("fontFamily", "url(example.com)")]
    [InlineData("fontSize", "9px")]
    [InlineData("fontSize", "97px")]
    [InlineData("fontSize", "16.5px")]
    [InlineData("unknown", "#ffffff")]
    public void RejectsUnsafeAndUnknownValues(string key, string value)
    {
        Assert.Throws<ArgumentException>(() => StoreAppearancePolicy.Validate(new() { Light = new() { [key] = value } }));
    }

    [Fact]
    public void AllManagementActionsRequireSuperAdmin()
    {
        var auth = Assert.Single(typeof(StoreAppearanceController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal("SuperAdmin", auth.Roles);
        Assert.DoesNotContain(typeof(StoreAppearanceController).GetMethods(), m => m.IsDefined(typeof(AllowAnonymousAttribute), true));
    }

    [Fact]
    public async Task PersistsSparseOverridesIsolatesStoresAndResetsIndividualModesAndProperties()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var owner = new AppUser { FirstName = "Test", LastName = "Owner" };
        var a = new Store { Name = "A", Slug = "appearance-a", Owner = owner, CustomDomain = "appearance.example.com" };
        var b = new Store { Name = "B", Slug = "appearance-b", Owner = owner };
        db.AddRange(a, b);
        await db.SaveChangesAsync();
        var controller = new StoreAppearanceController(db);
        Assert.IsType<OkObjectResult>(await controller.Put(a.Id, new() {
            Light = new() { ["primary"] = "#ff0000", ["storeTitleFontSize"] = "48px" },
            Dark = new() { ["primary"] = "#800080", ["productTitleFontFamily"] = "Tahoma" }
        }, default));
        db.ChangeTracker.Clear();

        // A fresh context represents a later session; public DTOs include the overrides.
        await using (var nextSession = new AppDbContext(options))
        {
            var service = new StoreService(nextSession, NullLogger<StoreService>.Instance, new AnonymousActor(), null!, null!, null!, new StorefrontTranslationService(nextSession), null!);
            var stores = await service.GetAllStoresAsync();
            Assert.Equal(2, stores.Single(x => x.Id == a.Id).Appearance.Light.Count);
            Assert.Empty(stores.Single(x => x.Id == b.Id).Appearance.Light);
            Assert.Equal("#ff0000", (await service.GetStoreBySlugAsync(a.Slug))!.Appearance.Light["primary"]);
            Assert.Equal("#800080", (await service.GetStoreByDomainAsync(a.CustomDomain!))!.Appearance.Dark["primary"]);
        }
        Assert.IsType<OkObjectResult>(await controller.ResetProperty(a.Id, "light", "primary", default));
        var persisted = StoreAppearancePolicy.Read((await db.Set<StoreAppearance>().SingleAsync()).OverridesJson);
        Assert.False(persisted.Light.ContainsKey("primary"));
        Assert.Equal("48px", persisted.Light["storeTitleFontSize"]);
        Assert.Equal("#800080", persisted.Dark["primary"]);
        Assert.IsType<OkObjectResult>(await controller.ResetMode(a.Id, "dark", default));
        persisted = StoreAppearancePolicy.Read((await db.Set<StoreAppearance>().SingleAsync()).OverridesJson);
        Assert.Empty(persisted.Dark);
        Assert.Single(persisted.Light);
        Assert.IsType<OkObjectResult>(await controller.Reset(a.Id, default));
        Assert.Empty(await db.Set<StoreAppearance>().ToListAsync());
        Assert.IsType<BadRequestResult>(await controller.ResetMode(a.Id, "invalid", default));
        Assert.IsType<NotFoundResult>(await controller.Put(Guid.NewGuid(), new(), default));
        Assert.IsType<BadRequestObjectResult>(await controller.Put(a.Id, new() { Dark = new() { ["primary"] = "invalid" } }, default));
        var deletedStore = await db.Stores.SingleAsync(x => x.Id == a.Id);
        deletedStore.IsDeleted = true;
        await db.SaveChangesAsync();
        Assert.IsType<NotFoundResult>(await controller.Get(a.Id, default));
    }

    private class AnonymousActor : ICurrentUserService
    {
        public Guid? UserId => null;
        public bool IsAuthenticated => false;
        public bool IsSuperAdmin => false;
        public bool IsStoreOwner => false;
    }

}
