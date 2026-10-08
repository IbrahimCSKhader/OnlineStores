using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using onlineStore.Models;
using onlineStore.Models.Enums;
using onlineStore.Models.Identity;
using onlineStore.Models.Subscriptions;
using onlineStore.Models.Subscriptions.Enums;
using System.Net;

namespace onlineStore.Data;

public static class ElectroPulseSeedData
{
    private const string OwnerEmail = "USER123@GMAIL.COM";
    private const string OwnerPassword = "PASS1234";
    private const string StoreSlug = "electropulse-hub";
    private const string ImageAssetsPath = "/wwwroot/seed-images/electropulse";

    public static async Task SeedAsync(AppDbContext context, UserManager<AppUser> userManager, string webRootPath)
    {
        EnsureLogoAsset(webRootPath);

        var owner = await EnsureOwnerAsync(userManager);
        var store = await EnsureStoreAsync(context, owner.Id);
        var products = Products();
        var categoryDefinitions = CategoryDefinitions();

        await HideObsoleteCatalogAsync(
            context,
            store.Id,
            products.Select(x => ProductSlug(x.Slug)).ToHashSet(StringComparer.OrdinalIgnoreCase),
            categoryDefinitions.Select(x => CategorySlug(x.Key)).ToHashSet(StringComparer.OrdinalIgnoreCase),
            categoryDefinitions.Select(x => SectionSlug(x.Key)).ToHashSet(StringComparer.OrdinalIgnoreCase));

        var categories = await EnsureCategoriesAsync(context, store.Id, categoryDefinitions);
        var sections = await EnsureSectionsAsync(context, store.Id, categoryDefinitions);
        await EnsureContactAccountsAsync(context, store.Id);
        await EnsureSubscriptionAsync(context, store.Id);
        await EnsureProductsAsync(context, store.Id, products, categories, sections);

        await context.SaveChangesAsync();
    }

