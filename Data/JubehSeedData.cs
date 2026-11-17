using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using onlineStore.Models;
using onlineStore.Models.Enums;
using onlineStore.Models.Identity;

namespace onlineStore.Data;

public static class JubehSeedData
{
    private const string StoreSlug = "jubeh-office-furniture";
    private const string OwnerEmail = "nidaljubeh90@gmail.com";
    private const string AssetPath = "/wwwroot/store-branding/jubeh";
    private static readonly Dictionary<string, decimal> InitialPrices = new()
    {
        [$"{StoreSlug}-wood-desk"] = 1200m,
        [$"{StoreSlug}-charcoal-desk"] = 1450m,
        [$"{StoreSlug}-ribbed-desk"] = 1800m,
        [$"{StoreSlug}-olive-desk"] = 1650m,
        [$"{StoreSlug}-office-bookcase"] = 650m,
        [$"{StoreSlug}-mesh-chair"] = 120m,
        [$"{StoreSlug}-mesh-chair-chrome"] = 170m,
    };

    public static async Task SeedAsync(AppDbContext db, UserManager<AppUser> users, RoleManager<AppRole> roles)
    {
        var existingStore = await db.Stores.IgnoreQueryFilters()
            .Where(store => store.Slug == StoreSlug)
            .Select(store => new { store.Id, store.IsDeleted })
            .SingleOrDefaultAsync();
        if (existingStore != null)
        {
            if (!existingStore.IsDeleted)
                await FillInitialPricesAsync(db, existingStore.Id);
            return;
        }

        var owner = await users.FindByEmailAsync(OwnerEmail);
        if (owner is null)
        {
            owner = new AppUser
            {
                UserName = OwnerEmail,
                Email = OwnerEmail,
                EmailConfirmed = true,
                IsActive = true,
                FirstName = "نضال",
                LastName = "الجعبة",
            };
            EnsureSucceeded(await users.CreateAsync(owner));
        }
        else if (!owner.IsActive || owner.IsDeleted || await users.IsInRoleAsync(owner, "SuperAdmin"))
        {
            throw new InvalidOperationException("The Jubeh owner email is assigned to an incompatible account.");
        }

        if (!owner.EmailConfirmed)
        {
            owner.EmailConfirmed = true;
            EnsureSucceeded(await users.UpdateAsync(owner));
        }

        if (!await roles.RoleExistsAsync("StoreOwner"))
            EnsureSucceeded(await roles.CreateAsync(new AppRole { Name = "StoreOwner" }));
        if (!await users.IsInRoleAsync(owner, "StoreOwner"))
            EnsureSucceeded(await users.AddToRoleAsync(owner, "StoreOwner"));

        var store = new Store
        {
            Slug = StoreSlug,
            Name = "الجعبة للأثاث المكتبي",
            OwnerId = owner.Id,
            Description = "مكاتب إدارية وكراسي وخزائن لتجهيز مساحات العمل.",
            BusinessType = "أثاث مكتبي",
            StoreStory = "الجعبة للأثاث المكتبي يعرض تشكيلة من المكاتب والكراسي والخزائن للمكاتب ومساحات العمل. للاستفسار عن الأسعار والمقاسات والتوفر، تواصلوا معنا على الأرقام التالية:\n0568067006\n0528601833",
            WhatsAppNumber = "972528601833",
            LogoUrl = $"{AssetPath}/logo.png",
            CoverImageUrl = $"{AssetPath}/ribbed-desk.jpg",
            ThemeTemplate = StoreThemeTemplates.AppNavbar,
            Language = StoreLanguages.Arabic,
            EnableLanguageSwitching = true,
            IsActive = true,
            ContactAccounts =
            [
                new() { Platform = StoreContactPlatforms.Facebook, Username = "jubehusedofficefurneture", SortOrder = 0 },
                new() { Platform = StoreContactPlatforms.Instagram, Username = "jubeh.office.furnture", SortOrder = 1 },
                new() { Platform = StoreContactPlatforms.TikTok, Username = "jubeh.office", SortOrder = 2 },
                new() { Platform = StoreContactPlatforms.WhatsApp, Username = "972528601833", SortOrder = 3 },
            ],
        };
        var section = new Section { Store = store, Name = "الأثاث المكتبي", Slug = $"{StoreSlug}-office" };
        var desks = new Category { Store = store, Name = "مكاتب إدارية", Slug = $"{StoreSlug}-desks", ImageUrl = $"{AssetPath}/ribbed-desk.jpg" };
        var chairs = new Category { Store = store, Name = "كراسي مكتبية", Slug = $"{StoreSlug}-chairs", ImageUrl = $"{AssetPath}/mesh-chair.jpg", DisplayOrder = 1 };
        var storage = new Category { Store = store, Name = "خزائن ومكتبات", Slug = $"{StoreSlug}-storage", ImageUrl = $"{AssetPath}/office-bookcase.jpg", DisplayOrder = 2 };

        db.Products.AddRange(
            Product("wood-desk", "مكتب إداري بلون الخشب الطبيعي", "سطح بلون الخشب مع فتحة لتمرير الأسلاك وواجهة رمادية.", desks, featured: true),
            Product("charcoal-desk", "مكتب إداري فحمي", "سطح خشبي داكن بفتحتين للأسلاك، مع واجهة سوداء بتفاصيل خطية.", desks),
            Product("ribbed-desk", "مكتب إداري بواجهة مخططة", "سطح بدرجة خشبية دافئة، مع زوايا مستديرة وواجهة بخطوط عمودية.", desks, featured: true),
            Product("olive-desk", "مكتب إداري زيتوني", "مكتب بسطح داكن ووحدة جانبية، مع خطوط أفقية وتفاصيل بلون ذهبي.", desks),
            Product("office-bookcase", "مكتبة مكتبية بأرفف وأبواب", "وحدة تخزين رأسية بأرفف مفتوحة في الأعلى وخزانة ببابين في الأسفل.", storage),
            Product("mesh-chair", "كرسي مكتبي شبكي بقاعدة سوداء", "كرسي دوّار بظهر شبكي ومسند رأس وذراعين وقاعدة بعجلات.", chairs, featured: true),
            Product("mesh-chair-chrome", "كرسي مكتبي شبكي بقاعدة كروم", "كرسي دوّار بمسند رأس ودعم لأسفل الظهر على قاعدة لامعة بعجلات.", chairs));
        await db.SaveChangesAsync();

        Product Product(string key, string name, string description, Category category, bool featured = false)
        {
            var imageUrl = $"{AssetPath}/{key}.jpg";
            var slug = $"{StoreSlug}-{key}";
            return new Product
            {
                Store = store, Section = section, Category = category,
                Name = name, Slug = slug, ShortDescription = description,
                ThumbnailUrl = imageUrl, Images = [new() { Url = imageUrl, AltText = name, IsPrimary = true }],
                Variants = [new() { Name = "Default", IsDefault = true }],
                Status = ProductStatus.Active, IsFeatured = featured,
                Price = InitialPrices[slug], TrackInventory = false,
            };
        }
    }

    private static async Task FillInitialPricesAsync(AppDbContext db, Guid storeId)
    {
        var slugs = InitialPrices.Keys.ToArray();
        var products = await db.Products
            .Where(product => product.StoreId == storeId && product.PriceOnRequest && product.Price == 0m
                && slugs.Contains(product.Slug))
            .ToListAsync();

        foreach (var product in products)
        {
            product.Price = InitialPrices[product.Slug];
            product.PriceOnRequest = false;
            if (product.StockQuantity == 0)
                product.TrackInventory = false;
        }

        if (products.Count > 0)
            await db.SaveChangesAsync();
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException($"Could not provision the Jubeh store owner: {string.Join(", ", result.Errors.Select(error => error.Description))}");
    }
}
