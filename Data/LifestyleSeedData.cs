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
        store.LogoUrl = AssetUrl(seed, "store-logo");
        store.CoverImageUrl = AssetUrl(seed, "store-cover");
        store.WhatsAppNumber = seed.WhatsAppNumber;
        store.StoreStory = seed.StoreStory;
        store.ThemeTemplate = seed.ThemeTemplate;
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
            category.ImageUrl = AssetUrl(seed, $"category-{definition.Key}");
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
            product.ThumbnailUrl = AssetUrl(storeSeed, $"{seed.Slug}-1");
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

        for (var i = 1; i <= 3; i++)
        {
            context.ProductImages.Add(new ProductImage
            {
                ProductId = product.Id,
                Url = AssetUrl(storeSeed, $"{seed.Slug}-{i}"),
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
            variant.ImageUrl = AssetUrl(storeSeed, $"{seed.Slug}-{(i == 3 ? 1 : Math.Min(i + 1, 3))}");
            if (PhotoCatalog.TryGetValue($"{storeSeed.AssetFolder}/{seed.Slug}", out var photoEntry)
                && i < photoEntry.VariantImageUrls.Length)
                variant.ImageUrl = photoEntry.VariantImageUrls[i];
            variant.IsDefault = i == 0;
            variant.IsActive = true;
            variant.IsDeleted = false;
            variant.SortOrder = i + 1;
        }
    }

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
                    product.Kind == ProductKind.Clothing
                        ? BuildClothingSvg(seed, product, i)
                        : BuildCupSvg(seed, product, i));
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

    private static StoreSeed[] Stores() =>
    [
        ClothesStore(),
        CupsStore()
    ];

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

    private static Palette MakePalette(string label, string backgroundA, string backgroundB, string main, string secondary) =>
        new(label, backgroundA, backgroundB, main, secondary);

    private static string AssetUrl(StoreSeed seed, string name) =>
        PublishedImages.TryGetValue($"{seed.AssetFolder}/{Slug(name)}", out var publishedUrl)
            ? publishedUrl
            : $"/wwwroot/seed-images/{seed.AssetFolder}/{Slug(name)}.svg";

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
        Cup
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
        ProductSeed[] Products);

    private sealed record CategorySeed(string Key, string Name, string Description, string ColorA, string ColorB);
    private sealed record ContactSeed(string Platform, string Username, string Label, int SortOrder);
    private sealed record Palette(string Label, string BackgroundA, string BackgroundB, string Main, string Secondary);
    private sealed record VariantSeed(string Name, decimal PriceDelta, int Stock, string Description);
    private sealed record PhotoCatalogEntry(string Name, string Description, string[] VariantNames, string[] VariantImageUrls);
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
        VariantSeed[] Variants);
}