    private static async Task<AppUser> EnsureOwnerAsync(UserManager<AppUser> userManager)
    {
        var owner = await userManager.FindByEmailAsync(OwnerEmail);

        if (owner == null)
        {
            owner = new AppUser
            {
                FirstName = "ElectroPulse",
                LastName = "Owner",
                Email = OwnerEmail,
                UserName = OwnerEmail,
                EmailConfirmed = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var createResult = await userManager.CreateAsync(owner);
            EnsureIdentityResultSucceeded(createResult, $"create store owner {OwnerEmail}");
        }

        var shouldUpdate = false;
        if (!owner.EmailConfirmed)
        {
            owner.EmailConfirmed = true;
            shouldUpdate = true;
        }

        if (!owner.IsActive)
        {
            owner.IsActive = true;
            shouldUpdate = true;
        }

        if (owner.IsDeleted)
        {
            owner.IsDeleted = false;
            shouldUpdate = true;
        }

        if (!string.Equals(owner.Email, OwnerEmail, StringComparison.Ordinal))
        {
            owner.Email = OwnerEmail;
            shouldUpdate = true;
        }

        if (!string.Equals(owner.UserName, OwnerEmail, StringComparison.Ordinal))
        {
            owner.UserName = OwnerEmail;
            shouldUpdate = true;
        }

        if (shouldUpdate)
        {
            var updateResult = await userManager.UpdateAsync(owner);
            EnsureIdentityResultSucceeded(updateResult, $"update store owner {OwnerEmail}");
        }

        if (!await userManager.IsInRoleAsync(owner, "StoreOwner"))
        {
            var roleResult = await userManager.AddToRoleAsync(owner, "StoreOwner");
            EnsureIdentityResultSucceeded(roleResult, $"assign StoreOwner role to {OwnerEmail}");
        }

        if (!await userManager.CheckPasswordAsync(owner, OwnerPassword))
        {
            owner.PasswordHash = userManager.PasswordHasher.HashPassword(owner, OwnerPassword);
            owner.SecurityStamp = Guid.NewGuid().ToString();
            owner.AccessFailedCount = 0;
            owner.LockoutEnd = null;

            var passwordResult = await userManager.UpdateAsync(owner);
            EnsureIdentityResultSucceeded(passwordResult, $"set seed password for {OwnerEmail}");
        }

        return owner;
    }

    private static async Task<Store> EnsureStoreAsync(AppDbContext context, Guid ownerId)
    {
        var store = await context.Stores.FirstOrDefaultAsync(x => x.Slug == StoreSlug);

        if (store == null)
        {
            store = new Store
            {
                Name = "ElectroPulse Hub",
                Slug = StoreSlug,
                OwnerId = ownerId
            };

            context.Stores.Add(store);
        }

        store.Name = "ElectroPulse Hub";
        store.Description = "متجر إلكترونيات متخصص في أشهر سلاسل اللابتوبات، الهواتف الذكية، وأجهزة الأيباد والتابلت.";
        store.BusinessType = "Electronics";
        store.LogoUrl = Image("tech logo neon electronics");
        store.CoverImageUrl = Photo("photo-1498049794561-7780e7231661", "electronics store devices");
        store.WhatsAppNumber = "970599123456";
        store.StoreStory = "ElectroPulse Hub يجمع أشهر الأجهزة التقنية في تجربة واضحة وسريعة، مع تصنيفات مباشرة وصور واقعية تساعد الزبون يختار الجهاز المناسب بثقة.";
        store.ThemeTemplate = StoreThemeTemplates.Blue;
        store.IsActive = true;
        store.IsDeleted = false;
        store.OwnerId = ownerId;

        return store;
    }

    private static async Task HideObsoleteCatalogAsync(
        AppDbContext context,
        Guid storeId,
        IReadOnlySet<string> desiredProductSlugs,
        IReadOnlySet<string> desiredCategorySlugs,
        IReadOnlySet<string> desiredSectionSlugs)
    {
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
            product.SKU = $"EPH-OLD-{product.Id:N}";

            foreach (var variant in product.Variants)
            {
                variant.IsDeleted = true;
                variant.IsActive = false;
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

        foreach (var section in sections.Where(x => !desiredSectionSlugs.Contains(x.Slug)))
        {
            section.IsDeleted = true;
            section.IsActive = false;
        }
    }

    private static async Task<Dictionary<string, Category>> EnsureCategoriesAsync(
        AppDbContext context,
        Guid storeId,
        CategorySeed[] definitions)
    {
        var result = new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < definitions.Length; i++)
        {
            var definition = definitions[i];
            var slug = CategorySlug(definition.Key);
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
            category.ImageUrl = definition.ImageUrl;
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
        CategorySeed[] definitions)
    {
        var result = new Dictionary<string, Section>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < definitions.Length; i++)
        {
            var definition = definitions[i];
            var slug = SectionSlug(definition.Key);
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

    private static async Task EnsureContactAccountsAsync(AppDbContext context, Guid storeId)
    {
        var accounts = new[]
        {
            new ContactSeed(StoreContactPlatforms.WhatsApp, "970599123456", "WhatsApp", 1),
            new ContactSeed(StoreContactPlatforms.Instagram, "electropulsehub", "Instagram", 2),
            new ContactSeed(StoreContactPlatforms.Facebook, "ElectroPulseHub", "Facebook", 3),
            new ContactSeed(StoreContactPlatforms.TikTok, "electropulsehub", "TikTok", 4),
            new ContactSeed(StoreContactPlatforms.YouTube, "electropulsehub", "YouTube", 5)
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
            Notes = "Seeded demo electronics store subscription."
        });
    }

    private static async Task EnsureProductsAsync(
        AppDbContext context,
        Guid storeId,
        ProductSeed[] products,
        IReadOnlyDictionary<string, Category> categories,
        IReadOnlyDictionary<string, Section> sections)
    {
        for (var i = 0; i < products.Length; i++)
        {
            var seed = products[i];
            var slug = ProductSlug(seed.Slug);
            var product = await context.Products
                .IgnoreQueryFilters()
                .Include(x => x.Variants)
                .FirstOrDefaultAsync(x => x.StoreId == storeId && x.Slug == slug);

            if (product == null)
            {
                product = new Product
                {
                    StoreId = storeId,
                    Slug = slug,
                    SKU = $"EPH-{(i + 1):000}"
                };

                context.Products.Add(product);
            }

            var category = categories[seed.CategoryKey];
            var section = sections[seed.CategoryKey];

            product.Name = seed.Name;
            product.CategoryId = category.Id;
            product.SectionId = section.Id;
            product.Description = seed.Description;
            product.ShortDescription = seed.ShortDescription;
            product.Price = seed.Price;
            product.CompareAtPrice = seed.CompareAtPrice;
            product.CostPrice = Math.Round(seed.Price * 0.72m, 2);
            product.WholesalePrice = Math.Round(seed.Price * 0.88m, 2);
            product.StockQuantity = seed.Stock;
            product.TrackInventory = true;
            product.Status = ProductStatus.Active;
            product.IsFeatured = i < 9;
            product.MetaTitle = $"{seed.Name} | ElectroPulse Hub";
            product.MetaDescription = seed.ShortDescription;
            product.ThumbnailUrl = seed.ImageUrls[0];
            product.IsDeleted = false;

            await EnsureImagesAsync(context, product, seed);
            EnsureVariants(product, seed, i + 1);
        }
    }

    private static async Task EnsureImagesAsync(AppDbContext context, Product product, ProductSeed seed)
    {
        foreach (var entry in context.ChangeTracker.Entries<ProductImage>().ToList())
        {
            if (entry.Entity.ProductId == product.Id)
                entry.State = EntityState.Detached;
        }

        await context.ProductImages
            .Where(x => x.ProductId == product.Id)
            .ExecuteDeleteAsync();

        for (var i = 0; i < seed.ImageUrls.Length; i++)
        {
            context.ProductImages.Add(new ProductImage
            {
                ProductId = product.Id,
                Url = seed.ImageUrls[i],
                AltText = $"{seed.Name} - صورة {i + 1}",
                DisplayOrder = i + 1,
                IsPrimary = i == 0
            });
        }
    }

    private static void EnsureVariants(Product product, ProductSeed seed, int productNumber)
    {
        foreach (var variant in product.Variants)
        {
            variant.IsDeleted = true;
            variant.IsActive = false;
            variant.IsDefault = false;
        }

        var variants = new[]
        {
            new VariantSeed("Standard", 0m, seed.Stock / 2, true, "النسخة الأساسية الجاهزة للتسليم."),
            new VariantSeed("Plus", seed.Price >= 300 ? 49m : 19m, Math.Max(5, seed.Stock / 3), false, "نسخة بسعة أو مواصفات أعلى."),
            new VariantSeed("Pro Bundle", seed.Price >= 300 ? 99m : 39m, Math.Max(3, seed.Stock / 4), false, "باقة كاملة مع إكسسوار مناسب وضمان ممتد.")
        };

        for (var i = 0; i < variants.Length; i++)
        {
            var seedVariant = variants[i];
            var sku = $"EPH-{productNumber:000}-V{i + 1}";
            var variant = product.Variants.FirstOrDefault(x => x.SKU == sku);

            if (variant == null)
            {
                variant = new ProductVariant
                {
                    SKU = sku
                };

                product.Variants.Add(variant);
            }

            variant.Name = $"{seed.Name} - {seedVariant.Name}";
            variant.Description = seedVariant.Description;
            variant.Price = seed.Price + seedVariant.PriceDelta;
            variant.CompareAtPrice = seed.CompareAtPrice + seedVariant.PriceDelta;
            variant.StockQuantity = seedVariant.Stock;
            variant.ImageUrl = seed.ImageUrls[Math.Min(i, seed.ImageUrls.Length - 1)];
            variant.IsDefault = seedVariant.IsDefault;
            variant.IsActive = true;
            variant.IsDeleted = false;
            variant.SortOrder = i + 1;
        }
    }

    private static ProductSeed[] Products() =>
    [
        new("apple-macbook", "Apple MacBook Air / Pro", "laptops", 1299m, 1449m, 18, "MacBook مشهور بخفة التصميم وقوة الأداء.", "سلسلة MacBook Air وMacBook Pro مناسبة للدراسة، البرمجة، التصميم، وصناعة المحتوى مع بطارية قوية وتجربة macOS سلسة.", LaptopImages(0)),
        new("dell-xps-inspiron-alienware", "Dell XPS / Inspiron / Alienware", "laptops", 1199m, 1349m, 16, "لابتوبات Dell للعمل والألعاب.", "اختيارات Dell تغطي الاستخدام اليومي عبر Inspiron، الفئة الفاخرة XPS، وأجهزة Alienware المخصصة للألعاب.", LaptopImages(1)),
        new("lenovo-thinkpad-yoga-legion", "Lenovo ThinkPad / Yoga / Legion", "laptops", 999m, 1149m, 20, "Lenovo للإنتاجية، المرونة، والألعاب.", "ThinkPad للشغل، Yoga للتنقل واللمس، وLegion للألعاب والأداء العالي.", LaptopImages(2)),
        new("hp-spectre-envy-pavilion-omen", "HP Spectre / Envy / Pavilion / Omen", "laptops", 949m, 1099m, 19, "سلاسل HP لكل ميزانية.", "Spectre وEnvy للتصميم الفاخر، Pavilion للاستخدام اليومي، وOmen للألعاب.", LaptopImages(3)),
        new("asus-rog-zenbook-tuf", "ASUS ROG / ZenBook / TUF", "laptops", 1099m, 1249m, 17, "ASUS للألعاب والعمل الخفيف.", "ROG وTUF للألعاب، وZenBook للتنقل والعمل اليومي مع تصميم نحيف.", LaptopImages(4)),
        new("acer-swift-aspire-predator", "Acer Swift / Aspire / Predator", "laptops", 799m, 929m, 22, "Acer خيارات عملية ومتنوعة.", "Swift للتنقل، Aspire للاستخدام اليومي، وPredator للألعاب والأداء القوي.", LaptopImages(5)),
        new("msi-titan-raider-stealth", "MSI Titan / Raider / Stealth", "laptops", 1599m, 1799m, 11, "MSI لأعلى أداء في الألعاب.", "سلاسل Titan وRaider وStealth مناسبة للاعبين والمبدعين الذين يحتاجون قوة رسومية عالية.", LaptopImages(6)),
        new("microsoft-surface-laptop-pro", "Microsoft Surface Laptop / Pro", "laptops", 1099m, 1249m, 14, "Surface بتصميم نظيف وتجربة ويندوز.", "Surface Laptop وSurface Pro مناسبان للتنقل، الدراسة، والاجتماعات مع شاشة لمس وخفة وزن.", LaptopImages(7)),
        new("razer-blade", "Razer Blade", "laptops", 1899m, 2099m, 8, "Razer Blade للألعاب بتصميم فاخر.", "لابتوب ألعاب نحيف وقوي بمظهر احترافي وأداء عالي للشاشة والرسوميات.", LaptopImages(8)),
        new("samsung-galaxy-book", "Samsung Galaxy Book", "laptops", 999m, 1149m, 15, "Galaxy Book خفيف ومناسب للعمل.", "سلسلة Samsung Galaxy Book تقدم تجربة ويندوز خفيفة وتكامل جيد مع أجهزة Galaxy.", LaptopImages(9)),

        new("apple-iphone", "Apple iPhone 15 / 16 / Pro / Pro Max", "phones", 999m, 1099m, 25, "iPhone بتجربة iOS وكاميرات قوية.", "سلاسل iPhone 15 و16 وPro وPro Max تناسب التصوير، الأداء العالي، وتحديثات طويلة.", PhoneImages(0)),
        new("samsung-galaxy", "Samsung Galaxy S / Z / A Series", "phones", 899m, 999m, 28, "Galaxy من الفلاجشيب حتى الفئة المتوسطة.", "سلاسل Galaxy S وZ Fold & Flip وA Series تقدم شاشات قوية وخيارات متنوعة.", PhoneImages(1)),
        new("xiaomi-redmi-poco", "Xiaomi / Redmi / Poco", "phones", 549m, 629m, 32, "Xiaomi مواصفات قوية بسعر منافس.", "تشمل Xiaomi Flagship وRedmi وPoco مع شحن سريع وشاشات ممتازة.", PhoneImages(2)),
        new("google-pixel", "Google Pixel Pro / A Series", "phones", 749m, 849m, 24, "Pixel لتصوير ذكي وأندرويد نقي.", "Pixel Pro وA Series مناسبان لمحبي الكاميرا الذكية وتجربة أندرويد مباشرة.", PhoneImages(3)),
        new("oneplus-nord", "OnePlus Numbered Series / Nord", "phones", 699m, 779m, 21, "OnePlus للأداء السريع والشحن القوي.", "السلسلة الرئيسية وNord تقدمان واجهة سريعة وخيارات جيدة للاستخدام المكثف.", PhoneImages(4)),
        new("huawei-pura-mate", "Huawei Pura / Mate Series", "phones", 799m, 899m, 18, "Huawei بتصميم وكاميرات قوية.", "Pura وMate Series مناسبتان للتصوير والتصميم الأنيق مع بطاريات كبيرة.", PhoneImages(5)),
        new("honor-magic-number", "Honor Magic / Number Series", "phones", 649m, 749m, 23, "Honor خيارات أنيقة وقوية.", "Magic Series وNumber Series تقدمان شاشات كبيرة وتصميمات مميزة.", PhoneImages(6)),
        new("realme-gt-number", "Realme GT / Number Series", "phones", 499m, 579m, 31, "Realme أداء سريع بسعر مناسب.", "GT Series وNumber Series مناسبتان للألعاب الخفيفة والاستخدام اليومي.", PhoneImages(7)),
        new("oppo-find-x-reno", "OPPO Find X / Reno", "phones", 599m, 699m, 26, "OPPO للتصوير والتصميم.", "Find X وReno تقدم كاميرات جيدة وشحن سريع وشكل أنيق.", PhoneImages(8)),
        new("motorola-edge-razr", "Motorola Edge / Razr", "phones", 649m, 749m, 20, "Motorola بين Edge وRazr القابل للطي.", "Edge للأداء اليومي القوي وRazr لمحبي الهواتف القابلة للطي.", PhoneImages(9)),

        new("apple-ipad-pro", "Apple iPad Pro", "tablets", 1099m, 1199m, 14, "iPad Pro للعمل والرسم والمونتاج.", "جهاز لوحي احترافي بشاشة ممتازة وأداء قوي لصناع المحتوى.", TabletImages(0)),
        new("apple-ipad-air", "Apple iPad Air", "tablets", 699m, 799m, 18, "iPad Air خفيف وقوي.", "اختيار متوازن للدراسة، الرسم، والعمل اليومي مع دعم القلم والكيبورد.", TabletImages(1)),
        new("apple-ipad-standard", "Apple iPad Standard", "tablets", 449m, 529m, 24, "iPad الأساسي للاستخدام اليومي.", "مناسب للتعلم، التصفح، الفيديو، والملاحظات بسعر أقل من الفئات الأعلى.", TabletImages(2)),
        new("apple-ipad-mini", "Apple iPad mini", "tablets", 549m, 629m, 20, "iPad mini صغير وسهل الحمل.", "جهاز لوحي مدمج للقراءة والملاحظات والتنقل اليومي.", TabletImages(3)),
        new("samsung-galaxy-tab-s", "Samsung Galaxy Tab S", "tablets", 899m, 999m, 15, "Tab S فئة أندرويد العليا.", "مثل Tab S9 وS10 Ultra مع شاشة كبيرة وقلم وتجربة إنتاجية قوية.", TabletImages(4)),
        new("samsung-galaxy-tab-a", "Samsung Galaxy Tab A", "tablets", 299m, 369m, 30, "Tab A للفئة الاقتصادية.", "مناسب للطلاب، الفيديو، والتصفح اليومي بسعر عملي.", TabletImages(5)),
        new("xiaomi-pad", "Xiaomi Pad 6 / Pro", "tablets", 399m, 469m, 25, "Xiaomi Pad شاشة كبيرة وسعر منافس.", "Xiaomi Pad 6 وPro مناسبان للترفيه، الدراسة، والعمل الخفيف.", TabletImages(6)),
        new("lenovo-tab", "Lenovo Tab P12 / Yoga Tab", "tablets", 349m, 429m, 22, "Lenovo Tab للدراسة والترفيه.", "Tab P12 وYoga Tab يوفران شاشة مريحة وخيارات عملية للعائلة والدراسة.", TabletImages(7)),
        new("huawei-matepad", "Huawei MatePad Pro", "tablets", 549m, 649m, 17, "MatePad للتصميم والملاحظات.", "Huawei MatePad Pro مناسب للرسم، القراءة، والعمل مع شاشة أنيقة.", TabletImages(8)),
        new("microsoft-surface-pro", "Microsoft Surface Pro", "tablets", 999m, 1149m, 13, "Surface Pro جهاز 2 في 1 بنظام ويندوز.", "يجمع بين التابلت واللابتوب، مناسب للدوام، الدراسة، والعروض.", TabletImages(9))
    ];

    private static CategorySeed[] CategoryDefinitions() =>
    [
        new("laptops", "لابتوبات", "أشهر سلاسل اللابتوبات للعمل، الدراسة، الألعاب، وصناعة المحتوى.", Photo("photo-1496181133206-80ce9b88a853", "laptop on desk")),
        new("phones", "هواتف ذكية", "أشهر سلاسل الهواتف الذكية من الفئات الاقتصادية حتى الفلاجشيب.", Photo("photo-1511707171634-5f897ff02aa9", "smartphones")),
        new("tablets", "أيباد وتابلت", "أجهزة iPad وتابلت Android وWindows للدراسة، الرسم، والعمل.", Photo("photo-1544244015-0df4b3ffc6b0", "tablet device"))
    ];

    private static string[] LaptopImages(int offset) => Pick(offset, [
        Photo("photo-1517336714731-489689fd1ca8", "macbook laptop"),
        Photo("photo-1496181133206-80ce9b88a853", "laptop workspace"),
        Photo("photo-1484788984921-03950022c9ef", "laptop desk"),
        Photo("photo-1525547719571-a2d4ac8945e2", "modern laptop"),
        Photo("photo-1588872657578-7efd1f1555ed", "laptop computer"),
        Photo("photo-1498050108023-c5249f4df085", "developer laptop")
    ]);

    private static string[] PhoneImages(int offset) => Pick(offset, [
        Photo("photo-1511707171634-5f897ff02aa9", "smartphone"),
        Photo("photo-1510557880182-3d4d3cba35a5", "phone hand"),
        Photo("photo-1598327105666-5b89351aff97", "modern phone"),
        Photo("photo-1556656793-08538906a9f8", "smartphone closeup"),
        Photo("photo-1523206489230-c012c64b2b48", "mobile phones"),
        Photo("photo-1533228100845-08145b01de14", "phone camera")
    ]);

    private static string[] TabletImages(int offset) => Pick(offset, [
        Photo("photo-1544244015-0df4b3ffc6b0", "ipad tablet"),
        Photo("photo-1589739900243-4b52cd9b104e", "tablet on desk"),
        Photo("photo-1561154464-82e9adf32764", "tablet device"),
        Photo("photo-1527698266440-12104e498b76", "tablet workspace"),
        Photo("photo-1587033411391-5d9e51cce126", "tablet drawing"),
        Photo("photo-1516321318423-f06f85e504b3", "tablet study")
    ]);

    private static string[] Pick(int offset, string[] values)
    {
        return
        [
            values[offset % values.Length],
            values[(offset + 1) % values.Length],
            values[(offset + 2) % values.Length]
        ];
    }

    private static string Photo(string id, string label)
    {
        return $"https://images.unsplash.com/{id}?auto=format&fit=crop&w=1200&q=80&ixlib=rb-4.0.3&{Slug(label)}";
    }

    private static string Image(string query)
    {
        return $"{ImageAssetsPath}/{Slug(query)}.svg";
    }

    private static void EnsureLogoAsset(string webRootPath)
    {
        if (string.IsNullOrWhiteSpace(webRootPath))
            return;

        var imageDirectory = Path.Combine(webRootPath, "seed-images", "electropulse");
        Directory.CreateDirectory(imageDirectory);

        var filePath = Path.Combine(imageDirectory, $"{Slug("tech logo neon electronics")}.svg");
        File.WriteAllText(filePath, BuildLogoSvg());
    }

    private static string BuildLogoSvg()
    {
        var title = WebUtility.HtmlEncode("ElectroPulse Hub");

        return $$"""
<svg xmlns="http://www.w3.org/2000/svg" width="900" height="700" viewBox="0 0 900 700" role="img" aria-label="{{title}}">
  <defs>
    <linearGradient id="bg" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0%" stop-color="#111827"/>
      <stop offset="100%" stop-color="#0f766e"/>
    </linearGradient>
    <linearGradient id="bolt" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0%" stop-color="#22d3ee"/>
      <stop offset="100%" stop-color="#60a5fa"/>
    </linearGradient>
  </defs>
  <rect width="900" height="700" rx="34" fill="url(#bg)"/>
  <circle cx="450" cy="282" r="150" fill="url(#bolt)" stroke="#e0f2fe" stroke-width="14"/>
  <path d="M474 122 318 326h118l-36 142 166-228H452z" fill="#0f172a"/>
  <text x="450" y="558" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="48" font-weight="800" fill="#ffffff">ElectroPulse Hub</text>
</svg>
""";
    }

    private static string CategorySlug(string key) => $"{StoreSlug}-{key}";
    private static string SectionSlug(string key) => $"{StoreSlug}-{key}";
    private static string ProductSlug(string key) => $"{StoreSlug}-{key}";

    private static string Slug(string value)
    {
        var normalized = value.ToLowerInvariant();
        var chars = normalized.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
        var slug = new string(chars);

        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);

        return slug.Trim('-');
    }

    private static void EnsureIdentityResultSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded)
            return;

        var errors = string.Join(", ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));
        throw new InvalidOperationException($"Failed to {operation}. {errors}");
    }

    private sealed record CategorySeed(string Key, string Name, string Description, string ImageUrl);
    private sealed record ContactSeed(string Platform, string Username, string Label, int SortOrder);
    private sealed record VariantSeed(string Name, decimal PriceDelta, int Stock, bool IsDefault, string Description);
    private sealed record ProductSeed(
        string Slug,
        string Name,
        string CategoryKey,
        decimal Price,
        decimal CompareAtPrice,
        int Stock,
        string ShortDescription,
        string Description,
        string[] ImageUrls);
}
