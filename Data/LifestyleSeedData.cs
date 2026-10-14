using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using onlineStore.Models;
using onlineStore.Models.Enums;
using onlineStore.Models.Identity;
using onlineStore.Models.Subscriptions;
using onlineStore.Models.Subscriptions.Enums;
using System.Net;

namespace onlineStore.Data;

public static class LifestyleSeedData
{
    private const string OwnerPassword = "PASS1234";
    private static Dictionary<string, string> PublishedImages = new();
    private static Dictionary<string, PhotoCatalogEntry> PhotoCatalog = new();

    public static async Task RepairPhotosAsync(AppDbContext context, string webRootPath)
    {
        var directory = Path.Combine(webRootPath, "seed-images");
        var images = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
            await File.ReadAllTextAsync(Path.Combine(directory, "lifestyle-published-images.json")))!;
        var catalog = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, PhotoCatalogEntry>>(
            await File.ReadAllTextAsync(Path.Combine(directory, "lifestyle-photo-catalog.json")))!;
        foreach (var slug in new[] { "style-wave", "mug-mood" })
        {
            context.ChangeTracker.Clear();
            var store = await context.Stores.SingleAsync(x => x.Slug == slug);
            store.LogoUrl = images[$"{slug}/store-logo"];
            store.CoverImageUrl = images[$"{slug}/store-cover"];
            var categories = await context.Categories.Where(x => x.StoreId == store.Id).ToListAsync();
            foreach (var category in categories)
            {
                category.ImageUrl = images[$"{slug}/category-{category.Slug[(slug.Length + 1)..]}"];
                if (slug == "style-wave" && category.Slug.EndsWith("-bottoms"))
                    category.Name = "بناطيل";
                if (slug == "style-wave" && category.Slug.EndsWith("-hoodies"))
                    category.Name = "هوديز وسويت شيرتات";
            }
            await context.SaveChangesAsync();
            var productIds = await context.Products.Where(x => x.StoreId == store.Id).Select(x => x.Id).ToListAsync();
            foreach (var productId in productIds)
            {
                context.ChangeTracker.Clear();
                var product = await context.Products.IgnoreQueryFilters().Include(x => x.Images)
                    .Include(x => x.Variants).SingleAsync(x => x.Id == productId);
                var key = $"{slug}/{product.Slug[(slug.Length + 1)..]}";
                var entry = catalog[key];
                product.Name = entry.Name;
                product.Description = entry.Description;
                product.ShortDescription = entry.Description;
                product.MetaTitle = entry.Name;
                product.MetaDescription = entry.Description;
                product.ThumbnailUrl = images[$"{key}-1"];
                context.ProductImages.RemoveRange(product.Images);
                for (var i = 1; i <= 3; i++)
                    context.ProductImages.Add(new ProductImage
                    {
                        ProductId = product.Id, Url = images[$"{key}-{i}"],
                        AltText = $"{entry.Name} - {i}", DisplayOrder = i, IsPrimary = i == 1
                    });
                var selectedVariantIds = product.Variants
                    .Where(v => v.SortOrder >= 1 && v.SortOrder <= entry.VariantNames.Length)
                    .GroupBy(v => v.SortOrder)
                    .Select(group => group.OrderBy(v => v.SKU != $"{product.SKU}-V{group.Key}")
                        .ThenBy(v => v.IsDeleted).First().Id)
                    .ToHashSet();
                foreach (var variant in product.Variants)
                {
                    var index = variant.SortOrder - 1;
                    var active = selectedVariantIds.Contains(variant.Id);
                    variant.IsActive = active;
                    variant.IsDeleted = !active;
                    variant.IsDefault = active && index == 0;
                    if (!active) continue;
                    variant.Name = $"{entry.Name} - {entry.VariantNames[index]}";
                    variant.Description = entry.VariantNames[index];
                    variant.ImageUrl = entry.VariantImageUrls[index];
                }
                await context.SaveChangesAsync();
                Console.WriteLine($"Repaired photos: {product.Slug}");
            }
        }
    }

    public static async Task SeedAsync(AppDbContext context, UserManager<AppUser> userManager, string webRootPath)
    {
        var manifestPath = Path.Combine(webRootPath, "seed-images", "lifestyle-published-images.json");
        if (File.Exists(manifestPath))
            PublishedImages = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
                await File.ReadAllTextAsync(manifestPath)) ?? new();
        var catalogPath = Path.Combine(webRootPath, "seed-images", "lifestyle-photo-catalog.json");
        if (File.Exists(catalogPath))
            PhotoCatalog = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, PhotoCatalogEntry>>(
                await File.ReadAllTextAsync(catalogPath)) ?? new();
        foreach (var seed in Stores())
        {
            EnsureImageAssets(webRootPath, seed);

            var owner = await EnsureOwnerAsync(userManager, seed);
            var store = await EnsureStoreAsync(context, seed, owner.Id);

            await HideObsoleteCatalogAsync(context, store.Id, seed);

            var categories = await EnsureCategoriesAsync(context, store.Id, seed);
            var sections = await EnsureSectionsAsync(context, store.Id, seed);
            await EnsureContactAccountsAsync(context, store.Id, seed);
            await EnsureSubscriptionAsync(context, store.Id);
            await EnsureProductsAsync(context, store.Id, seed, categories, sections);
            await context.SaveChangesAsync();
            await EnsureStorefrontTranslationsAsync(context, store.Id, seed, categories, sections);
        }

        await context.SaveChangesAsync();
    }

    public static async Task RepairAlsujoodAssetsAsync(AppDbContext context, string webRootPath)
    {
        var seed = CosmeticsStore();
        EnsureImageAssets(webRootPath, seed);

        var store = await context.Stores
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Slug == seed.Slug);

        if (store == null)
            throw new InvalidOperationException($"Store '{seed.Slug}' was not found.");

        store.LogoUrl = AssetUrl(seed, "store-logo");
        store.CoverImageUrl = AssetUrl(seed, "store-cover");
        store.IsActive = true;
        store.IsDeleted = false;

        var categories = await context.Categories
            .IgnoreQueryFilters()
            .Where(x => x.StoreId == store.Id)
            .ToListAsync();

        foreach (var categorySeed in seed.Categories)
        {
            var category = categories.FirstOrDefault(x =>
                string.Equals(x.Slug, CategorySlug(seed, categorySeed.Key), StringComparison.OrdinalIgnoreCase));
            if (category == null)
                continue;

            category.ImageUrl = AssetUrl(seed, $"category-{categorySeed.Key}");
            category.IsActive = true;
            category.IsDeleted = false;
        }

        var products = await context.Products
            .IgnoreQueryFilters()
            .Include(x => x.Images)
            .Include(x => x.Variants)
            .Where(x => x.StoreId == store.Id)
            .ToListAsync();

        for (var i = 0; i < seed.Products.Length; i++)
        {
            var productSeed = seed.Products[i];
            var product = products.FirstOrDefault(x =>
                string.Equals(x.Slug, ProductSlug(seed, productSeed.Slug), StringComparison.OrdinalIgnoreCase));
            if (product == null)
                continue;

            product.ThumbnailUrl = ProductImageUrl(seed, productSeed, 1);
            product.IsDeleted = false;
            product.Status = ProductStatus.Active;

            context.ProductImages.RemoveRange(product.Images);
            for (var imageIndex = 1; imageIndex <= 3; imageIndex++)
            {
                context.ProductImages.Add(new ProductImage
                {
                    ProductId = product.Id,
                    Url = ProductImageUrl(seed, productSeed, imageIndex),
                    AltText = $"{productSeed.Name} - صورة {imageIndex}",
                    DisplayOrder = imageIndex,
                    IsPrimary = imageIndex == 1
                });
            }

            for (var variantIndex = 0; variantIndex < productSeed.Variants.Length; variantIndex++)
            {
                var sku = $"{seed.SkuPrefix}-{(i + 1):000}-V{variantIndex + 1}";
                var variant = product.Variants.FirstOrDefault(x => x.SKU == sku);
                if (variant == null)
                    continue;

                variant.ImageUrl = ProductImageUrl(seed, productSeed, variantIndex == 2 ? 1 : variantIndex + 1);
                variant.IsActive = true;
                variant.IsDeleted = false;
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task<AppUser> EnsureOwnerAsync(UserManager<AppUser> userManager, StoreSeed seed)
    {
        var owner = await userManager.FindByEmailAsync(seed.OwnerEmail);

        if (owner == null)
        {
            owner = new AppUser
            {
                FirstName = seed.OwnerFirstName,
                LastName = "Owner",
                Email = seed.OwnerEmail,
                UserName = seed.OwnerEmail,
                EmailConfirmed = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var createResult = await userManager.CreateAsync(owner);
            EnsureIdentityResultSucceeded(createResult, $"create store owner {seed.OwnerEmail}");
        }

        var shouldUpdate = owner.FirstName != seed.OwnerFirstName || owner.LastName != "Owner"
            || owner.Email != seed.OwnerEmail || owner.UserName != seed.OwnerEmail
            || !owner.EmailConfirmed || !owner.IsActive || owner.IsDeleted;
        owner.FirstName = seed.OwnerFirstName;
        owner.LastName = "Owner";
        owner.Email = seed.OwnerEmail;
        owner.UserName = seed.OwnerEmail;
        owner.EmailConfirmed = true;
        owner.IsActive = true;
        owner.IsDeleted = false;

        if (!await userManager.CheckPasswordAsync(owner, OwnerPassword))
        {
            owner.PasswordHash = userManager.PasswordHasher.HashPassword(owner, OwnerPassword);
            owner.SecurityStamp = Guid.NewGuid().ToString();
            owner.AccessFailedCount = 0;
            owner.LockoutEnd = null;
            shouldUpdate = true;
        }

        if (shouldUpdate)
        {
            var updateResult = await userManager.UpdateAsync(owner);
            EnsureIdentityResultSucceeded(updateResult, $"update store owner {seed.OwnerEmail}");
        }

        if (!await userManager.IsInRoleAsync(owner, "StoreOwner"))
        {
            var roleResult = await userManager.AddToRoleAsync(owner, "StoreOwner");
            EnsureIdentityResultSucceeded(roleResult, $"assign StoreOwner role to {seed.OwnerEmail}");
        }

        return owner;
    }

    private static async Task<Store> EnsureStoreAsync(AppDbContext context, StoreSeed seed, Guid ownerId)
    {
        var store = await context.Stores
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Slug == seed.Slug);

        if (store == null)
        {
            store = new Store
            {
                Slug = seed.Slug,
                OwnerId = ownerId
            };

            context.Stores.Add(store);
        }

        store.Name = seed.Name;
        store.Description = seed.Description;
        store.BusinessType = seed.BusinessType;
        store.LogoUrl = UseLocalSeedAssets(seed) ? AssetUrl(seed, "store-logo") : seed.LogoImageUrl ?? AssetUrl(seed, "store-logo");
        store.CoverImageUrl = UseLocalSeedAssets(seed) ? AssetUrl(seed, "store-cover") : seed.CoverImageUrl ?? AssetUrl(seed, "store-cover");
        store.WhatsAppNumber = seed.WhatsAppNumber;
        store.StoreStory = seed.StoreStory;
        store.ThemeTemplate = seed.ThemeTemplate;
        store.Language = StoreLanguages.Arabic;
        store.IsActive = true;
        store.IsDeleted = false;
        store.OwnerId = ownerId;

        return store;
    }

    private static async Task HideObsoleteCatalogAsync(AppDbContext context, Guid storeId, StoreSeed seed)
    {
        var desiredProductSlugs = seed.Products
            .Select(x => ProductSlug(seed, x.Slug))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var desiredCategorySlugs = seed.Categories
            .Select(x => CategorySlug(seed, x.Key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var products = await context.Products
            .IgnoreQueryFilters()
            .Include(x => x.Variants)
            .Where(x => x.StoreId == storeId)
            .ToListAsync();

        foreach (var product in products.Where(x => !desiredProductSlugs.Contains(x.Slug)))
        {
            product.IsDeleted = true;
            product.Status = ProductStatus.Draft;
            product.IsFeatured = false;
            product.SKU = $"{seed.SkuPrefix}-OLD-{product.Id:N}";

            foreach (var variant in product.Variants)
            {
                variant.IsDeleted = true;
                variant.IsActive = false;
                variant.IsDefault = false;
            }
        }

        var categories = await context.Categories
            .IgnoreQueryFilters()
            .Where(x => x.StoreId == storeId)
            .ToListAsync();

        foreach (var category in categories.Where(x => !desiredCategorySlugs.Contains(x.Slug)))
        {
            category.IsDeleted = true;
            category.IsActive = false;
        }

        var sections = await context.Sections
            .IgnoreQueryFilters()
            .Where(x => x.StoreId == storeId)
            .ToListAsync();

        foreach (var section in sections.Where(x => !desiredCategorySlugs.Contains(x.Slug)))
        {
            section.IsDeleted = true;
            section.IsActive = false;
        }
    }

    private static async Task<Dictionary<string, Category>> EnsureCategoriesAsync(
        AppDbContext context,
        Guid storeId,
        StoreSeed seed)
    {
        var result = new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < seed.Categories.Length; i++)
        {
            var definition = seed.Categories[i];
            var slug = CategorySlug(seed, definition.Key);
            var category = await context.Categories
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.StoreId == storeId && x.Slug == slug);

            if (category == null)
            {
                category = new Category
                {
                    StoreId = storeId,
                    Slug = slug
                };

                context.Categories.Add(category);
            }

            category.Name = definition.Name;
            category.Description = definition.Description;
            category.ImageUrl = UseLocalSeedAssets(seed)
                ? AssetUrl(seed, $"category-{definition.Key}")
                : definition.ImageUrl ?? AssetUrl(seed, $"category-{definition.Key}");
            category.DisplayOrder = i + 1;
            category.IsActive = true;
            category.IsDeleted = false;
            result[definition.Key] = category;
        }

        return result;
    }

    private static async Task<Dictionary<string, Section>> EnsureSectionsAsync(
        AppDbContext context,
        Guid storeId,
        StoreSeed seed)
    {
        var result = new Dictionary<string, Section>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < seed.Categories.Length; i++)
        {
            var definition = seed.Categories[i];
            var slug = CategorySlug(seed, definition.Key);
            var section = await context.Sections
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.StoreId == storeId && x.Slug == slug);

            if (section == null)
            {
                section = new Section
                {
                    StoreId = storeId,
                    Slug = slug
                };

                context.Sections.Add(section);
            }

            section.Name = definition.Name;
            section.Description = definition.Description;
            section.DisplayOrder = i + 1;
            section.IsActive = true;
            section.IsDeleted = false;
            result[definition.Key] = section;
        }

        return result;
    }

    private static async Task EnsureContactAccountsAsync(AppDbContext context, Guid storeId, StoreSeed seed)
    {
        var accounts = new[]
        {
            new ContactSeed(StoreContactPlatforms.WhatsApp, seed.WhatsAppNumber, "WhatsApp", 1),
            new ContactSeed(StoreContactPlatforms.Instagram, seed.Instagram, "Instagram", 2),
            new ContactSeed(StoreContactPlatforms.Facebook, seed.Facebook, "Facebook", 3),
            new ContactSeed(StoreContactPlatforms.TikTok, seed.TikTok, "TikTok", 4)
        };

        foreach (var accountSeed in accounts)
        {
            var account = await context.StoreContactAccounts
                .FirstOrDefaultAsync(x => x.StoreId == storeId && x.Platform == accountSeed.Platform);

            if (account == null)
            {
                account = new StoreContactAccount
                {
                    StoreId = storeId,
                    Platform = accountSeed.Platform
                };

                context.StoreContactAccounts.Add(account);
            }

            account.Username = accountSeed.Username;
            account.Label = accountSeed.Label;
            account.SortOrder = accountSeed.SortOrder;
            account.IsDeleted = false;
        }
    }

    private static async Task EnsureSubscriptionAsync(AppDbContext context, Guid storeId)
    {
        var proPlan = await context.SubscriptionPlans
            .FirstOrDefaultAsync(x => x.Code == SubscriptionPlanCodes.Pro && x.IsActive);

        if (proPlan == null)
            return;

        var hasActiveSubscription = await context.StoreSubscriptions.AnyAsync(x =>
            x.StoreId == storeId &&
            x.Status == SubscriptionStatus.Active &&
            (!x.EndDate.HasValue || x.EndDate >= DateTime.UtcNow));

        if (hasActiveSubscription)
            return;

        context.StoreSubscriptions.Add(new StoreSubscription
        {
            StoreId = storeId,
            SubscriptionPlanId = proPlan.Id,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddYears(1),
            Status = SubscriptionStatus.Active,
            PaidAmount = 0m,
            Currency = proPlan.Currency,
            IsAutoRenew = true,
            Notes = "Seeded rich demo store subscription."
        });
    }

    private static async Task EnsureProductsAsync(
        AppDbContext context,
        Guid storeId,
        StoreSeed storeSeed,
        IReadOnlyDictionary<string, Category> categories,
        IReadOnlyDictionary<string, Section> sections)
    {
        for (var i = 0; i < storeSeed.Products.Length; i++)
        {
            var seed = storeSeed.Products[i];
            if (PhotoCatalog.TryGetValue($"{storeSeed.AssetFolder}/{seed.Slug}", out var photoEntry))
                seed = seed with
                {
                    Name = photoEntry.Name,
                    Description = photoEntry.Description,
                    ShortDescription = photoEntry.Description,
                    Variants = photoEntry.VariantNames.Select(name => new VariantSeed(name, 0m, 14, name)).ToArray()
                };
            var slug = ProductSlug(storeSeed, seed.Slug);
            var product = await context.Products
                .IgnoreQueryFilters()
                .Include(x => x.Variants)
                .FirstOrDefaultAsync(x => x.StoreId == storeId && x.Slug == slug);

            if (product == null)
            {
                product = new Product
                {
                    StoreId = storeId,
                    Slug = slug
                };

                context.Products.Add(product);
            }

            product.SKU = $"{storeSeed.SkuPrefix}-{(i + 1):000}";
            product.Name = seed.Name;
            product.CategoryId = categories[seed.CategoryKey].Id;
            product.SectionId = sections[seed.CategoryKey].Id;
            product.Description = seed.Description;
            product.ShortDescription = seed.ShortDescription;
            product.Price = seed.Price;
            product.CompareAtPrice = seed.CompareAtPrice;
            product.CostPrice = Math.Round(seed.Price * 0.55m, 2);
            product.WholesalePrice = Math.Round(seed.Price * 0.78m, 2);
            product.StockQuantity = seed.Stock;
            product.TrackInventory = true;
            product.Status = ProductStatus.Active;
            product.IsFeatured = i < 8;
            product.MetaTitle = $"{seed.Name} | {storeSeed.Name}";
            product.MetaDescription = seed.ShortDescription;
            product.ThumbnailUrl = ProductImageUrl(storeSeed, seed, 1);
            product.IsDeleted = false;

            await EnsureImagesAsync(context, product, storeSeed, seed);
            EnsureVariants(context, product, storeSeed, seed, i + 1);
        }
    }

    private static async Task EnsureImagesAsync(
        AppDbContext context,
        Product product,
        StoreSeed storeSeed,
        ProductSeed seed)
    {
        var existingImages = await context.ProductImages.IgnoreQueryFilters()
            .Where(x => x.ProductId == product.Id)
            .ToListAsync();
        context.ProductImages.RemoveRange(existingImages);

        var imageCount = UseLocalSeedAssets(storeSeed)
            ? 3
            : seed.ImageUrls?.Length > 0 ? seed.ImageUrls.Length : 3;
        for (var i = 1; i <= imageCount; i++)
        {
            context.ProductImages.Add(new ProductImage
            {
                ProductId = product.Id,
                Url = ProductImageUrl(storeSeed, seed, i),
                AltText = $"{seed.Name} - صورة {i}",
                DisplayOrder = i,
                IsPrimary = i == 1
            });
        }
    }

    private static void EnsureVariants(
        AppDbContext context,
        Product product,
        StoreSeed storeSeed,
        ProductSeed seed,
        int productNumber)
    {
        foreach (var variant in product.Variants)
        {
            variant.IsDeleted = true;
            variant.IsActive = false;
            variant.IsDefault = false;
        }

        for (var i = 0; i < seed.Variants.Length; i++)
        {
            var seedVariant = seed.Variants[i];
            var sku = $"{storeSeed.SkuPrefix}-{productNumber:000}-V{i + 1}";
            var variant = product.Variants.FirstOrDefault(x => x.SKU == sku);

            if (variant == null)
            {
                variant = new ProductVariant { SKU = sku };
                product.Variants.Add(variant);
                context.ProductVariants.Add(variant);
            }

            variant.Name = $"{seed.Name} - {seedVariant.Name}";
            variant.Description = seedVariant.Description;
            variant.Price = seed.Price + seedVariant.PriceDelta;
            variant.CompareAtPrice = seed.CompareAtPrice + seedVariant.PriceDelta;
            variant.StockQuantity = seedVariant.Stock;
            variant.ImageUrl = ProductImageUrl(storeSeed, seed, i == 3 ? 1 : Math.Min(i + 1, 3));
            if (PhotoCatalog.TryGetValue($"{storeSeed.AssetFolder}/{seed.Slug}", out var photoEntry)
                && i < photoEntry.VariantImageUrls.Length)
                variant.ImageUrl = photoEntry.VariantImageUrls[i];
            variant.IsDefault = i == 0;
            variant.IsActive = true;
            variant.IsDeleted = false;
            variant.SortOrder = i + 1;
        }
    }

    private static async Task EnsureStorefrontTranslationsAsync(
        AppDbContext context,
        Guid storeId,
        StoreSeed seed,
        IReadOnlyDictionary<string, Category> categories,
        IReadOnlyDictionary<string, Section> sections)
    {
        if (!string.Equals(seed.Slug, "alsujood-cosmetics", StringComparison.OrdinalIgnoreCase))
            return;

        var products = await context.Products
            .IgnoreQueryFilters()
            .Include(x => x.Variants)
            .Where(x => x.StoreId == storeId)
            .ToListAsync();

        var translations = await context.StoreTranslations
            .IgnoreQueryFilters()
            .Where(x => x.StoreId == storeId)
            .ToListAsync();

        void Upsert(Guid entityId, string entityType, string language, string fieldName, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            var translation = translations.FirstOrDefault(x =>
                x.EntityId == entityId &&
                x.EntityType == entityType &&
                x.Language == language &&
                x.FieldName == fieldName);

            if (translation == null)
            {
                translation = new StoreTranslation
                {
                    StoreId = storeId,
                    EntityId = entityId,
                    EntityType = entityType,
                    Language = language,
                    FieldName = fieldName
                };

                translations.Add(translation);
                context.StoreTranslations.Add(translation);
            }

            translation.Value = value.Trim();
            translation.IsDeleted = false;
        }

        Upsert(storeId, StoreTranslationEntityTypes.Store, StoreLanguages.English, "Name", "Al Sujood Cosmetics");
        Upsert(storeId, StoreTranslationEntityTypes.Store, StoreLanguages.English, "BusinessType", "Cosmetics");
        Upsert(storeId, StoreTranslationEntityTypes.Store, StoreLanguages.English, "Description", "An elegant burgundy cosmetics store that brings makeup, skincare, and fragrance together with organized browsing and clear product images.");
        Upsert(storeId, StoreTranslationEntityTypes.Store, StoreLanguages.English, "StoreStory", "Al Sujood Cosmetics brings everyday beauty products in a rich burgundy style: lip shades, complexion essentials, skincare, and elegant scents in a smooth shopping experience.");
        Upsert(storeId, StoreTranslationEntityTypes.Store, StoreLanguages.Hebrew, "Name", "אל סג'וד קוסמטיקס");
        Upsert(storeId, StoreTranslationEntityTypes.Store, StoreLanguages.Hebrew, "BusinessType", "קוסמטיקה");
        Upsert(storeId, StoreTranslationEntityTypes.Store, StoreLanguages.Hebrew, "Description", "חנות קוסמטיקה אלגנטית בגווני בורדו שמחברת איפור, טיפוח ובישום עם דפדוף מסודר ותמונות מוצר ברורות.");
        Upsert(storeId, StoreTranslationEntityTypes.Store, StoreLanguages.Hebrew, "StoreStory", "אל סג'וד קוסמטיקס מציעה מוצרי יופי יומיומיים בסגנון בורדו יוקרתי: גווני שפתיים, מוצרי בסיס, טיפוח עור וניחוחות אלגנטיים בחוויית קנייה נוחה.");

        var categoryTranslations = CosmeticCategoryTranslations();
        foreach (var categorySeed in seed.Categories)
        {
            if (!categoryTranslations.TryGetValue(categorySeed.Key, out var translation))
                continue;

            if (categories.TryGetValue(categorySeed.Key, out var category))
            {
                Upsert(category.Id, StoreTranslationEntityTypes.Category, StoreLanguages.English, "Name", translation.EnglishName);
                Upsert(category.Id, StoreTranslationEntityTypes.Category, StoreLanguages.English, "Description", translation.EnglishDescription);
                Upsert(category.Id, StoreTranslationEntityTypes.Category, StoreLanguages.Hebrew, "Name", translation.HebrewName);
                Upsert(category.Id, StoreTranslationEntityTypes.Category, StoreLanguages.Hebrew, "Description", translation.HebrewDescription);
            }

            if (sections.TryGetValue(categorySeed.Key, out var section))
            {
                Upsert(section.Id, StoreTranslationEntityTypes.Section, StoreLanguages.English, "Name", translation.EnglishName);
                Upsert(section.Id, StoreTranslationEntityTypes.Section, StoreLanguages.English, "Description", translation.EnglishDescription);
                Upsert(section.Id, StoreTranslationEntityTypes.Section, StoreLanguages.Hebrew, "Name", translation.HebrewName);
                Upsert(section.Id, StoreTranslationEntityTypes.Section, StoreLanguages.Hebrew, "Description", translation.HebrewDescription);
            }
        }

        var productTranslations = CosmeticProductTranslations();
        foreach (var productSeed in seed.Products)
        {
            if (!productTranslations.TryGetValue(productSeed.Slug, out var translation))
                continue;

            var product = products.FirstOrDefault(x =>
                string.Equals(x.Slug, ProductSlug(seed, productSeed.Slug), StringComparison.OrdinalIgnoreCase));
            if (product == null)
                continue;

            Upsert(product.Id, StoreTranslationEntityTypes.Product, StoreLanguages.English, "Name", translation.EnglishName);
            Upsert(product.Id, StoreTranslationEntityTypes.Product, StoreLanguages.English, "ShortDescription", translation.EnglishShortDescription);
            Upsert(product.Id, StoreTranslationEntityTypes.Product, StoreLanguages.English, "Description", translation.EnglishDescription);
            Upsert(product.Id, StoreTranslationEntityTypes.Product, StoreLanguages.Hebrew, "Name", translation.HebrewName);
            Upsert(product.Id, StoreTranslationEntityTypes.Product, StoreLanguages.Hebrew, "ShortDescription", translation.HebrewShortDescription);
            Upsert(product.Id, StoreTranslationEntityTypes.Product, StoreLanguages.Hebrew, "Description", translation.HebrewDescription);

            for (var i = 0; i < productSeed.Variants.Length; i++)
            {
                var sku = $"{seed.SkuPrefix}-{(Array.IndexOf(seed.Products, productSeed) + 1):000}-V{i + 1}";
                var variant = product.Variants.FirstOrDefault(x => x.SKU == sku);
                if (variant == null)
                    continue;

                var englishVariant = TranslateCosmeticVariant(productSeed.Variants[i].Name, StoreLanguages.English);
                var hebrewVariant = TranslateCosmeticVariant(productSeed.Variants[i].Name, StoreLanguages.Hebrew);
                Upsert(variant.Id, StoreTranslationEntityTypes.ProductVariant, StoreLanguages.English, "Name", $"{translation.EnglishName} - {englishVariant}");
                Upsert(variant.Id, StoreTranslationEntityTypes.ProductVariant, StoreLanguages.English, "Description", $"{englishVariant} option from Al Sujood Cosmetics.");
                Upsert(variant.Id, StoreTranslationEntityTypes.ProductVariant, StoreLanguages.Hebrew, "Name", $"{translation.HebrewName} - {hebrewVariant}");
                Upsert(variant.Id, StoreTranslationEntityTypes.ProductVariant, StoreLanguages.Hebrew, "Description", $"אפשרות {hebrewVariant} של אל סג'וד קוסמטיקס.");
            }
        }
    }

    private static Dictionary<string, CategoryTranslationSeed> CosmeticCategoryTranslations() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["lips"] = new("Lips", "Lipstick, gloss, tint, and balm in clear shades.", "שפתיים", "שפתון, גלוס, טינט ובאלם בגוונים ברורים."),
        ["face"] = new("Face Makeup", "Foundation, powder, blush, highlighter, and application tools.", "איפור פנים", "מייקאפ, פודרה, סומק, היילייטר וכלי איפור."),
        ["eyes"] = new("Eyes & Brows", "Mascara, eyeliner, eyeshadow, and brow products.", "עיניים וגבות", "מסקרה, אייליינר, צלליות ומוצרי גבות."),
        ["skincare"] = new("Skincare", "A gentle routine for hydration, glow, and daily protection.", "טיפוח עור", "שגרת טיפוח עדינה ללחות, זוהר והגנה יומיומית."),
        ["fragrance"] = new("Fragrance & Finishing Touches", "Elegant scents and products that complete the look.", "בשמים ונגיעות סיום", "ניחוחות אלגנטיים ומוצרים שמשלימים את המראה.")
    };

    private static Dictionary<string, ProductTranslationSeed> CosmeticProductTranslations() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["velvet-burgundy-lipstick"] = new("Velvet Burgundy Lipstick", "Velvety, long-wearing burgundy color.", "A comfortable matte lipstick with rich coverage and a polished finish for outings and occasions.", "שפתון בורדו קטיפתי", "גוון בורדו קטיפתי ועמיד.", "שפתון מאט נוח עם כיסוי עשיר וגימור מוקפד ליציאות ולאירועים."),
        ["soft-matte-lip-cream"] = new("Soft Matte Lip Cream", "Light texture in everyday shades.", "A long-wearing lip cream that feels soft, not dry, with natural daily shades.", "קרם שפתיים מאט רך", "מרקם קל בגוונים יומיומיים.", "קרם שפתיים עמיד שמרגיש רך ולא מייבש, בגוונים טבעיים ליומיום."),
        ["flawless-liquid-foundation"] = new("Flawless Liquid Foundation", "Smooth, natural coverage.", "A satin-finish liquid foundation that evens the complexion and stays comfortable for hours.", "מייקאפ נוזלי Flawless", "כיסוי חלק וטבעי.", "מייקאפ נוזלי בגימור סאטן שמאחד את גוון העור ונשאר נוח לאורך שעות."),
        ["silky-pressed-powder"] = new("Silky Pressed Powder", "Soft setting without caking.", "A lightweight powder that sets makeup, reduces shine, and leaves a silky skin feel.", "פודרה דחוסה Silky", "קיבוע רך בלי מראה כבד.", "פודרה קלילה שמקבעת איפור, מפחיתה ברק ומשאירה תחושה משיית על העור."),
        ["rose-blush-compact"] = new("Rose Compact Blush", "Natural rosy cheek color.", "A buildable blush that gives the face a fresh everyday glow.", "סומק Rose Compact", "צבע לחיים ורוד טבעי.", "סומק ניתן לבנייה שמעניק לפנים מראה רענן ומתאים לכל יום."),
        ["golden-burgundy-eyeshadow"] = new("Burgundy Gold Eyeshadow Palette", "Burgundy and gold shades.", "An eyeshadow palette with matte and shimmer shades for daily looks or elegant evenings.", "פלטת צלליות Burgundy Gold", "גווני בורדו וזהב.", "פלטת צלליות בגימור מאט ומבריק למראה יומי או לערב אלגנטי."),
        ["volume-lash-mascara"] = new("Volume Lash Mascara", "Fuller, more defined lashes.", "A black mascara with a separating brush that builds volume without clumps.", "מסקרה Volume Lash", "ריסים מלאים ומודגשים יותר.", "מסקרה שחורה עם מברשת מפרידה שמעניקה נפח בלי גושים."),
        ["gentle-gel-cleanser"] = new("Gentle Gel Cleanser", "Daily cleansing without dryness.", "A gentle gel cleanser that removes impurities while keeping skin comfortable and clean.", "ג'ל ניקוי עדין", "ניקוי יומי בלי יובש.", "ג'ל ניקוי עדין שמסיר לכלוך ושומר על תחושת עור נקייה ונוחה."),
        ["hyaluronic-glow-serum"] = new("Hyaluronic Glow Serum", "Hydration and healthy glow.", "A lightweight hyaluronic acid serum that helps skin look plump and smooth.", "סרום Hyaluronic Glow", "לחות וזוהר בריא.", "סרום קליל עם חומצה היאלורונית שעוזר לעור להיראות מלא וחלק."),
        ["daily-hydra-moisturizer"] = new("Daily Hydra Moisturizer", "Light daily hydration.", "A fast-absorbing moisturizing cream for use before makeup or after cleansing.", "קרם לחות Daily Hydra", "לחות יומיומית קלילה.", "קרם לחות שנספג מהר ומתאים לפני איפור או אחרי ניקוי."),
        ["spf-50-sun-veil"] = new("SPF 50 Sun Veil", "Daily protection with a soft feel.", "A lightweight sunscreen that avoids a heavy layer and works well under makeup.", "קרם הגנה SPF 50", "הגנה יומיומית במרקם רך.", "קרם הגנה קליל שלא משאיר שכבה כבדה ומתאים מתחת לאיפור."),
        ["oud-rose-mist"] = new("Oud & Rose Mist", "Warm rosy scent.", "An elegant body mist with soft rose and oud notes for daily use or your bag.", "מיסט ורד ועוּד", "ניחוח ורדים חמים.", "בודי מיסט אלגנטי עם נגיעות ורד ועוּד לשימוש יומי או לנשיאה בתיק."),
        ["glass-shine-lip-gloss"] = new("Glass Shine Lip Gloss", "Glossy, non-sticky shine.", "A soft clear shine that makes lips look fuller over lipstick or on its own.", "גלוס ברק זכוכית", "ברק מבריק ולא דביק.", "ברק שקוף ורך שמעניק לשפתיים מראה מלא מעל שפתון או לבד."),
        ["cherry-lip-tint"] = new("Cherry Lip Tint", "Light, long-wearing color.", "A cherry-toned lip and cheek tint for workdays and quick outings.", "טינט שפתיים Cherry", "צבע קליל ועמיד.", "טינט שפתיים ולחיים בגוון דובדבן טבעי לעבודה וליציאות קצרות."),
        ["nourish-lip-balm"] = new("Nourish Lip Balm", "Daily lip hydration.", "A nourishing lip balm that softens lips before lipstick or throughout the day.", "באלם שפתיים Nourish", "לחות יומיומית לשפתיים.", "באלם מזין שמרכך את השפתיים לפני שפתון או במהלך היום."),
        ["precision-lip-liner"] = new("Precision Lip Liner", "Clean, long-wearing definition.", "A lip pencil that helps shape the edges and keep lipstick color in place longer.", "תוחם שפתיים Precision", "תיחום נקי ועמיד.", "עיפרון שפתיים שמסייע לעצב את הקו ולשמור על צבע השפתון לאורך זמן."),
        ["smooth-liquid-concealer"] = new("Smooth Liquid Concealer", "Coverage for dark circles and spots.", "A lightweight liquid concealer that blends easily and gives neat coverage without heaviness.", "קונסילר Smooth", "כיסוי לכהויות וכתמים.", "קונסילר נוזלי קליל שמיטשטש בקלות ומעניק כיסוי מסודר בלי כבדות."),
        ["soft-contour-stick"] = new("Soft Contour Stick", "Natural face definition.", "A creamy contour stick that blends easily on the nose, cheeks, and jawline.", "סטיק קונטור רך", "הדגשת פנים טבעית.", "סטיק קונטור קרמי שנמרח בקלות על האף, הלחיים וקו הלסת."),
        ["pearl-glow-highlighter"] = new("Pearl Glow Highlighter", "Soft cheek glow.", "A pressed highlighter that adds gentle light without an overdone shimmer.", "היילייטר Pearl Glow", "זוהר עדין ללחיים.", "היילייטר דחוס שמוסיף הארה רכה בלי ברק מוגזם."),
        ["pro-makeup-brush-set"] = new("Makeup Pro Brush Set", "A full set for application and blending.", "A soft brush set for foundation, powder, blush, and eyeshadow.", "סט מברשות Makeup Pro", "סט מלא להנחה וטשטוש.", "סט מברשות רכות למייקאפ, פודרה, סומק וצלליות."),
        ["dewy-setting-spray"] = new("Dewy Setting Spray", "Set makeup with a healthy glow.", "A setting spray that helps makeup stay neat and adds a light fresh finish.", "ספריי קיבוע Dewy", "קיבוע איפור עם זוהר בריא.", "ספריי קיבוע שעוזר לאיפור להישאר מסודר ומוסיף גימור רענן ועדין."),
        ["black-precision-eyeliner"] = new("Precision Black Eyeliner", "Clear, easy black line.", "A liquid eyeliner with a fine tip for a steady line from the first stroke.", "אייליינר שחור Precision", "קו שחור ברור וקל.", "אייליינר נוזלי עם קצה מדויק לציור קו יציב כבר מהמריחה הראשונה."),
        ["brow-define-pencil"] = new("Brow Define Pencil", "Natural brow filling.", "A brow pencil in natural shades with a fine tip for a neat, soft shape.", "עיפרון גבות Brow Define", "מילוי גבות טבעי.", "עיפרון גבות בגוונים טבעיים עם קצה דק למראה מסודר ורך."),
        ["neutral-everyday-eyeshadow"] = new("Neutral Daily Eyeshadow Palette", "Easy-blending everyday shades.", "An eyeshadow palette with nude, brown, and rosy shades for daily makeup.", "פלטת צלליות Neutral Daily", "גוונים יומיומיים שקל לטשטש.", "פלטת צלליות בגווני ניוד, חום וורוד לאיפור יומי."),
        ["clear-brow-gel"] = new("Clear Hold Brow Gel", "Colorless brow hold.", "A clear gel that combs and holds brows naturally throughout the day.", "ג'ל גבות שקוף Clear Hold", "קיבוע גבות ללא צבע.", "ג'ל שקוף שמסרק ומקבע את הגבות בצורה טבעית לאורך היום."),
        ["vitamin-c-bright-serum"] = new("Vitamin C Bright Serum", "Glow and a more even look.", "A lightweight vitamin C serum that supports a brighter, more even-looking complexion.", "סרום Vitamin C Bright", "זוהר ומראה אחיד יותר.", "סרום ויטמין C קליל שתומך במראה עור בהיר ואחיד יותר."),
        ["retinol-night-serum"] = new("Retinol Night Serum", "A gentle night routine.", "A night serum made to care for the appearance of texture and fine lines.", "סרום לילה Retinol", "שגרת לילה עדינה.", "סרום לילה שמיועד לטיפוח מראה המרקם והקמטוטים."),
        ["balance-rose-toner"] = new("Balance Rose Toner", "Refresh and hydrate after cleansing.", "A gentle toner that refreshes skin after cleansing and prepares it for serum and moisturizer.", "טונר Balance Rose", "רענון ולחות אחרי ניקוי.", "טונר עדין שמרענן את העור אחרי ניקוי ומכין אותו לסרום ולקרם לחות."),
        ["micellar-cleansing-water"] = new("Micellar Cleansing Water", "Gentle makeup removal.", "Micellar water for removing makeup and impurities without harsh rubbing.", "מי מיסלר לניקוי", "הסרת איפור עדינה.", "מי מיסלר להסרת איפור ולכלוך בלי שפשוף חזק."),
        ["clay-pore-mask"] = new("Clay Pore Mask", "Deep pore cleansing.", "A clay mask that helps clarify the skin and reduce excess shine.", "מסכת חימר Clay Pore", "ניקוי עמוק לנקבוביות.", "מסכת חימר שעוזרת לטהר את העור ולהפחית ברק עודף."),
        ["floral-body-mist"] = new("Floral Body Mist", "Light floral scent.", "A daily body mist with soft floral notes for after showering or before going out.", "בודי מיסט Floral", "ניחוח פרחוני קל.", "בודי מיסט יומי עם ניחוח פרחוני עדין אחרי מקלחת או לפני יציאה."),
        ["pocket-perfume-oil"] = new("Pocket Perfume Oil", "Concentrated fragrance for your bag.", "A small perfume oil made for quick touch-ups during outings.", "שמן בושם Pocket", "בושם מרוכז לתיק.", "שמן בושם קטן שמתאים לרענון מהיר במהלך היום.")
    };

    private static string TranslateCosmeticVariant(string value, string language)
    {
        var normalized = value.Trim();
        var translations = language == StoreLanguages.Hebrew
            ? HebrewCosmeticVariantTranslations()
            : EnglishCosmeticVariantTranslations();

        return translations.TryGetValue(normalized, out var translation)
            ? translation
            : normalized;
    }

    private static Dictionary<string, string> EnglishCosmeticVariantTranslations() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["برغندي"] = "Burgundy",
        ["توتي"] = "Berry",
        ["وردي غامق"] = "Deep Pink",
        ["نود وردي"] = "Rosy Nude",
        ["موف"] = "Mauve",
        ["كرز"] = "Cherry",
        ["فاتح"] = "Light",
        ["متوسط"] = "Medium",
        ["قمحي"] = "Wheat",
        ["شفاف"] = "Clear",
        ["بيج"] = "Beige",
        ["دافئ"] = "Warm",
        ["وردي"] = "Pink",
        ["خوخي"] = "Peach",
        ["ذهبي"] = "Gold",
        ["بني"] = "Brown",
        ["أسود"] = "Black",
        ["أسود مقاوم للماء"] = "Waterproof Black",
        ["عادي"] = "Normal",
        ["مختلط"] = "Combination",
        ["حساس"] = "Sensitive",
        ["مجموعة"] = "Set",
        ["خفيف"] = "Light",
        ["غني"] = "Rich",
        ["ليلي"] = "Night",
        ["ورد"] = "Rose",
        ["عود"] = "Oud",
        ["مسك"] = "Musk",
        ["توت"] = "Berry",
        ["روز"] = "Rose",
        ["فانيلا"] = "Vanilla",
        ["نود"] = "Nude",
        ["بني وردي"] = "Rosy Brown",
        ["مقاوم للماء"] = "Waterproof"
    };

    private static Dictionary<string, string> HebrewCosmeticVariantTranslations() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["برغندي"] = "בורדו",
        ["توتي"] = "פירות יער",
        ["وردي غامق"] = "ורוד כהה",
        ["نود وردي"] = "ניוד ורדרד",
        ["موف"] = "מוֹב",
        ["كرز"] = "דובדבן",
        ["فاتح"] = "בהיר",
        ["متوسط"] = "בינוני",
        ["قمحي"] = "חיטה",
        ["شفاف"] = "שקוף",
        ["بيج"] = "בז'",
        ["دافئ"] = "חמים",
        ["وردي"] = "ורוד",
        ["خوخي"] = "אפרסק",
        ["ذهبي"] = "זהב",
        ["بني"] = "חום",
        ["أسود"] = "שחור",
        ["أسود مقاوم للماء"] = "שחור עמיד במים",
        ["عادي"] = "רגיל",
        ["مختلط"] = "מעורב",
        ["حساس"] = "רגיש",
        ["مجموعة"] = "סט",
        ["خفيف"] = "קליל",
        ["غني"] = "עשיר",
        ["ليلي"] = "לילה",
        ["ورد"] = "ורד",
        ["عود"] = "עוּד",
        ["مسك"] = "מאסק",
        ["توت"] = "פירות יער",
        ["روز"] = "ורד",
        ["فانيلا"] = "וניל",
        ["نود"] = "ניוד",
        ["بني وردي"] = "חום ורדרד",
        ["مقاوم للماء"] = "עמיד במים",
        ["Light"] = "בהיר",
        ["Medium"] = "בינוני",
        ["Warm"] = "חמים",
        ["Deep"] = "עמוק",
        ["Pearl"] = "פנינה",
        ["Champagne"] = "שמפניה",
        ["Rose Gold"] = "רוז גולד",
        ["Basic"] = "בסיסי",
        ["Complete"] = "מלא",
        ["Travel"] = "נסיעות",
        ["Dewy"] = "זוהר לח",
        ["Matte"] = "מאט",
        ["Long Wear"] = "עמיד לאורך זמן",
        ["Light Brown"] = "חום בהיר",
        ["Dark Brown"] = "חום כהה",
        ["Graphite"] = "גרפיט",
        ["Nude"] = "ניוד",
        ["Smoky"] = "מעושן",
        ["Clear"] = "שקוף",
        ["Brown"] = "חום",
        ["Strong Hold"] = "קיבוע חזק",
        ["Gentle"] = "עדין",
        ["Regular"] = "רגיל",
        ["Advanced"] = "מתקדם",
        ["Rose"] = "ורד",
        ["Hydra"] = "לחות",
        ["Calm"] = "מרגיע",
        ["Sensitive"] = "רגיש",
        ["Hydrating"] = "מלחח",
        ["Oil Control"] = "איזון שומן",
        ["Clay"] = "חימר",
        ["Rose Clay"] = "חימר ורדים",
        ["Charcoal"] = "פחם",
        ["Floral"] = "פרחוני",
        ["Vanilla"] = "וניל",
        ["Musk"] = "מאסק",
        ["Rose Oud"] = "ורד ועוּד",
        ["Amber"] = "ענבר",
        ["Tinted"] = "עם גוון"
    };

    private static void EnsureImageAssets(string webRootPath, StoreSeed seed)
    {
        if (string.IsNullOrWhiteSpace(webRootPath))
            return;

        var imageDirectory = Path.Combine(webRootPath, "seed-images", seed.AssetFolder);
        Directory.CreateDirectory(imageDirectory);

        WriteAsset(imageDirectory, "store-logo", BuildLogoSvg(seed));
        WriteAsset(imageDirectory, "store-cover", BuildCollectionSvg(seed, seed.Name, seed.CoverSubtitle, seed.AccentA, seed.AccentB));

        foreach (var category in seed.Categories)
        {
            WriteAsset(
                imageDirectory,
                $"category-{category.Key}",
                BuildCollectionSvg(seed, category.Name, category.Description, category.ColorA, category.ColorB));
        }

        foreach (var product in seed.Products)
        {
            for (var i = 1; i <= 3; i++)
            {
                WriteAsset(
                    imageDirectory,
                    $"{product.Slug}-{i}",
                    product.Kind switch
                    {
                        ProductKind.Clothing => BuildClothingSvg(seed, product, i),
                        ProductKind.Cup => BuildCupSvg(seed, product, i),
                        ProductKind.Cosmetic => BuildCosmeticSvg(seed, product, i),
                        _ => BuildCollectionSvg(seed, product.Name, product.ShortDescription, seed.AccentA, seed.AccentB)
                    });
            }
        }
    }

    private static void WriteAsset(string imageDirectory, string name, string svg)
    {
        File.WriteAllText(Path.Combine(imageDirectory, $"{Slug(name)}.svg"), svg);
    }

    private static string BuildLogoSvg(StoreSeed seed)
    {
        var title = Encode(seed.Name);
        var letter = Encode(seed.LogoLetter);

        return $$"""
<svg xmlns="http://www.w3.org/2000/svg" width="900" height="700" viewBox="0 0 900 700" role="img" aria-label="{{title}}">
  <defs>
    <linearGradient id="bg" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0%" stop-color="{{seed.AccentA}}"/>
      <stop offset="100%" stop-color="{{seed.AccentB}}"/>
    </linearGradient>
    <filter id="shadow"><feDropShadow dx="0" dy="22" stdDeviation="24" flood-color="#111827" flood-opacity="0.24"/></filter>
  </defs>
  <rect width="900" height="700" rx="38" fill="url(#bg)"/>
  <circle cx="450" cy="286" r="154" fill="#ffffff" opacity="0.94" filter="url(#shadow)"/>
  <text x="450" y="335" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="142" font-weight="900" fill="{{seed.AccentA}}">{{letter}}</text>
  <text x="450" y="560" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="48" font-weight="800" fill="#ffffff">{{title}}</text>
</svg>
""";
    }

    private static string BuildCollectionSvg(StoreSeed seed, string title, string subtitle, string colorA, string colorB)
    {
        var safeTitle = Encode(title);
        var safeSubtitle = Encode(subtitle);

        return $$"""
<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="900" viewBox="0 0 1200 900" role="img" aria-label="{{safeTitle}}">
  <defs>
    <linearGradient id="bg" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0%" stop-color="{{colorA}}"/>
      <stop offset="100%" stop-color="{{colorB}}"/>
    </linearGradient>
    <filter id="shadow"><feDropShadow dx="0" dy="24" stdDeviation="28" flood-color="#111827" flood-opacity="0.26"/></filter>
  </defs>
  <rect width="1200" height="900" rx="42" fill="url(#bg)"/>
  <rect x="88" y="86" width="1024" height="620" rx="38" fill="#ffffff" opacity="0.14"/>
  {{BuildCollectionArt(seed)}}
  <text x="600" y="775" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="58" font-weight="900" fill="#ffffff">{{safeTitle}}</text>
  <text x="600" y="830" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="30" font-weight="700" fill="#f8fafc">{{safeSubtitle}}</text>
</svg>
""";
    }

    private static string BuildCollectionArt(StoreSeed seed)
    {
        if (seed.AssetFolder.Contains("cosmetic", StringComparison.OrdinalIgnoreCase))
        {
            return """
  <g filter="url(#shadow)">
    <rect x="322" y="278" width="116" height="334" rx="40" fill="#ffffff"/>
    <rect x="344" y="206" width="72" height="94" rx="22" fill="#f7d8df"/>
    <path d="M364 206c22-80 44-80 66 0" fill="#9f1239"/>
    <rect x="520" y="190" width="172" height="430" rx="54" fill="#ffffff"/>
    <rect x="556" y="132" width="100" height="82" rx="28" fill="#f7d8df"/>
    <rect x="562" y="334" width="88" height="42" rx="21" fill="#9f1239" opacity="0.82"/>
    <circle cx="820" cy="462" r="126" fill="#ffffff"/>
    <circle cx="820" cy="462" r="72" fill="#f7d8df"/>
    <path d="M760 462h120" stroke="#9f1239" stroke-width="24" stroke-linecap="round" opacity="0.7"/>
  </g>
""";
        }

        if (seed.AssetFolder.Contains("cup", StringComparison.OrdinalIgnoreCase))
        {
            return """
  <g filter="url(#shadow)">
    <path d="M402 282h300v230c0 86-70 156-156 156s-144-70-144-156z" fill="#ffffff"/>
    <path d="M702 344h62c58 0 96 38 96 88s-38 88-96 88h-62v-56h56c24 0 42-14 42-32s-18-32-42-32h-56z" fill="#fef3c7"/>
    <rect x="438" y="324" width="226" height="42" rx="21" fill="#0f172a" opacity="0.16"/>
    <path d="M456 236c-28-54 64-58 34-118M556 236c-30-54 62-62 30-122M650 236c-26-50 56-58 30-108" stroke="#ffffff" stroke-width="22" stroke-linecap="round" opacity="0.72"/>
  </g>
""";
        }

        return """
  <g filter="url(#shadow)">
    <path d="M382 248 484 196h232l102 52-56 118-76-34v304H414V332l-76 34z" fill="#ffffff"/>
    <path d="M494 204c14 60 62 92 106 92s92-32 106-92" fill="#e2e8f0"/>
    <rect x="456" y="398" width="288" height="42" rx="21" fill="#0f172a" opacity="0.13"/>
    <rect x="500" y="480" width="200" height="34" rx="17" fill="#0f172a" opacity="0.10"/>
  </g>
""";
    }

    private static string BuildClothingSvg(StoreSeed storeSeed, ProductSeed product, int index)
    {
        var safeTitle = Encode(product.Name);
        var palette = product.Palettes[(index - 1) % product.Palettes.Length];

        return $$"""
<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="900" viewBox="0 0 1200 900" role="img" aria-label="{{safeTitle}}">
  <defs>
    <linearGradient id="bg" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0%" stop-color="{{palette.BackgroundA}}"/>
      <stop offset="100%" stop-color="{{palette.BackgroundB}}"/>
    </linearGradient>
    <filter id="shadow"><feDropShadow dx="0" dy="28" stdDeviation="30" flood-color="#111827" flood-opacity="0.28"/></filter>
  </defs>
  <rect width="1200" height="900" rx="42" fill="url(#bg)"/>
  <rect x="86" y="78" width="1028" height="650" rx="40" fill="#ffffff" opacity="0.13"/>
  <g filter="url(#shadow)">
    {{ClothingArt(product.Shape, palette)}}
  </g>
  <text x="600" y="792" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="52" font-weight="900" fill="#ffffff">{{safeTitle}}</text>
  <text x="600" y="842" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="28" font-weight="700" fill="#f8fafc">{{Encode(palette.Label)}}</text>
</svg>
""";
    }

    private static string ClothingArt(string shape, Palette palette)
    {
        return shape switch
        {
            "dress" => $$"""
    <path d="M494 166h212l52 150-68 32 120 300H390l120-300-68-32z" fill="{{palette.Main}}" stroke="#ffffff" stroke-width="14"/>
    <path d="M520 166c16 54 52 82 80 82s64-28 80-82" fill="{{palette.Secondary}}" opacity="0.78"/>
    <path d="M486 500h228" stroke="{{palette.Secondary}}" stroke-width="24" stroke-linecap="round" opacity="0.55"/>
""",
            "pants" => $$"""
    <path d="M434 146h332l-36 514H614l-14-298-18 298H466z" fill="{{palette.Main}}" stroke="#ffffff" stroke-width="14"/>
    <path d="M452 214h296" stroke="{{palette.Secondary}}" stroke-width="32" stroke-linecap="round" opacity="0.56"/>
    <path d="M600 202v452" stroke="#ffffff" stroke-width="12" opacity="0.45"/>
""",
            _ => $$"""
    <path d="M382 218 492 158h216l110 60-62 130-70-34v326H514V314l-70 34z" fill="{{palette.Main}}" stroke="#ffffff" stroke-width="14"/>
    <path d="M502 166c18 62 58 94 98 94s80-32 98-94" fill="{{palette.Secondary}}" opacity="0.72"/>
    <rect x="532" y="418" width="136" height="34" rx="17" fill="{{palette.Secondary}}" opacity="0.62"/>
"""
        };
    }

    private static string BuildCupSvg(StoreSeed storeSeed, ProductSeed product, int index)
    {
        var safeTitle = Encode(product.Name);
        var palette = product.Palettes[(index - 1) % product.Palettes.Length];

        return $$"""
<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="900" viewBox="0 0 1200 900" role="img" aria-label="{{safeTitle}}">
  <defs>
    <linearGradient id="bg" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0%" stop-color="{{palette.BackgroundA}}"/>
      <stop offset="100%" stop-color="{{palette.BackgroundB}}"/>
    </linearGradient>
    <filter id="shadow"><feDropShadow dx="0" dy="28" stdDeviation="30" flood-color="#111827" flood-opacity="0.28"/></filter>
  </defs>
  <rect width="1200" height="900" rx="42" fill="url(#bg)"/>
  <rect x="84" y="80" width="1032" height="650" rx="42" fill="#ffffff" opacity="0.14"/>
  <g filter="url(#shadow)">
    {{CupArt(product.Shape, palette)}}
  </g>
  <text x="600" y="792" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="52" font-weight="900" fill="#ffffff">{{safeTitle}}</text>
  <text x="600" y="842" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="28" font-weight="700" fill="#f8fafc">{{Encode(palette.Label)}}</text>
</svg>
""";
    }

    private static string CupArt(string shape, Palette palette)
    {
        return shape switch
        {
            "tumbler" => $$"""
    <path d="M438 176h324l-46 500H484z" fill="{{palette.Main}}" stroke="#ffffff" stroke-width="14"/>
    <rect x="408" y="130" width="384" height="64" rx="30" fill="{{palette.Secondary}}"/>
    <path d="M506 294h188" stroke="#ffffff" stroke-width="26" stroke-linecap="round" opacity="0.58"/>
    <path d="M532 362h136" stroke="#ffffff" stroke-width="20" stroke-linecap="round" opacity="0.35"/>
""",
            "glass" => $$"""
    <path d="M442 166h316l-44 500H486z" fill="{{palette.Main}}" stroke="#ffffff" stroke-width="14" opacity="0.82"/>
    <path d="M482 282h236l-26 292H508z" fill="#ffffff" opacity="0.22"/>
    <path d="M520 388c70-42 104 42 166 0" stroke="{{palette.Secondary}}" stroke-width="30" stroke-linecap="round" opacity="0.8"/>
""",
            _ => $$"""
    <path d="M430 234h310v284c0 86-70 156-156 156s-154-70-154-156z" fill="{{palette.Main}}" stroke="#ffffff" stroke-width="14"/>
    <path d="M738 320h72c62 0 104 42 104 98s-42 98-104 98h-72v-62h62c28 0 48-16 48-36s-20-36-48-36h-62z" fill="{{palette.Secondary}}" stroke="#ffffff" stroke-width="10"/>
    <rect x="482" y="336" width="202" height="40" rx="20" fill="#ffffff" opacity="0.36"/>
    <path d="M522 190c-26-50 56-56 28-110M610 190c-28-50 58-60 28-112" stroke="#ffffff" stroke-width="22" stroke-linecap="round" opacity="0.65"/>
"""
        };
    }

    private static string BuildCosmeticSvg(StoreSeed storeSeed, ProductSeed product, int index)
    {
        var safeTitle = Encode(product.Name);
        var palette = product.Palettes[(index - 1) % product.Palettes.Length];

        return $$"""
<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="900" viewBox="0 0 1200 900" role="img" aria-label="{{safeTitle}}">
  <defs>
    <linearGradient id="bg" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0%" stop-color="{{palette.BackgroundA}}"/>
      <stop offset="100%" stop-color="{{palette.BackgroundB}}"/>
    </linearGradient>
    <filter id="shadow"><feDropShadow dx="0" dy="28" stdDeviation="30" flood-color="#111827" flood-opacity="0.26"/></filter>
  </defs>
  <rect width="1200" height="900" rx="42" fill="url(#bg)"/>
  <rect x="84" y="80" width="1032" height="650" rx="42" fill="#ffffff" opacity="0.15"/>
  <g filter="url(#shadow)">
    {{CosmeticArt(product.Shape, palette)}}
  </g>
  <text x="600" y="792" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="52" font-weight="900" fill="#ffffff">{{safeTitle}}</text>
  <text x="600" y="842" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="28" font-weight="700" fill="#fff1f2">{{Encode(palette.Label)}}</text>
</svg>
""";
    }

    private static string CosmeticArt(string shape, Palette palette)
    {
        return shape switch
        {
            "lipstick" => $$"""
    <rect x="482" y="360" width="236" height="280" rx="38" fill="{{palette.Main}}" stroke="#ffffff" stroke-width="14"/>
    <rect x="516" y="286" width="168" height="112" rx="30" fill="{{palette.Secondary}}"/>
    <path d="M542 286c38-134 78-134 116 0z" fill="{{palette.Main}}" stroke="#ffffff" stroke-width="12"/>
    <rect x="532" y="460" width="136" height="38" rx="19" fill="#ffffff" opacity="0.34"/>
""",
            "compact" => $$"""
    <circle cx="600" cy="430" r="214" fill="{{palette.Main}}" stroke="#ffffff" stroke-width="16"/>
    <circle cx="600" cy="430" r="132" fill="{{palette.Secondary}}" opacity="0.84"/>
    <path d="M486 430h228" stroke="#ffffff" stroke-width="28" stroke-linecap="round" opacity="0.45"/>
    <circle cx="600" cy="430" r="54" fill="#ffffff" opacity="0.30"/>
""",
            "palette" => $$"""
    <rect x="352" y="250" width="496" height="330" rx="54" fill="{{palette.Main}}" stroke="#ffffff" stroke-width="16"/>
    <circle cx="476" cy="358" r="52" fill="{{palette.Secondary}}"/>
    <circle cx="600" cy="358" r="52" fill="#ffffff" opacity="0.82"/>
    <circle cx="724" cy="358" r="52" fill="#fecdd3"/>
    <rect x="436" y="480" width="328" height="36" rx="18" fill="#ffffff" opacity="0.35"/>
""",
            "jar" => $$"""
    <rect x="432" y="308" width="336" height="320" rx="84" fill="{{palette.Main}}" stroke="#ffffff" stroke-width="16"/>
    <rect x="474" y="232" width="252" height="104" rx="40" fill="{{palette.Secondary}}" stroke="#ffffff" stroke-width="12"/>
    <rect x="508" y="440" width="184" height="48" rx="24" fill="#ffffff" opacity="0.38"/>
""",
            "tube" => $$"""
    <path d="M462 206h276l-54 444H516z" fill="{{palette.Main}}" stroke="#ffffff" stroke-width="16"/>
    <rect x="498" y="136" width="204" height="96" rx="36" fill="{{palette.Secondary}}" stroke="#ffffff" stroke-width="12"/>
    <path d="M522 376h156" stroke="#ffffff" stroke-width="32" stroke-linecap="round" opacity="0.42"/>
""",
            "brush" => $$"""
    <rect x="520" y="286" width="160" height="364" rx="58" fill="{{palette.Main}}" stroke="#ffffff" stroke-width="14"/>
    <path d="M542 220c20-108 96-108 116 0v82H542z" fill="{{palette.Secondary}}" stroke="#ffffff" stroke-width="12"/>
    <rect x="552" y="414" width="96" height="38" rx="19" fill="#ffffff" opacity="0.36"/>
""",
            "perfume" => $$"""
    <rect x="438" y="280" width="324" height="360" rx="70" fill="{{palette.Main}}" stroke="#ffffff" stroke-width="16"/>
    <rect x="530" y="198" width="140" height="104" rx="34" fill="{{palette.Secondary}}" stroke="#ffffff" stroke-width="12"/>
    <rect x="560" y="150" width="80" height="58" rx="22" fill="#ffffff" opacity="0.84"/>
    <circle cx="600" cy="460" r="66" fill="#ffffff" opacity="0.32"/>
""",
            _ => $$"""
    <rect x="486" y="228" width="228" height="416" rx="58" fill="{{palette.Main}}" stroke="#ffffff" stroke-width="16"/>
    <rect x="522" y="154" width="156" height="100" rx="34" fill="{{palette.Secondary}}" stroke="#ffffff" stroke-width="12"/>
    <path d="M552 416h96" stroke="#ffffff" stroke-width="30" stroke-linecap="round" opacity="0.42"/>
    <path d="M600 322c48 56 76 108 76 156 0 52-34 86-76 86s-76-34-76-86c0-48 28-100 76-156z" fill="#ffffff" opacity="0.22"/>
"""
        };
    }

    private static StoreSeed[] Stores() =>
    [
        CosmeticsStore(),
        ClothesStore(),
        CupsStore()
    ];

    private static StoreSeed CosmeticsStore()
    {
        const string coverImage = "https://images.unsplash.com/photo-1596462502278-27bfdc403348?auto=format&fit=crop&w=1600&q=80";
        const string lipstickRed = "https://images.unsplash.com/photo-1645961359170-c01fa306aa6b?auto=format&fit=crop&w=1200&q=80";
        const string lipstickCream = "https://images.unsplash.com/photo-1675081633635-d853cd74b158?auto=format&fit=crop&w=1200&q=80";
        const string lipGloss = "https://res.cloudinary.com/nikolbeauty/image/upload/v1744658602/Lip_Gloss_Room_121_nazqaa.jpg";
        const string foundation = "https://static.wixstatic.com/media/b804c8_096033b9ed5f4f86b04f22c0f66724ce~mv2.jpeg/v1/fill/w_480,h_480,al_c,q_80,enc_avif,quality_auto/b804c8_096033b9ed5f4f86b04f22c0f66724ce~mv2.jpeg";
        const string compactPowder = "https://img.freepik.com/free-photo/close-up-powder-make-up_23-2148978116.jpg";
        const string blushCompact = "https://images.lummi.ai/assets/QmYqyaNXaGZ9bsTrdSFbNDWWqzmkHJ5T2um8oNVEyEAgaw?auto=format&w=1500";
        const string eyeshadow = "https://www.artistkitcompany.com/cdn/shop/files/bde89a201d9f570bb457ed1373458ef7.jpg";
        const string mascara = "https://www.hiamag.com/sites/default/files/article/21-07-2025/21-07-2025_main_mascara_0.jpg";
        const string cleanser = "https://cdn.shopify.com/s/files/1/0534/8667/6910/files/SR_-_November_-_Website_Images.jpg?v=1699136441";
        const string serum = "https://images.squarespace-cdn.com/content/v1/6002f47cdaa64017d8237a9e/1630107055192-34Z7FZPI28OAF3QMV40B/2021-05-17+Hannah+Product-18.jpg";
        const string moisturizer = "https://www.vidguru.in/wp-content/uploads/2024/03/New_Project_25_82c9fd0c-51be-4ea4-b5f9-4fba72f75075-1024x1024.webp";
        const string sunscreen = "https://images.unsplash.com/photo-1728487892215-0d96da2221b7?auto=format&fit=crop&w=1200&q=80";
        const string perfumeClassic = "https://images.unsplash.com/photo-1618437199085-26ee393a26af?auto=format&fit=crop&w=1200&q=80";
        const string perfumeFloral = "https://images.unsplash.com/photo-1707539159801-87009aded4cb?auto=format&fit=crop&w=1200&q=80";
        const string makeupBrushes = "https://images.unsplash.com/photo-1522338242992-e1a54906a8da?auto=format&fit=crop&w=1200&q=80";
        const string settingSpray = "https://manorbeauty.com/cdn/shop/files/MakeupRevolutionSuperfixMistingSpray150ml_2.jpg?v=1716387399&width=1445";
        const string toner = "https://facerealityskincare.com/cdn/shop/files/Moisture-Balance-Toner__052323__FR-Skin-Care__004__1600x1600_f96c4a60-4d98-49cb-859a-73bfe6db3c2d.jpg?v=1684967899&width=1200";
        const string retinol = "https://skinsciences.com/cdn/shop/files/1_1f9669e3-7801-4865-8dbb-08fc8d067d97.jpg?v=1736330174&width=1500";
        const string micellar = "https://cloudinary.images-iherb.com/image/upload/f_auto,q_auto:eco/images/sie/sie47065/l/10.jpg";
        const string bodyMist = "https://cdn11.bigcommerce.com/s-kwtu5j36x0/images/stencil/1280x1280/products/3653/8280/55661__12050.1714328639.jpg?c=1";

        var categories = new[]
        {
            new CategorySeed("lips", "شفاه", "روج، جلوس، تنت، وبلسم بدرجات واضحة.", "#4c0519", "#be123c", lipstickRed),
            new CategorySeed("face", "مكياج الوجه", "فاونديشن، بودرة، بلاشر، هايلايتر، وأدوات تطبيق.", "#881337", "#f43f5e", foundation),
            new CategorySeed("eyes", "عيون وحواجب", "ماسكارا، آيلاينر، ظلال، ومنتجات حواجب.", "#581c87", "#be123c", eyeshadow),
            new CategorySeed("skincare", "عناية بالبشرة", "روتين ناعم للترطيب، النضارة، والحماية اليومية.", "#881337", "#f43f5e", serum),
            new CategorySeed("fragrance", "عطور ولمسات نهائية", "روائح أنيقة ومنتجات تكمل اللوك.", "#6d0f2d", "#d4af37", perfumeClassic)
        };

        return new StoreSeed(
            "alsujood-cosmetics",
            "السجود كوزمتكس",
            "AlSujood",
            "COSMETICS123@GMAIL.COM",
            "AlSujoodCosmetics",
            "Cosmetics",
            "متجر كوزمتكس بثيم برغندي راق، يجمع المكياج والعناية والعطور بواجهة مرتبة وصور واضحة.",
            "السجود كوزمتكس يقدم منتجات جمال يومية بستايل برغندي فاخر: درجات شفاه، أساسات، عناية بالبشرة، وروائح أنيقة بتجربة تسوق ناعمة وسهلة.",
            "اختيارات جمال مختارة بعناية، بدرجات برغندية ووردية ولمسات ذهبية تعطي المتجر حضور مرتب وفخم.",
            "970599556677",
            "alsujood.cosmetics",
            "AlSujoodCosmetics",
            "alsujoodcosmetics",
            StoreThemeTemplates.Red,
            "SJC",
            "alsujood-cosmetics",
            "س",
            "#4c0519",
            "#be123c",
            categories,
            [
                Cosmetic("velvet-burgundy-lipstick", "روج برغندي Velvet", "lips", "لون برغندي مخملي وثابت.", "روج مطفي بتركيبة مريحة يعطي تغطية واضحة ولمسة فاخرة للطلعات والمناسبات.", 18m, 25m, 76, "lipstick", BurgundyMakeupPalettes(), CosmeticVariants("برغندي", "توتي", "وردي غامق"), ImageSet(lipstickRed, lipstickCream)),
                Cosmetic("soft-matte-lip-cream", "Lip Cream مطفي ناعم", "lips", "ملمس خفيف بدرجات يومية.", "كريم شفاه ثابت بدون جفاف، مناسب للاستخدام اليومي مع درجات طبيعية وناعمة.", 15m, 21m, 88, "lipstick", RoseMakeupPalettes(), CosmeticVariants("نود وردي", "موف", "كرز"), ImageSet(lipstickCream, lipGloss)),
                Cosmetic("flawless-liquid-foundation", "فاونديشن Flawless", "face", "تغطية ناعمة وطبيعية.", "فاونديشن سائل بلمسة ساتان، يوحد لون البشرة ويبقى مريح لساعات طويلة.", 32m, 42m, 54, "bottle", BasePalettes(), CosmeticVariants("فاتح", "متوسط", "قمحي"), ImageSet(foundation)),
                Cosmetic("silky-pressed-powder", "بودرة مضغوطة Silky", "face", "تثبيت ناعم بدون تكتل.", "بودرة خفيفة تثبت المكياج وتخفف اللمعان مع إحساس حريري على البشرة.", 22m, 29m, 63, "compact", PowderPalettes(), CosmeticVariants("شفاف", "بيج", "دافئ"), ImageSet(compactPowder)),
                Cosmetic("rose-blush-compact", "بلاشر Rose Compact", "face", "لون خدود وردي طبيعي.", "بلاشر ناعم يمنح الوجه إشراقة مرتبة، قابل للبناء ومناسب لكل يوم.", 19m, 27m, 70, "compact", BlushPalettes(), CosmeticVariants("وردي", "خوخي", "توتي"), ImageSet(blushCompact)),
                Cosmetic("golden-burgundy-eyeshadow", "باليت ظلال Burgundy Gold", "eyes", "درجات برغندي وذهبي.", "باليت عيون بدرجات مطفية ولامعة، مناسبة للوك يومي أو سهرة مرتبة.", 36m, 48m, 42, "palette", ShadowPalettes(), CosmeticVariants("برغندي", "ذهبي", "بني"), ImageSet(eyeshadow)),
                Cosmetic("volume-lash-mascara", "ماسكارا Volume Lash", "eyes", "كثافة ورموش أوضح.", "ماسكارا سوداء بفرشاة تفصل الرموش وتمنح كثافة بدون تكتل.", 20m, 28m, 66, "brush", MascaraPalettes(), CosmeticVariants("أسود", "بني", "أسود مقاوم للماء"), ImageSet(mascara)),
                Cosmetic("gentle-gel-cleanser", "غسول Gel لطيف", "skincare", "تنظيف يومي بدون جفاف.", "غسول جل لطيف يزيل الشوائب ويحافظ على إحساس البشرة بالراحة والنظافة.", 17m, 24m, 82, "tube", SkincarePalettes(), CosmeticVariants("عادي", "مختلط", "حساس"), ImageSet(cleanser)),
                Cosmetic("hyaluronic-glow-serum", "سيروم Hyaluronic Glow", "skincare", "ترطيب ولمعة صحية.", "سيروم خفيف بحمض الهيالورونيك يساعد البشرة تبدو ممتلئة وناعمة.", 28m, 38m, 58, "dropper", SerumPalettes(), CosmeticVariants("30ml", "50ml", "مجموعة"), ImageSet(serum)),
                Cosmetic("daily-hydra-moisturizer", "مرطب Daily Hydra", "skincare", "ترطيب يومي خفيف.", "كريم مرطب سريع الامتصاص مناسب قبل المكياج وبعد الغسول.", 24m, 34m, 64, "jar", CreamPalettes(), CosmeticVariants("خفيف", "غني", "ليلي"), ImageSet(moisturizer)),
                Cosmetic("spf-50-sun-veil", "واقي شمس SPF 50", "skincare", "حماية يومية بملمس ناعم.", "واقي شمس خفيف لا يترك طبقة ثقيلة، مناسب للروتين الصباحي وتحت المكياج.", 26m, 36m, 60, "tube", SunscreenPalettes(), CosmeticVariants("شفاف", "Tinted", "Matte"), ImageSet(sunscreen)),
                Cosmetic("oud-rose-mist", "عطر Mist ورد وعود", "fragrance", "رائحة وردية دافئة.", "Body mist أنيق بلمسة ورد وعود خفيفة، مناسب للحقيبة والاستخدام اليومي.", 21m, 30m, 72, "perfume", FragrancePalettes(), CosmeticVariants("ورد", "عود", "مسك"), ImageSet(bodyMist, perfumeClassic)),
                Cosmetic("glass-shine-lip-gloss", "Lip Gloss لمعة زجاجية", "lips", "جلوس لامع بملمس غير لاصق.", "لمعة شفافة ناعمة تعطي الشفاه مظهر ممتلئ ومرتب فوق الروج أو لوحدها.", 14m, 20m, 82, "lipstick", RoseMakeupPalettes(), CosmeticVariants("شفاف", "وردي", "توتي"), ImageSet(lipGloss)),
                Cosmetic("cherry-lip-tint", "تنت شفاه Cherry", "lips", "لون خفيف وثابت.", "تنت شفاه وخدود بدرجة كرز طبيعية، مناسب للدوام والطلعات السريعة.", 13m, 18m, 79, "lipstick", BurgundyMakeupPalettes(), CosmeticVariants("كرز", "روز", "توت"), ImageSet(lipstickCream)),
                Cosmetic("nourish-lip-balm", "بلسم شفاه Nourish", "lips", "ترطيب يومي للشفاه.", "بلسم شفاه مغذي يعطي ترطيب ونعومة قبل الروج أو أثناء اليوم.", 9m, 13m, 95, "jar", CreamPalettes(), CosmeticVariants("شفاف", "وردي", "فانيلا"), ImageSet(lipGloss)),
                Cosmetic("precision-lip-liner", "محدد شفاه Precision", "lips", "تحديد مرتب وثابت.", "قلم تحديد شفاه يساعد على رسم الحواف وتثبيت لون الروج لفترة أطول.", 11m, 16m, 68, "brush", BurgundyMakeupPalettes(), CosmeticVariants("نود", "برغندي", "بني وردي"), ImageSet(lipstickRed)),
                Cosmetic("smooth-liquid-concealer", "كونسيلر Smooth", "face", "تغطية للهالات والتصبغات.", "كونسيلر سائل خفيف يندمج بسهولة ويعطي تغطية مرتبة بدون ثقل.", 21m, 29m, 64, "bottle", BasePalettes(), CosmeticVariants("Light", "Medium", "Warm"), ImageSet(foundation)),
                Cosmetic("soft-contour-stick", "كونتور Stick ناعم", "face", "تحديد طبيعي للوجه.", "كونتور كريمي سهل الدمج للأنف والخدود وخط الفك.", 18m, 26m, 58, "lipstick", BasePalettes(), CosmeticVariants("Light", "Medium", "Deep"), ImageSet(foundation)),
                Cosmetic("pearl-glow-highlighter", "هايلايتر Pearl Glow", "face", "لمعة ناعمة للخدود.", "هايلايتر مضغوط يعطي إضاءة لطيفة بدون لمعان مبالغ فيه.", 24m, 32m, 52, "compact", PowderPalettes(), CosmeticVariants("Pearl", "Champagne", "Rose Gold"), ImageSet(compactPowder)),
                Cosmetic("pro-makeup-brush-set", "طقم فرش Makeup Pro", "face", "فرش تطبيق ودمج كاملة.", "مجموعة فرش للفاونديشن، البودرة، البلاشر، والظلال بخامة ناعمة.", 34m, 45m, 46, "brush", PowderPalettes(), CosmeticVariants("Basic", "Complete", "Travel"), ImageSet(makeupBrushes)),
                Cosmetic("dewy-setting-spray", "Setting Spray Dewy", "face", "تثبيت ولمعة صحية.", "سبراي تثبيت يساعد المكياج يبقى مرتب ويعطي لمسة نضارة خفيفة.", 19m, 27m, 61, "bottle", SkincarePalettes(), CosmeticVariants("Dewy", "Matte", "Long Wear"), ImageSet(settingSpray)),
                Cosmetic("black-precision-eyeliner", "آيلاينر Precision Black", "eyes", "خط أسود واضح وسهل.", "آيلاينر سائل بطرف دقيق لرسم خط ثابت من أول مرة.", 16m, 22m, 72, "brush", MascaraPalettes(), CosmeticVariants("أسود", "بني", "مقاوم للماء"), ImageSet(mascara)),
                Cosmetic("brow-define-pencil", "قلم حواجب Brow Define", "eyes", "تعبئة طبيعية للحواجب.", "قلم حواجب بدرجات طبيعية مع طرف دقيق لشكل مرتب وغير قاس.", 14m, 19m, 70, "brush", MascaraPalettes(), CosmeticVariants("Light Brown", "Dark Brown", "Graphite"), ImageSet(makeupBrushes)),
                Cosmetic("neutral-everyday-eyeshadow", "باليت ظلال Neutral Daily", "eyes", "درجات يومية سهلة الدمج.", "باليت عيون بدرجات نود وبني ووردي مناسبة للمكياج اليومي.", 31m, 42m, 44, "palette", ShadowPalettes(), CosmeticVariants("Nude", "Warm", "Smoky"), ImageSet(eyeshadow)),
                Cosmetic("clear-brow-gel", "جل حواجب Clear Hold", "eyes", "تثبيت للحواجب بدون لون.", "جل شفاف يمشط الحواجب ويثبتها بشكل طبيعي خلال اليوم.", 13m, 18m, 75, "brush", MascaraPalettes(), CosmeticVariants("Clear", "Brown", "Strong Hold"), ImageSet(mascara)),
                Cosmetic("vitamin-c-bright-serum", "سيروم Vitamin C Bright", "skincare", "إشراقة وتوحيد لون.", "سيروم فيتامين C خفيف يساعد على مظهر بشرة أكثر نضارة وتجانس.", 30m, 42m, 54, "dropper", SerumPalettes(), CosmeticVariants("15ml", "30ml", "50ml"), ImageSet(serum)),
                Cosmetic("retinol-night-serum", "سيروم Retinol Night", "skincare", "روتين ليلي ناعم.", "سيروم ليلي بتركيبة مخصصة للعناية بمظهر الخطوط والملمس.", 34m, 48m, 42, "dropper", SerumPalettes(), CosmeticVariants("Gentle", "Regular", "Advanced"), ImageSet(retinol)),
                Cosmetic("balance-rose-toner", "تونر Balance Rose", "skincare", "تجهيز وترطيب بعد الغسول.", "تونر لطيف ينعش البشرة بعد التنظيف ويجهزها للسيروم والمرطب.", 18m, 25m, 70, "bottle", SkincarePalettes(), CosmeticVariants("Rose", "Hydra", "Calm"), ImageSet(toner)),
                Cosmetic("micellar-cleansing-water", "Micellar Cleansing Water", "skincare", "إزالة مكياج لطيفة.", "ماء ميسيلار لإزالة المكياج والشوائب بدون فرك قوي.", 16m, 23m, 76, "bottle", SkincarePalettes(), CosmeticVariants("Sensitive", "Hydrating", "Oil Control"), ImageSet(micellar)),
                Cosmetic("clay-pore-mask", "ماسك Clay Pore", "skincare", "تنظيف عميق للمسام.", "ماسك طيني يساعد على تنقية البشرة وتقليل اللمعان الزائد.", 22m, 31m, 50, "jar", CreamPalettes(), CosmeticVariants("Clay", "Rose Clay", "Charcoal"), ImageSet(moisturizer)),
                Cosmetic("floral-body-mist", "Body Mist Floral", "fragrance", "رائحة زهرية خفيفة.", "بودي ميست يومي برائحة زهور ناعمة مناسب بعد الشاور أو قبل الخروج.", 19m, 27m, 78, "perfume", FragrancePalettes(), CosmeticVariants("Floral", "Vanilla", "Musk"), ImageSet(bodyMist, perfumeFloral)),
                Cosmetic("pocket-perfume-oil", "زيت عطر Pocket Perfume", "fragrance", "عطر مركز للحقيبة.", "زيت عطري صغير الحجم مناسب للمشاوير وإعادة تعطير سريعة.", 17m, 24m, 66, "perfume", FragrancePalettes(), CosmeticVariants("Rose Oud", "Amber", "Musk"), ImageSet(perfumeClassic, perfumeFloral))
            ],
            coverImage);
    }

    private static StoreSeed ClothesStore()
    {
        var categories = new[]
        {
            new CategorySeed("tops", "بلوزات وتيشيرتات", "قطع يومية ناعمة بألوان سهلة التنسيق.", "#7f1d1d", "#f97316"),
            new CategorySeed("hoodies", "هوديز وسويت شيرتات", "طبقات دافئة ومريحة للجامعة والمشاوير.", "#1f2937", "#0f766e"),
            new CategorySeed("bottoms", "بناطيل", "قصات عملية تناسب اللبس اليومي والطلعات.", "#312e81", "#db2777")
        };

        return new StoreSeed(
            "style-wave",
            "Style Wave",
            "Style",
            "CLOTHES123@GMAIL.COM",
            "StyleWave",
            "Fashion",
            "متجر ملابس عصري بألوان واضحة، خامات مريحة، ومقاسات متعددة لكل قطعة.",
            "ستايل عملي وجذاب: بلوزات، هوديز، بناطيل، وفساتين بصور ملونة وخيارات مقاس ولون تساعد الزبون يختار بسرعة.",
            "قطع مصممة لتبان جميلة على الشاشة وعلى أرض الواقع، مع خيارات ألوان ومقاسات واضحة لكل منتج.",
            "970599223344",
            "stylewave",
            "StyleWave",
            "stylewave",
            StoreThemeTemplates.Red,
            "STW",
            "style-wave",
            "S",
            "#7f1d1d",
            "#db2777",
            categories,
            [
                Clothing("basic-cotton-tee", "تيشيرت قطن أساسي", "tops", "تيشيرت ناعم بألوان يومية.", "قماش قطني مريح، مناسب للدوام والجامعة، متوفر بأكثر من لون ومقاس.", 19m, 25m, 90, "shirt", NeutralPalettes(), SizeColorVariants("أبيض", "أسود", "بيج")),
                Clothing("oversized-graphic-tee", "تيشيرت واسع بطباعة", "tops", "قصة oversized مع طباعة أنيقة.", "قطعة شبابية سهلة التنسيق مع جينز أو بنطال قماش، وخيارات ألوان متعددة.", 24m, 32m, 70, "shirt", StreetPalettes(), SizeColorVariants("رمادي", "أزرق كحلي", "خمري")),
                Clothing("ribbed-long-sleeve", "بلوزة Ribbed طويلة", "tops", "بلوزة خفيفة بقصة مرتبة.", "نسيج ribbed مرن ومريح، مناسب للطبقات اليومية والطلعات الهادية.", 29m, 39m, 62, "shirt", SoftPalettes(), SizeColorVariants("وردي", "كريمي", "زيتوني")),
                Clothing("linen-summer-shirt", "قميص كتان صيفي", "tops", "قميص خفيف ومرتب.", "كتان ناعم للتنقل والطقس الدافئ، متوفر بألوان فاتحة وأحجام متعددة.", 34m, 45m, 55, "shirt", LinenPalettes(), SizeColorVariants("أبيض", "سماوي", "رملي")),
                Clothing("premium-zip-hoodie", "هودي بسحاب Premium", "hoodies", "هودي دافئ بسحاب عملي.", "خامة ثقيلة وناعمة من الداخل، مناسب للمساء والجامعة، بألوان محايدة.", 49m, 65m, 50, "shirt", HoodiePalettes(), SizeColorVariants("أسود", "رمادي", "زيتي")),
                Clothing("cropped-denim-jacket", "جاكيت دينم قصير", "hoodies", "جاكيت جينز بقصة عصرية.", "طبقة خفيفة تعطي اللوك شكل مرتب، متوفر بدرجات دينم مختلفة.", 59m, 75m, 38, "shirt", DenimPalettes(), SizeColorVariants("أزرق", "أسود", "ثلجي")),
                Clothing("puffer-light-jacket", "جاكيت Puffer خفيف", "hoodies", "دفء خفيف بدون ثقل.", "جاكيت مناسب للمشاوير اليومية، ألوان واضحة وقصة مريحة.", 69m, 89m, 34, "shirt", PufferPalettes(), SizeColorVariants("أسود", "بيج", "أخضر")),
                Clothing("wide-leg-jeans", "جينز Wide Leg", "bottoms", "جينز واسع ومريح.", "قصة واسعة سهلة الحركة، متوفرة بدرجات أزرق وأسود وبمقاسات متعددة.", 44m, 59m, 58, "pants", DenimPalettes(), SizeColorVariants("أزرق", "أسود", "فاتح")),
                Clothing("tailored-straight-pants", "بنطال Straight رسمي", "bottoms", "بنطال مرتب للشغل والجامعة.", "قصة مستقيمة بخامة عملية، يناسب القمصان والبلوزات اليومية.", 39m, 52m, 60, "pants", TailoredPalettes(), SizeColorVariants("أسود", "كاكي", "رمادي")),
                Clothing("pleated-midi-skirt", "تنورة Midi بكسرات", "bottoms", "تنورة أنيقة وسهلة التنسيق.", "قماش خفيف بحركة جميلة، خيارات ألوان هادئة ومقاسات متنوعة.", 42m, 55m, 42, "dress", SkirtPalettes(), SizeColorVariants("أسود", "وردي", "كحلي")),
                Clothing("casual-knit-dress", "فستان Knit يومي", "bottoms", "فستان ناعم ومريح.", "قصة عملية للطلعات اليومية، ألوان دافئة ومقاسات متعددة.", 54m, 69m, 36, "dress", KnitPalettes(), SizeColorVariants("بيج", "بني", "أسود")),
                Clothing("athletic-jogger-set", "طقم Jogger رياضي", "hoodies", "طقم مريح للحركة اليومية.", "هودي وبنطال jogger بخامة مريحة، مناسب للسفر والرياضة الخفيفة.", 64m, 82m, 44, "pants", SportPalettes(), SizeColorVariants("رمادي", "أسود", "أخضر"))
            ]);
    }

    private static StoreSeed CupsStore()
    {
        var categories = new[]
        {
            new CategorySeed("coffee", "أكواب قهوة", "أكواب سيراميك للبيت والمكتب.", "#78350f", "#f59e0b"),
            new CategorySeed("travel", "أكواب حرارية", "Tumblers وأكواب سفر تحفظ الحرارة.", "#0f766e", "#155e75"),
            new CategorySeed("gift", "أكواب هدايا", "تصاميم لطيفة للتغليف والهدايا.", "#7c2d12", "#be123c")
        };

        return new StoreSeed(
            "mug-mood",
            "Mug Mood",
            "Cups",
            "CUPS123@GMAIL.COM",
            "MugMood",
            "Drinkware",
            "متجر أكواب ملوّن للقهوة، الشاي، السفر، والهدايا اليومية.",
            "أكواب سيراميك وحرارية بتصاميم مغرية، ألوان واضحة، وصور مناسبة لكل منتج.",
            "اختيارات لطيفة للقهوة والشاي والهدايا، مع ألوان وسعات مختلفة لكل ذوق.",
            "970599334455",
            "mugmood",
            "MugMood",
            "mugmood",
            StoreThemeTemplates.Brown,
            "MUG",
            "mug-mood",
            "M",
            "#78350f",
            "#f59e0b",
            categories,
            [
                Cup("classic-ceramic-mug", "كوب سيراميك كلاسيك", "coffee", "كوب يومي للقهوة.", "سيراميك لامع بمقبض مريح وسعة مناسبة للقهوة الصباحية.", 12m, 16m, 80, "mug", CoffeePalettes(), CupVariants("أبيض", "أسود", "عسلي")),
                Cup("matte-latte-mug", "كوب لاتيه Matte", "coffee", "ملمس مطفي وألوان هادئة.", "كوب واسع للقهوة بالحليب والشاي، بألوان مطفية مرتبة.", 15m, 20m, 74, "mug", LattePalettes(), CupVariants("كريمي", "وردي", "رمادي")),
                Cup("espresso-cup-set", "طقم أكواب Espresso", "coffee", "طقم صغير للقهوة المركزة.", "أكواب صغيرة أنيقة مع ألوان متناسقة، مناسبة للضيافة.", 18m, 24m, 50, "mug", EspressoPalettes(), CupVariants("أبيض", "بني", "أسود")),
                Cup("glass-tea-cup", "كوب شاي زجاجي", "coffee", "زجاج شفاف للشاي والأعشاب.", "كوب خفيف بمظهر نظيف يظهر لون المشروب، مناسب للشاي اليومي.", 10m, 14m, 88, "glass", GlassPalettes(), CupVariants("شفاف", "عنبر", "دخاني")),
                Cup("insulated-travel-tumbler", "كوب سفر حراري", "travel", "يحافظ على حرارة المشروب.", "Tumbler بغطاء محكم مناسب للسيارة والدوام، بسعات وألوان مختلفة.", 24m, 32m, 66, "tumbler", TravelPalettes(), CapacityVariants("أسود", "فضي", "أخضر")),
                Cup("stainless-steel-tumbler", "Tumbler ستانلس ستيل", "travel", "قوي وعملي للمشاوير.", "ستانلس ستيل بجدار مزدوج، مناسب للقهوة الباردة والساخنة.", 29m, 39m, 54, "tumbler", SteelPalettes(), CapacityVariants("فضي", "أسود", "أزرق")),
                Cup("sport-water-cup", "كوب ماء رياضي", "travel", "خفيف للحركة والرياضة.", "كوب بغطاء سريع وسعة جيدة للنادي والمشي.", 19m, 26m, 70, "tumbler", SportCupPalettes(), CapacityVariants("أزرق", "أخضر", "بنفسجي")),
                Cup("minimal-desk-mug", "كوب مكتب Minimal", "gift", "تصميم هادئ للمكتب.", "كوب أنيق للمكاتب والهدايا البسيطة، بألوان نظيفة.", 14m, 19m, 72, "mug", MinimalCupPalettes(), CupVariants("أبيض", "رمادي", "كحلي")),
                Cup("heart-gift-mug", "كوب هدية Heart", "gift", "تصميم لطيف للهدايا.", "كوب برسمة قلب ناعمة، مناسب للتغليف والمناسبات الصغيرة.", 16m, 22m, 60, "mug", GiftPalettes(), CupVariants("وردي", "أحمر", "أبيض")),
                Cup("custom-name-mug", "كوب اسم مخصص", "gift", "كوب هدية باسم الشخص.", "تصميم قابل للتخصيص، مناسب للأصدقاء والزملاء والهدايا اليومية.", 20m, 28m, 45, "mug", CustomPalettes(), CupVariants("أبيض", "أسود", "ذهبي")),
                Cup("speckled-handmade-mug", "كوب Handmade منقط", "coffee", "شكل حرفي دافئ.", "ملمس يدوي ونقاط ناعمة تعطي شكل مميز على رف القهوة.", 22m, 29m, 38, "mug", HandmadePalettes(), CupVariants("رملي", "أخضر", "طيني")),
                Cup("kids-color-cup", "كوب أطفال ملوّن", "gift", "خفيف وآمن للأطفال.", "كوب لطيف بألوان مرحة وسعة مناسبة للأطفال.", 9m, 13m, 95, "mug", KidsPalettes(), CupVariants("أصفر", "سماوي", "وردي"))
            ]);
    }

    private static ProductSeed Clothing(
        string slug,
        string name,
        string categoryKey,
        string shortDescription,
        string description,
        decimal price,
        decimal compareAtPrice,
        int stock,
        string shape,
        Palette[] palettes,
        VariantSeed[] variants) =>
        new(slug, name, categoryKey, shortDescription, description, price, compareAtPrice, stock, ProductKind.Clothing, shape, palettes, variants);

    private static ProductSeed Cup(
        string slug,
        string name,
        string categoryKey,
        string shortDescription,
        string description,
        decimal price,
        decimal compareAtPrice,
        int stock,
        string shape,
        Palette[] palettes,
        VariantSeed[] variants) =>
        new(slug, name, categoryKey, shortDescription, description, price, compareAtPrice, stock, ProductKind.Cup, shape, palettes, variants);

    private static ProductSeed Cosmetic(
        string slug,
        string name,
        string categoryKey,
        string shortDescription,
        string description,
        decimal price,
        decimal compareAtPrice,
        int stock,
        string shape,
        Palette[] palettes,
        VariantSeed[] variants) =>
        new(slug, name, categoryKey, shortDescription, description, price, compareAtPrice, stock, ProductKind.Cosmetic, shape, palettes, variants);

    private static ProductSeed Cosmetic(
        string slug,
        string name,
        string categoryKey,
        string shortDescription,
        string description,
        decimal price,
        decimal compareAtPrice,
        int stock,
        string shape,
        Palette[] palettes,
        VariantSeed[] variants,
        string[] imageUrls) =>
        new(slug, name, categoryKey, shortDescription, description, price, compareAtPrice, stock, ProductKind.Cosmetic, shape, palettes, variants, imageUrls);

    private static VariantSeed[] SizeColorVariants(string first, string second, string third) =>
    [
        new($"{first} - S", 0m, 14, $"لون {first} بمقاس S."),
        new($"{second} - M", 3m, 16, $"لون {second} بمقاس M."),
        new($"{third} - L", 5m, 12, $"لون {third} بمقاس L."),
        new($"{first} - XL", 7m, 9, $"لون {first} بمقاس XL.")
    ];

    private static VariantSeed[] CupVariants(string first, string second, string third) =>
    [
        new($"{first} - 300ml", 0m, 18, $"كوب بلون {first} وسعة 300ml."),
        new($"{second} - 350ml", 2m, 16, $"كوب بلون {second} وسعة 350ml."),
        new($"{third} - 450ml", 4m, 12, $"كوب بلون {third} وسعة 450ml.")
    ];

    private static VariantSeed[] CapacityVariants(string first, string second, string third) =>
    [
        new($"{first} - 500ml", 0m, 15, $"نسخة {first} بسعة 500ml."),
        new($"{second} - 750ml", 5m, 14, $"نسخة {second} بسعة 750ml."),
        new($"{third} - 1L", 8m, 10, $"نسخة {third} بسعة 1L.")
    ];

    private static VariantSeed[] CosmeticVariants(string first, string second, string third) =>
    [
        new(first, 0m, 18, $"خيار {first} من السجود كوزمتكس."),
        new(second, 3m, 14, $"خيار {second} من السجود كوزمتكس."),
        new(third, 5m, 10, $"خيار {third} من السجود كوزمتكس.")
    ];

    private static Palette[] NeutralPalettes() => [MakePalette("أبيض ناعم", "#f8fafc", "#cbd5e1", "#f8fafc", "#111827"), MakePalette("أسود كلاسيك", "#111827", "#475569", "#111827", "#f8fafc"), MakePalette("بيج دافئ", "#fef3c7", "#d97706", "#f5e6ca", "#92400e")];
    private static Palette[] StreetPalettes() => [MakePalette("رمادي بطباعة", "#334155", "#0f172a", "#64748b", "#f8fafc"), MakePalette("كحلي عميق", "#1e3a8a", "#0f172a", "#1e40af", "#bfdbfe"), MakePalette("خمري جريء", "#7f1d1d", "#111827", "#991b1b", "#fecaca")];
    private static Palette[] SoftPalettes() => [MakePalette("وردي ناعم", "#f9a8d4", "#be185d", "#fbcfe8", "#9d174d"), MakePalette("كريمي", "#fff7ed", "#ea580c", "#ffedd5", "#9a3412"), MakePalette("زيتوني", "#365314", "#84cc16", "#4d7c0f", "#ecfccb")];
    private static Palette[] LinenPalettes() => [MakePalette("أبيض كتان", "#f8fafc", "#94a3b8", "#f1f5f9", "#334155"), MakePalette("سماوي", "#bae6fd", "#0284c7", "#7dd3fc", "#075985"), MakePalette("رملي", "#fde68a", "#b45309", "#f5d08a", "#78350f")];
    private static Palette[] HoodiePalettes() => [MakePalette("أسود ثقيل", "#0f172a", "#334155", "#111827", "#e2e8f0"), MakePalette("رمادي رياضي", "#64748b", "#1f2937", "#94a3b8", "#f8fafc"), MakePalette("زيتي", "#14532d", "#0f172a", "#166534", "#dcfce7")];
    private static Palette[] DenimPalettes() => [MakePalette("دينم أزرق", "#1d4ed8", "#0f172a", "#2563eb", "#dbeafe"), MakePalette("دينم أسود", "#111827", "#475569", "#1f2937", "#f8fafc"), MakePalette("دينم ثلجي", "#bfdbfe", "#2563eb", "#dbeafe", "#1e3a8a")];
    private static Palette[] PufferPalettes() => [MakePalette("أسود لامع", "#020617", "#475569", "#111827", "#f8fafc"), MakePalette("بيج شتوي", "#fed7aa", "#9a3412", "#f5c58b", "#7c2d12"), MakePalette("أخضر غامق", "#064e3b", "#14b8a6", "#065f46", "#ccfbf1")];
    private static Palette[] TailoredPalettes() => [MakePalette("أسود رسمي", "#111827", "#4b5563", "#1f2937", "#f9fafb"), MakePalette("كاكي", "#713f12", "#d97706", "#a16207", "#fef3c7"), MakePalette("رمادي", "#475569", "#94a3b8", "#64748b", "#f8fafc")];
    private static Palette[] SkirtPalettes() => [MakePalette("أسود", "#111827", "#374151", "#111827", "#f9fafb"), MakePalette("وردي", "#f9a8d4", "#be185d", "#fbcfe8", "#9d174d"), MakePalette("كحلي", "#172554", "#3b82f6", "#1e3a8a", "#dbeafe")];
    private static Palette[] KnitPalettes() => [MakePalette("بيج Knit", "#fde68a", "#b45309", "#e6c17a", "#78350f"), MakePalette("بني دافئ", "#7c2d12", "#fed7aa", "#92400e", "#ffedd5"), MakePalette("أسود", "#111827", "#475569", "#1f2937", "#f8fafc")];
    private static Palette[] SportPalettes() => [MakePalette("رمادي رياضي", "#64748b", "#111827", "#94a3b8", "#f8fafc"), MakePalette("أسود", "#0f172a", "#334155", "#111827", "#e2e8f0"), MakePalette("أخضر", "#166534", "#84cc16", "#15803d", "#dcfce7")];

    private static Palette[] CoffeePalettes() => [MakePalette("أبيض قهوة", "#78350f", "#f59e0b", "#f8fafc", "#92400e"), MakePalette("أسود مطفي", "#111827", "#78350f", "#111827", "#fef3c7"), MakePalette("عسلي", "#92400e", "#fbbf24", "#d97706", "#fff7ed")];
    private static Palette[] LattePalettes() => [MakePalette("كريمي مطفي", "#fff7ed", "#c2410c", "#ffedd5", "#9a3412"), MakePalette("وردي مطفي", "#f9a8d4", "#be185d", "#fbcfe8", "#9d174d"), MakePalette("رمادي هادئ", "#64748b", "#cbd5e1", "#94a3b8", "#111827")];
    private static Palette[] EspressoPalettes() => [MakePalette("أبيض Espresso", "#f8fafc", "#92400e", "#f8fafc", "#78350f"), MakePalette("بني", "#7c2d12", "#f59e0b", "#92400e", "#ffedd5"), MakePalette("أسود", "#111827", "#475569", "#111827", "#f8fafc")];
    private static Palette[] GlassPalettes() => [MakePalette("شفاف", "#e0f2fe", "#0ea5e9", "#bae6fd", "#075985"), MakePalette("عنبر", "#f59e0b", "#78350f", "#fbbf24", "#fff7ed"), MakePalette("دخاني", "#334155", "#0f172a", "#64748b", "#f8fafc")];
    private static Palette[] TravelPalettes() => [MakePalette("أسود سفر", "#0f172a", "#0f766e", "#111827", "#ccfbf1"), MakePalette("فضي", "#94a3b8", "#334155", "#cbd5e1", "#0f172a"), MakePalette("أخضر", "#064e3b", "#14b8a6", "#047857", "#ecfdf5")];
    private static Palette[] SteelPalettes() => [MakePalette("فضي ستانلس", "#cbd5e1", "#475569", "#e2e8f0", "#0f172a"), MakePalette("أسود ستيل", "#111827", "#475569", "#111827", "#f8fafc"), MakePalette("أزرق", "#1d4ed8", "#0f172a", "#2563eb", "#dbeafe")];
    private static Palette[] SportCupPalettes() => [MakePalette("أزرق رياضي", "#0284c7", "#0f172a", "#0ea5e9", "#e0f2fe"), MakePalette("أخضر", "#16a34a", "#064e3b", "#22c55e", "#dcfce7"), MakePalette("بنفسجي", "#6d28d9", "#1e1b4b", "#8b5cf6", "#ede9fe")];
    private static Palette[] MinimalCupPalettes() => [MakePalette("أبيض Minimal", "#f8fafc", "#94a3b8", "#ffffff", "#111827"), MakePalette("رمادي", "#64748b", "#cbd5e1", "#94a3b8", "#111827"), MakePalette("كحلي", "#172554", "#3b82f6", "#1e3a8a", "#dbeafe")];
    private static Palette[] GiftPalettes() => [MakePalette("وردي هدية", "#f9a8d4", "#be185d", "#fbcfe8", "#9d174d"), MakePalette("أحمر", "#b91c1c", "#7f1d1d", "#ef4444", "#fee2e2"), MakePalette("أبيض قلب", "#f8fafc", "#e11d48", "#ffffff", "#be123c")];
    private static Palette[] CustomPalettes() => [MakePalette("أبيض مخصص", "#f8fafc", "#f59e0b", "#ffffff", "#92400e"), MakePalette("أسود مخصص", "#111827", "#f59e0b", "#111827", "#fef3c7"), MakePalette("ذهبي", "#fbbf24", "#78350f", "#f59e0b", "#fff7ed")];
    private static Palette[] HandmadePalettes() => [MakePalette("رملي منقط", "#fde68a", "#92400e", "#f5d08a", "#78350f"), MakePalette("أخضر حرفي", "#047857", "#064e3b", "#10b981", "#ecfdf5"), MakePalette("طيني", "#9a3412", "#fed7aa", "#c2410c", "#fff7ed")];
    private static Palette[] KidsPalettes() => [MakePalette("أصفر مرح", "#facc15", "#fb923c", "#fde047", "#78350f"), MakePalette("سماوي", "#7dd3fc", "#0284c7", "#bae6fd", "#075985"), MakePalette("وردي", "#f9a8d4", "#db2777", "#fbcfe8", "#9d174d")];

    private static Palette[] BurgundyMakeupPalettes() => [MakePalette("برغندي مخملي", "#4c0519", "#9f1239", "#881337", "#fecdd3"), MakePalette("توتي عميق", "#831843", "#be123c", "#9f1239", "#ffe4e6"), MakePalette("وردي فاخر", "#be123c", "#fda4af", "#e11d48", "#fff1f2")];
    private static Palette[] RoseMakeupPalettes() => [MakePalette("نود وردي", "#9f1239", "#fda4af", "#fb7185", "#fff1f2"), MakePalette("موف ناعم", "#701a75", "#f0abfc", "#a21caf", "#fae8ff"), MakePalette("كرز", "#7f1d1d", "#e11d48", "#be123c", "#ffe4e6")];
    private static Palette[] BasePalettes() => [MakePalette("فاتح ساتان", "#fdf2f8", "#be123c", "#f5d0b5", "#881337"), MakePalette("متوسط", "#fbcfe8", "#9f1239", "#d6a06f", "#4c0519"), MakePalette("قمحي", "#9f1239", "#f59e0b", "#b7794c", "#fff7ed")];
    private static Palette[] PowderPalettes() => [MakePalette("شفاف ناعم", "#fff1f2", "#e11d48", "#f8fafc", "#9f1239"), MakePalette("بيج حريري", "#fef3c7", "#be123c", "#f5d0a8", "#7c2d12"), MakePalette("دافئ", "#fda4af", "#92400e", "#d97706", "#fff7ed")];
    private static Palette[] BlushPalettes() => [MakePalette("وردي خدود", "#881337", "#fb7185", "#f9a8d4", "#4c0519"), MakePalette("خوخي", "#9f1239", "#fb923c", "#fdba74", "#7c2d12"), MakePalette("توتي", "#4c0519", "#be123c", "#9f1239", "#ffe4e6")];
    private static Palette[] ShadowPalettes() => [MakePalette("برغندي وذهب", "#4c0519", "#d4af37", "#9f1239", "#fef3c7"), MakePalette("ذهبي لامع", "#7c2d12", "#fbbf24", "#d4af37", "#fff7ed"), MakePalette("بني دافئ", "#431407", "#be123c", "#92400e", "#ffedd5")];
    private static Palette[] MascaraPalettes() => [MakePalette("أسود كثيف", "#020617", "#9f1239", "#111827", "#fecdd3"), MakePalette("بني ناعم", "#431407", "#be123c", "#78350f", "#fed7aa"), MakePalette("مقاوم للماء", "#111827", "#475569", "#0f172a", "#f8fafc")];
    private static Palette[] SkincarePalettes() => [MakePalette("جل وردي", "#fff1f2", "#be123c", "#fda4af", "#4c0519"), MakePalette("حساس", "#f8fafc", "#fb7185", "#ffffff", "#881337"), MakePalette("مختلط", "#fecdd3", "#9f1239", "#f472b6", "#4c0519")];
    private static Palette[] SerumPalettes() => [MakePalette("Glow وردي", "#881337", "#fbcfe8", "#fb7185", "#fff1f2"), MakePalette("Hyaluronic", "#f8fafc", "#e11d48", "#e0f2fe", "#9f1239"), MakePalette("مجموعة", "#4c0519", "#d4af37", "#be123c", "#fef3c7")];
    private static Palette[] CreamPalettes() => [MakePalette("كريم خفيف", "#fff1f2", "#be123c", "#ffffff", "#9f1239"), MakePalette("غني", "#fef3c7", "#9f1239", "#fde68a", "#7c2d12"), MakePalette("ليلي", "#4c0519", "#be123c", "#881337", "#ffe4e6")];
    private static Palette[] SunscreenPalettes() => [MakePalette("SPF شفاف", "#f8fafc", "#be123c", "#ffffff", "#9f1239"), MakePalette("Tinted", "#fbcfe8", "#92400e", "#d6a06f", "#4c0519"), MakePalette("Matte", "#881337", "#fecdd3", "#f8fafc", "#9f1239")];
    private static Palette[] FragrancePalettes() => [MakePalette("ورد وعود", "#4c0519", "#d4af37", "#9f1239", "#fef3c7"), MakePalette("مسك وردي", "#881337", "#fda4af", "#fb7185", "#fff1f2"), MakePalette("عود فاخر", "#431407", "#be123c", "#78350f", "#fef3c7")];

    private static Palette MakePalette(string label, string backgroundA, string backgroundB, string main, string secondary) =>
        new(label, backgroundA, backgroundB, main, secondary);

    private static string AssetUrl(StoreSeed seed, string name) =>
        PublishedImages.TryGetValue($"{seed.AssetFolder}/{Slug(name)}", out var publishedUrl)
            ? publishedUrl
            : $"/wwwroot/seed-images/{seed.AssetFolder}/{Slug(name)}.svg";

    private static string ProductImageUrl(StoreSeed storeSeed, ProductSeed seed, int index)
    {
        if (UseLocalSeedAssets(storeSeed))
            return AssetUrl(storeSeed, $"{seed.Slug}-{index}");

        if (seed.ImageUrls is { Length: > 0 })
            return seed.ImageUrls[(index - 1) % seed.ImageUrls.Length];

        return AssetUrl(storeSeed, $"{seed.Slug}-{index}");
    }

    private static bool UseLocalSeedAssets(StoreSeed seed) =>
        string.Equals(seed.AssetFolder, "alsujood-cosmetics", StringComparison.OrdinalIgnoreCase);

    private static string[] ImageSet(params string[] urls) => urls;

    private static string ProductSlug(StoreSeed seed, string key) => $"{seed.Slug}-{key}";
    private static string CategorySlug(StoreSeed seed, string key) => $"{seed.Slug}-{key}";

    private static string Slug(string value)
    {
        var normalized = value.ToLowerInvariant();
        var chars = normalized.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
        var slug = new string(chars);

        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);

        return slug.Trim('-');
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    private static void EnsureIdentityResultSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded)
            return;

        var errors = string.Join(", ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));
        throw new InvalidOperationException($"Failed to {operation}. {errors}");
    }

    private enum ProductKind
    {
        Clothing,
        Cup,
        Cosmetic
    }

    private sealed record StoreSeed(
        string Slug,
        string Name,
        string OwnerFirstName,
        string OwnerEmail,
        string DisplayName,
        string BusinessType,
        string Description,
        string StoreStory,
        string CoverSubtitle,
        string WhatsAppNumber,
        string Instagram,
        string Facebook,
        string TikTok,
        string ThemeTemplate,
        string SkuPrefix,
        string AssetFolder,
        string LogoLetter,
        string AccentA,
        string AccentB,
        CategorySeed[] Categories,
        ProductSeed[] Products,
        string? CoverImageUrl = null,
        string? LogoImageUrl = null);

    private sealed record CategorySeed(
        string Key,
        string Name,
        string Description,
        string ColorA,
        string ColorB,
        string? ImageUrl = null);
    private sealed record CategoryTranslationSeed(
        string EnglishName,
        string EnglishDescription,
        string HebrewName,
        string HebrewDescription);
    private sealed record ContactSeed(string Platform, string Username, string Label, int SortOrder);
    private sealed record Palette(string Label, string BackgroundA, string BackgroundB, string Main, string Secondary);
    private sealed record VariantSeed(string Name, decimal PriceDelta, int Stock, string Description);
    private sealed record PhotoCatalogEntry(string Name, string Description, string[] VariantNames, string[] VariantImageUrls);
    private sealed record ProductTranslationSeed(
        string EnglishName,
        string EnglishShortDescription,
        string EnglishDescription,
        string HebrewName,
        string HebrewShortDescription,
        string HebrewDescription);
    private sealed record ProductSeed(
        string Slug,
        string Name,
        string CategoryKey,
        string ShortDescription,
        string Description,
        decimal Price,
        decimal CompareAtPrice,
        int Stock,
        ProductKind Kind,
        string Shape,
        Palette[] Palettes,
        VariantSeed[] Variants,
        string[]? ImageUrls = null);
}
