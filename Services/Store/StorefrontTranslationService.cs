using Microsoft.EntityFrameworkCore;
using onlineStore.Data;
using onlineStore.DTOs.Category;
using onlineStore.DTOs.Product;
using onlineStore.DTOs.Section;
using onlineStore.DTOs.Store;
using onlineStore.Models;

namespace onlineStore.Services.Store;

public sealed class StorefrontTranslationService : IStorefrontTranslationService
{
    private readonly AppDbContext _context;

    public StorefrontTranslationService(AppDbContext context)
    {
        _context = context;
    }

    public async Task ApplyStoreAsync(
        StoreDto store,
        string? language,
        CancellationToken cancellationToken = default)
    {
        var normalizedLanguage = StoreLanguages.NormalizeOrDefault(language);
        store.Language = normalizedLanguage;

        if (StoreLanguages.IsBaseLanguage(normalizedLanguage))
            return;

        var translations = await LoadTranslationsAsync(
            store.Id,
            [store.Id],
            normalizedLanguage,
            cancellationToken);

        store.Name = GetRequired(translations, StoreTranslationEntityTypes.Store, store.Id, "Name", store.Name);
        store.Description = GetOptional(translations, StoreTranslationEntityTypes.Store, store.Id, "Description", store.Description);
        store.BusinessType = GetOptional(translations, StoreTranslationEntityTypes.Store, store.Id, "BusinessType", store.BusinessType);
        store.StoreStory = GetOptional(translations, StoreTranslationEntityTypes.Store, store.Id, "StoreStory", store.StoreStory);
    }

    public async Task ApplyCategoriesAsync(
        Guid storeId,
        IReadOnlyCollection<CategoryDto> categories,
        string? language,
        CancellationToken cancellationToken = default)
    {
        var normalizedLanguage = StoreLanguages.NormalizeOrDefault(language);
        SetLanguage(categories, normalizedLanguage);

        if (StoreLanguages.IsBaseLanguage(normalizedLanguage) || categories.Count == 0)
            return;

        var translations = await LoadTranslationsAsync(
            storeId,
            categories.Select(x => x.Id),
            normalizedLanguage,
            cancellationToken);

        foreach (var category in categories)
        {
            category.Name = GetRequired(translations, StoreTranslationEntityTypes.Category, category.Id, "Name", category.Name);
            category.Description = GetOptional(translations, StoreTranslationEntityTypes.Category, category.Id, "Description", category.Description);
        }
    }

    public async Task ApplySectionsAsync(
        Guid storeId,
        IReadOnlyCollection<SectionDto> sections,
        string? language,
        CancellationToken cancellationToken = default)
    {
        var normalizedLanguage = StoreLanguages.NormalizeOrDefault(language);
        SetLanguage(sections, normalizedLanguage);

        if (StoreLanguages.IsBaseLanguage(normalizedLanguage) || sections.Count == 0)
            return;

        var translations = await LoadTranslationsAsync(
            storeId,
            sections.Select(x => x.Id),
            normalizedLanguage,
            cancellationToken);

        foreach (var section in sections)
        {
            section.Name = GetRequired(translations, StoreTranslationEntityTypes.Section, section.Id, "Name", section.Name);
            section.Description = GetOptional(translations, StoreTranslationEntityTypes.Section, section.Id, "Description", section.Description);
        }
    }

    public async Task ApplyProductAsync(
        ProductDto? product,
        string? language,
        CancellationToken cancellationToken = default)
    {
        if (product == null)
            return;

        await ApplyProductsAsync(
            product.StoreId,
            [product],
            language,
            cancellationToken);
    }

    public async Task ApplyProductsAsync(
        Guid storeId,
        IReadOnlyCollection<ProductDto> products,
        string? language,
        CancellationToken cancellationToken = default)
    {
        var normalizedLanguage = StoreLanguages.NormalizeOrDefault(language);
        SetLanguage(products, normalizedLanguage);

        if (StoreLanguages.IsBaseLanguage(normalizedLanguage) || products.Count == 0)
            return;

        var entityIds = products
            .Select(x => x.Id)
            .Concat(products.Select(x => x.CategoryId))
            .Concat(products.Select(x => x.SectionId))
            .Concat(products.SelectMany(x => x.Variants.Select(v => v.Id)))
            .Distinct()
            .ToList();

        var translations = await LoadTranslationsAsync(
            storeId,
            entityIds,
            normalizedLanguage,
            cancellationToken);

        foreach (var product in products)
        {
            product.Name = GetRequired(translations, StoreTranslationEntityTypes.Product, product.Id, "Name", product.Name);
            product.Description = GetOptional(translations, StoreTranslationEntityTypes.Product, product.Id, "Description", product.Description);
            product.ShortDescription = GetOptional(translations, StoreTranslationEntityTypes.Product, product.Id, "ShortDescription", product.ShortDescription);
            product.CategoryName = GetOptional(translations, StoreTranslationEntityTypes.Category, product.CategoryId, "Name", product.CategoryName);
            product.SectionName = GetOptional(translations, StoreTranslationEntityTypes.Section, product.SectionId, "Name", product.SectionName);

            foreach (var variant in product.Variants)
            {
                variant.Name = GetRequired(translations, StoreTranslationEntityTypes.ProductVariant, variant.Id, "Name", variant.Name);
                variant.Description = GetOptional(translations, StoreTranslationEntityTypes.ProductVariant, variant.Id, "Description", variant.Description);
            }
        }
    }

    private async Task<Dictionary<TranslationKey, string>> LoadTranslationsAsync(
        Guid storeId,
        IEnumerable<Guid> entityIds,
        string language,
        CancellationToken cancellationToken)
    {
        var ids = entityIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<TranslationKey, string>();

        return await _context.StoreTranslations
            .AsNoTracking()
            .Where(t => t.StoreId == storeId &&
                        t.Language == language &&
                        ids.Contains(t.EntityId))
            .ToDictionaryAsync(
                t => new TranslationKey(t.EntityType, t.EntityId, t.FieldName),
                t => t.Value,
                cancellationToken);
    }

    private static string GetRequired(
        IReadOnlyDictionary<TranslationKey, string> translations,
        string entityType,
        Guid entityId,
        string fieldName,
        string fallback)
    {
        return translations.TryGetValue(new TranslationKey(entityType, entityId, fieldName), out var value) &&
            !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
    }

    private static string? GetOptional(
        IReadOnlyDictionary<TranslationKey, string> translations,
        string entityType,
        Guid entityId,
        string fieldName,
        string? fallback)
    {
        return translations.TryGetValue(new TranslationKey(entityType, entityId, fieldName), out var value) &&
            !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
    }

    private static void SetLanguage(IEnumerable<CategoryDto> categories, string language)
    {
        foreach (var category in categories)
            category.Language = language;
    }

    private static void SetLanguage(IEnumerable<SectionDto> sections, string language)
    {
        foreach (var section in sections)
            section.Language = language;
    }

    private static void SetLanguage(IEnumerable<ProductDto> products, string language)
    {
        foreach (var product in products)
            product.Language = language;
    }

    private readonly record struct TranslationKey(string EntityType, Guid EntityId, string FieldName);
}
