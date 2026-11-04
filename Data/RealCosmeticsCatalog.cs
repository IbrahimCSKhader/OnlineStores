using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using onlineStore.Models;

namespace onlineStore.Data;

public static class RealCosmeticsCatalog
{
    public static async Task ApplyAsync(AppDbContext context, string webRootPath)
    {
        var path = Path.Combine(webRootPath, "store-branding", "alsujood", "real-products.json");
        if (!File.Exists(path)) return;
        var catalog = JsonSerializer.Deserialize<Catalog>(await File.ReadAllTextAsync(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        if (catalog.Products.Count != 32 || catalog.Products.Select(x => x.Slug).Distinct().Count() != 32)
            throw new InvalidOperationException("Expected 32 distinct documented cosmetics products.");
        var store = await context.Stores.SingleAsync(s => s.Slug == "alsujood-cosmetics");
        var products = await context.Products.Include(p => p.Images).Include(p => p.Variants).Where(p => p.StoreId == store.Id).ToListAsync();
        var categories = await context.Categories.Where(c => c.StoreId == store.Id).ToListAsync();
        var translations = await context.StoreTranslations.Where(t => t.StoreId == store.Id).ToListAsync();
        foreach (var entry in catalog.Products)
        {
            if (entry.Images.Count == 0 || !Uri.IsWellFormedUriString(entry.Source, UriKind.Absolute)) throw new InvalidOperationException("Missing product provenance.");
            var product = products.Single(p => p.Slug == entry.Slug);
            product.Name = entry.Name;
            product.Description = $"{entry.Name}. راجعي بيانات العبوة وتعليمات الشركة قبل الاستخدام.";
            product.ShortDescription = entry.Name;
            product.MetaTitle = entry.Name;
            product.MetaDescription = entry.Name;
            product.ThumbnailUrl = entry.Images[0];
            var category = categories.Single(c => c.Id == product.CategoryId);
            if (string.IsNullOrEmpty(category.ImageUrl) || category.ImageUrl.Contains("seed-images/alsujood")) category.ImageUrl = entry.Images[0];
            context.ProductImages.RemoveRange(product.Images);
            foreach (var (url,index) in entry.Images.Distinct().Select((url,index)=>(url,index))) context.ProductImages.Add(new ProductImage { ProductId = product.Id, Url = url, AltText = entry.Name, DisplayOrder = index, IsPrimary = index == 0 });
            var variants = product.Variants.OrderByDescending(v => v.IsDefault).ThenBy(v => v.SortOrder).ToList();
            for (var index = 0; index < variants.Count; index++)
            {
                var variant = variants[index];
                variant.IsActive = index == 0;
                variant.IsDefault = index == 0;
                variant.Name = entry.Name;
                variant.Description = entry.Name;
                variant.ImageUrl = entry.Images[0];
            }
            var ids = variants.Select(v => v.Id).ToList();
            var attributes = await context.ProductVariantAttributeValues.Where(v => ids.Contains(v.VariantId)).ToListAsync();
            context.ProductVariantAttributeValues.RemoveRange(attributes);
            foreach (var translation in translations.Where(t => t.EntityId == product.Id || ids.Contains(t.EntityId)))
                translation.Value = entry.Name;
        }
        if (!string.IsNullOrWhiteSpace(catalog.LogoUrl)) store.LogoUrl = catalog.LogoUrl;
        if (!string.IsNullOrWhiteSpace(catalog.CoverImageUrl)) store.CoverImageUrl = catalog.CoverImageUrl;
        store.Description = "السجود كوزمتكس | الجمال بتفاصيله. تشكيلة مكياج وعناية بالبشرة وعطور من علامات معروفة.";
        store.StoreStory = "نختار للجمال تفاصيله: مكياج للشفاه والعيون والبشرة، عناية يومية وعطور مميزة. تفاصيل صغيرة تكمل إطلالتك وتمنح يومك لمسة أجمل.";
        await context.SaveChangesAsync();
    }

    public sealed class Catalog
    {
        public string? LogoUrl { get; set; }
        public string? CoverImageUrl { get; set; }
        public List<Entry> Products { get; set; } = [];
    }
    public sealed class Entry
    {
        public string Slug { get; set; } = "";
        public string Name { get; set; } = "";
        public string Source { get; set; } = "";
        public List<string> Images { get; set; } = [];
    }
}
