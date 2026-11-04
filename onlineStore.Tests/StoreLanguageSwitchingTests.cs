using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.Category;
using onlineStore.DTOs.Product;
using onlineStore.DTOs.Section;
using onlineStore.DTOs.Store;
using onlineStore.Models;
using onlineStore.Models.Identity;
using onlineStore.Services.Store;

namespace onlineStore.Tests;

public class StoreLanguageSwitchingTests
{
    [Theory]
    [InlineData(false, "E", "I", "E")]
    [InlineData(false, "I", "A", "I")]
    [InlineData(true, "I", null, "I")]
    [InlineData(true, "I", "E", "E")]
    [InlineData(true, "E", "A", "A")]
    public async Task StoreAndCatalogRespectPerStorePolicy(bool enabled, string registeredLanguage, string? requested, string expected)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var store = new Store { Name = "Original", Slug = "test", Language = registeredLanguage,
            EnableLanguageSwitching = enabled, Owner = new AppUser { FirstName = "Test", LastName = "Owner" } };
        db.Add(store);
        var categoryId = Guid.NewGuid();
        foreach (var language in new[] { "E", "I" })
        {
            db.Add(new StoreTranslation { Store = store, EntityId = store.Id, EntityType = StoreTranslationEntityTypes.Store,
                FieldName = "Name", Language = language, Value = $"Store {language}" });
            db.Add(new StoreTranslation { Store = store, EntityId = categoryId, EntityType = StoreTranslationEntityTypes.Category,
                FieldName = "Name", Language = language, Value = $"Category {language}" });
        }
        await db.SaveChangesAsync();
        var service = new StorefrontTranslationService(db);
        var dto = new StoreDto { Id = store.Id, Name = store.Name, Language = registeredLanguage, DefaultLanguage = registeredLanguage };
        var category = new CategoryDto { Id = categoryId, Name = "Original", Slug = "test" };
        var section = new SectionDto { Id = Guid.NewGuid(), Name = "Original", Slug = "test" };
        var product = new ProductDto { Id = Guid.NewGuid(), StoreId = store.Id };
        await service.ApplyStoreAsync(dto, requested);
        await service.ApplyCategoriesAsync(store.Id, [category], requested);
        await service.ApplySectionsAsync(store.Id, [section], requested);
        await service.ApplyProductAsync(product, requested);
        Assert.Equal(expected, dto.Language);
        Assert.Equal(expected, category.Language);
        Assert.Equal(expected, section.Language);
        Assert.Equal(expected, product.Language);
        Assert.Equal(registeredLanguage, dto.DefaultLanguage);
        Assert.Equal(expected == "A" ? "Original" : $"Store {expected}", dto.Name);
        Assert.Equal(expected == "A" ? "Original" : $"Category {expected}", category.Name);
        Assert.Equal(registeredLanguage, (await db.Stores.AsNoTracking().SingleAsync()).Language);
    }

    [Fact]
    public void NewStoresAndRequestsDisableSwitchingByDefault()
    {
        Assert.False(new Store().EnableLanguageSwitching);
        Assert.False(new CreateStoreDto().EnableLanguageSwitching);
        Assert.False(new StoreDto().EnableLanguageSwitching);
        Assert.Null(new UpdateStoreDto().EnableLanguageSwitching);
    }
}
