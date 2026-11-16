using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.Store;
using onlineStore.DTOs.Category;
using onlineStore.DTOs.Section;
using onlineStore.DTOs.Product;
using onlineStore.Models;
using onlineStore.Models.Identity;
using onlineStore.Services.Store;

namespace onlineStore.Tests;

public class CatalogTranslationsTests
{
    [Theory]
    [InlineData("Category")]
    [InlineData("Section")]
    [InlineData("Product")]
    public async Task SavesReadsUpdatesClearsAndPreservesOtherLanguages(string entityType)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var store = new Store { Name = "Original", Slug = "test", Language = "E", EnableLanguageSwitching = true,
            Owner = new AppUser { FirstName = "Test", LastName = "Owner" } };
        db.Add(store);
        await db.SaveChangesAsync();
        var id = Guid.NewGuid();
        await CatalogTranslations.StageAsync(db, store.Id, entityType, id,
            [new() { Language = "A", Name = "عربي", Description = "وصف" }, new() { Language = "I", Name = "עברית" }]);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var service = new StorefrontTranslationService(db);
        var category = new CategoryDto { Id = id, StoreId = store.Id, Name = "Original" };
        var section = new SectionDto { Id = id, StoreId = store.Id, Name = "Original" };
        var product = new ProductDto { Id = id, StoreId = store.Id, Name = "Original" };
        if (entityType == "Category") {
            await service.ApplyCategoriesAsync(store.Id, [category], "A");
            Assert.Equal("عربي", category.Name); Assert.Equal("Original", category.BaseName);
            Assert.Equal(2, category.Translations!.Count);
        } else if (entityType == "Section") {
            await service.ApplySectionsAsync(store.Id, [section], "A"); Assert.Equal("عربي", section.Name);
        } else {
            await service.ApplyProductAsync(product, "A"); Assert.Equal("عربي", product.Name);
        }
        await CatalogTranslations.StageAsync(db, store.Id, entityType, id, null);
        await db.SaveChangesAsync();
        Assert.Equal(3, await db.StoreTranslations.CountAsync());
        await CatalogTranslations.StageAsync(db, store.Id, entityType, id, [new() { Language = "A", Name = "تعديل" }]);
        await db.SaveChangesAsync();
        Assert.Equal(2, await db.StoreTranslations.CountAsync());
        await CatalogTranslations.StageAsync(db, store.Id, entityType, id, [new() { Language = "A", Name = " " }]);
        await db.SaveChangesAsync();
        Assert.Equal("I", (await db.StoreTranslations.SingleAsync()).Language);
        var fallback = new ProductDto { Id = id, StoreId = store.Id, Name = "Original" };
        await service.ApplyProductAsync(fallback, "A");
        Assert.Equal("Original", fallback.Name);
        Assert.Empty(await CatalogTranslations.ReadAsync(db, Guid.NewGuid(), entityType, [id]));
    }

    [Theory]
    [InlineData(false, "I", false)]
    [InlineData(true, "A", false)]
    [InlineData(true, "xx", false)]
    [InlineData(true, "I", true)]
    public async Task RejectsDisabledBaseUnknownAndDuplicateLanguages(bool enabled, string language, bool duplicate)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var store = new Store { Name = "Original", Slug = "test", Language = "A", EnableLanguageSwitching = enabled,
            Owner = new AppUser { FirstName = "Test", LastName = "Owner" } };
        db.Add(store); await db.SaveChangesAsync();
        var input = new List<CatalogTranslationDto> { new() { Language = language, Name = "Translated" } };
        if (duplicate) input.Add(new() { Language = language, Name = "Duplicate" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => CatalogTranslations.StageAsync(db, store.Id, "Product", Guid.NewGuid(), input));
        Assert.Empty(db.ChangeTracker.Entries<StoreTranslation>());
    }
}
